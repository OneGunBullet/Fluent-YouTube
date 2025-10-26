using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace WinYouTube
{
    // This is a Windows-only app (WebView2). Mark the class to avoid platform-compatibility warnings (CA1416).
    [SupportedOSPlatform("windows")]
    public partial class MainForm : Form
    {
        private readonly string CssFileName = "winui-youtube.css";
        private bool _injectCss = true;

        // id returned by AddScriptToExecuteOnDocumentCreatedAsync so we can attempt to remove it later.
        // Note: RemoveScriptToExecuteOnDocumentCreatedAsync is not present in all WebView2 versions.
        private string? _registeredScriptId;
        private string? _registeredWindowScriptId;

        // optional environment reference to keep alive
        private CoreWebView2Environment? _env;

        // P/Invoke to allow window dragging from JS via host message
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN =0xA1;
        private const int HTCAPTION =0x2;

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            await InitializeWebView2Async();
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                // Create a persistent user data folder (optional)
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinYouTube", "WebView2");
                _env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                // Ensure CoreWebView2 is initialized with the environment
                await webView21.EnsureCoreWebView2Async(_env);

                var core = webView21.CoreWebView2;
                if (core == null)
                {
                    MessageBox.Show("CoreWebView2 initialization failed.", "WebView2", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Basic settings
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = false;

                // ConsoleMessageReceived not available in this WebView2 version; use DevTools instead.

                // In DEBUG builds open DevTools automatically to inspect DOM and injected script
#if DEBUG
                try { core.OpenDevToolsWindow(); } catch { }
#endif

                // Optional: navigation event handlers for debug
                core.NavigationStarting += Core_NavigationStarting;
                core.NavigationCompleted += Core_NavigationCompleted;

                // Listen for messages from the web content (window controls / drag)
                core.WebMessageReceived += Core_WebMessageReceived;

                // Register CSS injection if requested
                if (_injectCss)
                {
                    await RegisterCssInjectionAsync();
                }

                // Register injected window controls (always register so user can drag & control window)
                await RegisterWindowControlsInjectionAsync();

                // Navigate to YouTube
                core.Navigate("https://www.youtube.com/");
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 failed to initialize: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine("WebView2 initialization error: " + ex);
            }
        }

        private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            Debug.WriteLine("NavigationStarting -> " + e.Uri);
        }

        private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Debug.WriteLine($"NavigationCompleted. Success: {e.IsSuccess}, StatusCode: {e.HttpStatusCode}");
        }

        // Register the injection script (runs early on every document)
        private async Task RegisterCssInjectionAsync()
        {
            try
            {
                if (webView21?.CoreWebView2 == null)
                {
                    Debug.WriteLine("RegisterCssInjectionAsync: CoreWebView2 is null.");
                    return;
                }

                string cssPath = Path.Combine(AppContext.BaseDirectory, CssFileName);
                Debug.WriteLine("Checking CSS file at: " + cssPath + " (exists=" + File.Exists(cssPath) + ")");
                if (!File.Exists(cssPath))
                {
                    Debug.WriteLine($"RegisterCssInjectionAsync: CSS file not found at {cssPath}. Skipping injection.");
                    return;
                }

                string css = File.ReadAllText(cssPath);
                Debug.WriteLine($"Loaded CSS length={css.Length} chars");

                string cssJson = JsonSerializer.Serialize(css); // JSON escapes the string

                // A persistent injector: inserts style, observes removals, re-inserts as-needed, updates shadowRoots/iframes,
                // and runs a periodic reassert to survive aggressive DOM replacements.
                string jsTemplate = @"
(function() {
  try {
    const cssText = CSS_TEXT_PLACEHOLDER;
    const STYLE_ID = 'winui-css-injector-v1';
    const PERSIST_CHECK_MS = 1500; // how often to reassert (ms)

    function createStyleNode(doc, css) {
      try {
        const s = doc.createElement('style');
        s.id = STYLE_ID;
        s.type = 'text/css';
        s.appendChild(doc.createTextNode(css));
        return s;
      } catch (e) {
        console.error('winui-css-injector createStyleNode error', e);
        return null;
      }
    }

    function ensureStyleInDocument(doc, css) {
      try {
        let existing = doc.getElementById(STYLE_ID);
        if (existing && existing.tagName === 'STYLE') {
          // update content if needed
          if (existing.textContent !== css) {
            existing.textContent = css;
            console.log('winui-css-injector: updated existing style in', doc.location ? doc.location.href : '(document)');
          }
          return existing;
        }
        const style = createStyleNode(doc, css);
        if (!style) return null;
        (doc.head || doc.documentElement).appendChild(style);
        console.log('winui-css-injector: inserted style into', doc.location ? doc.location.href : '(document)');
        return style;
      } catch (e) {
        console.error('winui-css-injector ensureStyleInDocument error', e);
        return null;
      }
    }

    function injectIntoShadowRoots(root, css) {
      try {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null, false);
        while (walker.nextNode()) {
          const el = walker.currentNode;
          try {
            if (el && el.shadowRoot) {
              if (!el.shadowRoot.getElementById(STYLE_ID)) {
                const s = docCreateStyleForShadow(css);
                if (s) el.shadowRoot.appendChild(s);
                console.log('winui-css-injector: injected into shadowRoot for', el.tagName);
              }
            }
          } catch (e) {
            // closed shadow root or permission denied
          }
        }
      } catch (e) {
        console.error('winui-css-injector injectIntoShadowRoots error', e);
      }
    }

    function docCreateStyleForShadow(css) {
      try {
        const s = document.createElement('style');
        s.id = STYLE_ID;
        s.textContent = css;
        return s;
      } catch (e) { return null; }
    }

    function injectIntoIframes(doc, css) {
      try {
        const iframes = doc.querySelectorAll('iframe');
        for (const frame of iframes) {
          try {
            const fdoc = frame.contentDocument;
            if (fdoc) {
              ensureStyleInDocument(fdoc, css);
              console.log('winui-css-injector: injected into iframe (same-origin) src=', frame.src);
            }
          } catch (e) {
            // cross-origin iframe -> cannot access
          }
        }
      } catch (e) {
        console.error('winui-css-injector injectIntoIframes error', e);
      }
    }

    // Keep the style present on the main document
    function setupPersistence(doc, css) {
      try {
        // Insert or update immediately
        ensureStyleInDocument(doc, css);

        // Observe removals/changes in head and documentElement and re-add if missing
        const observer = new MutationObserver(muts => {
          let found = doc.getElementById(STYLE_ID);
          if (!found) {
            // quickly re-insert
            ensureStyleInDocument(doc, css);
            console.log('winui-css-injector: reinserted style after mutation');
          } else {
            // if content changed, restore correct css
            if (found.textContent !== css) {
              found.textContent = css;
              console.log('winui-css-injector: restored style content after mutation');
            }
          }
        });

        observer.observe(doc.documentElement || doc, { childList: true, subtree: true, attributes: true });

        // Periodic reassert loop (defensive)
        const intervalId = setInterval(() => {
          try {
            let s = doc.getElementById(STYLE_ID);
            if (!s) {
              ensureStyleInDocument(doc, css);
              console.log('winui-css-injector: reasserted style via interval');
            } else if (s.textContent !== css) {
              s.textContent = css;
              console.log('winui-css-injector: refreshed style content via interval');
            }
          } catch (e) {
            // ignore
          }
        }, PERSIST_CHECK_MS);

        // expose a stop API if needed
        return { observer, intervalId };
      } catch (e) {
        console.error('winui-css-injector setupPersistence failed', e);
        return null;
      }
    }

    // Initial insertion + persistence for main document
    const mainPersistence = setupPersistence(document, cssText);

    // Attempt to inject into existing same-origin iframes
    injectIntoIframes(document, cssText);

    // Observe new iframes and shadow containers
    const globalMo = new MutationObserver(muts => {
      for (const m of muts) {
        for (const node of m.addedNodes) {
          try {
            if (node.nodeType === Node.ELEMENT_NODE) {
              const el = node;
              try {
                if (el.tagName === 'IFRAME') {
                  try {
                    const fdoc = el.contentDocument;
                    if (fdoc) {
                      ensureStyleInDocument(fdoc, cssText);
                      console.log('winui-css-injector: injected into newly added iframe');
                    }
                  } catch (e) { /* cross-origin or not ready */ }
                }
                // check for shadowHost elements inside the subtree
                try {
                  if (el.shadowRoot && !el.shadowRoot.getElementById(STYLE_ID)) {
                    const s = docCreateStyleForShadow(cssText);
                    if (s) el.shadowRoot.appendChild(s);
                    console.log('winui-css-injector: injected into newly added shadowRoot for', el.tagName);
                  }
                } catch (e) { /* ignore */ }
              } catch (e) { /* ignore per-node */ }
            }
          } catch (e) { /* ignore */ }
        }
      }
    });

    globalMo.observe(document.documentElement || document, { childList: true, subtree: true });

    // Expose update API to host code
    window.__winui_css_injector = {
      update: function(newCss) {
        try {
          // update main doc style
          try {
            let el = document.getElementById(STYLE_ID);
            if (el) el.textContent = newCss;
            else ensureStyleInDocument(document, newCss);
          } catch (e) {}
          // update same-origin iframes
          try { injectIntoIframes(document, newCss); } catch (e) {}
          console.log('winui-css-injector: update applied, len=' + (newCss ? newCss.length : 0));
        } catch (e) {
          console.error('winui-css-injector update failed', e);
        }
      },
      // for diagnostics
      isPresent: function() {
        try { return !!document.getElementById(STYLE_ID); } catch (e) { return false; }
      }
    };

    console.log('winui-css-injector: initialization complete');
  } catch (e) {
    console.error('WinUI CSS injector fatal', e);
  }
})();;";

                string jsToRegister = jsTemplate.Replace("CSS_TEXT_PLACEHOLDER", cssJson);

                try
                {
                    string scriptId = await webView21.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(jsToRegister);
                    _registeredScriptId = scriptId;
                    Debug.WriteLine("Registered CSS injector, scriptId=" + scriptId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AddScriptToExecuteOnDocumentCreatedAsync failed for injector: " + ex);
                    _registeredScriptId = null;
                }

                // One-off apply to current document (best-effort) and return result for debugging
                try
                {
                    string applyScript = $@"(function(){{ try {{ if(window.__winui_css_injector) {{ window.__winui_css_injector.update({cssJson}); return 'updated-via-api'; }} else {{ const s=document.getElementById('winui-css-injector-v1'); if(!s) {{ const st=document.createElement('style'); st.id='winui-css-injector-v1'; st.appendChild(document.createTextNode({cssJson})); (document.head||document.documentElement).appendChild(st); return 'injected-oneoff'; }} else {{ s.textContent = {cssJson}; return 'updated-existing'; }} }} }} catch(e) {{ return 'apply-exception:' + e.toString(); }} }})();";
                    string applyResult = await webView21.CoreWebView2.ExecuteScriptAsync(applyScript);
                    Debug.WriteLine("Immediate apply script returned: " + applyResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Immediate apply script failed: " + ex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RegisterCssInjectionAsync failed: " + ex);
            }
        }

        // Inject a small control bar into pages to integrate window controls and draggability.
        private async Task RegisterWindowControlsInjectionAsync()
        {
            try
            {
                if (webView21?.CoreWebView2 == null)
                {
                    Debug.WriteLine("RegisterWindowControlsInjectionAsync: CoreWebView2 is null.");
                    return;
                }

                string jsPath = Path.Combine(AppContext.BaseDirectory, "winui-window-controls.js");
                if (!File.Exists(jsPath))
                {
                    Debug.WriteLine("Window controls JS file not found: " + jsPath);
                    return;
                }

                string controlsJs = File.ReadAllText(jsPath);

                try
                {
                    string scriptId = await webView21.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(controlsJs);
                    _registeredWindowScriptId = scriptId;
                    Debug.WriteLine("Registered window controls injector, scriptId=" + scriptId);

                    // Run immediately in current document if possible
                    try { await webView21.CoreWebView2.ExecuteScriptAsync(controlsJs); } catch { }

                    // Verify injector present in current document
                    try
                    {
                        string checkResult = await webView21.CoreWebView2.ExecuteScriptAsync("(function(){ try{ return !!window.__winui_wincontrols_injected; }catch(e){ return 'err:'+e.toString(); } })();");
                        Debug.WriteLine("Injector check returned: " + checkResult);
                        if (checkResult == null || checkResult.IndexOf("true") <0)
                        {
                            // show a non-blocking notice to help debugging
                            Debug.WriteLine("Window controls injector was not detected in the current page.");
                            // show a message box so the user sees the diagnostic when running the app
                            try { this.BeginInvoke(() => MessageBox.Show("Window controls injector not detected in the current page. Check WebView2 DevTools console for errors (CSP, script blocked).", "Injector missing", MessageBoxButtons.OK, MessageBoxIcon.Warning)); } catch { }
                        }
                    }
                    catch (Exception exCheck)
                    {
                        Debug.WriteLine("Injector presence check failed: " + exCheck);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AddScriptToExecuteOnDocumentCreatedAsync failed for window controls injector: " + ex);
                    _registeredWindowScriptId = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RegisterWindowControlsInjectionAsync failed: " + ex);
            }
        }

        // Remove the registered injection (if any). Many WebView2 versions don't have RemoveScriptToExecuteOnDocumentCreatedAsync;
        // therefore we attempt to call it via reflection, and if it's not available we remove the style element via ExecuteScriptAsync.
        private async Task UnregisterCssInjectionAsync()
        {
            try
            {
                if (webView21?.CoreWebView2 == null)
                {
                    _registeredScriptId = null;
                    return;
                }

                bool removedViaApi = false;

                // Try to call RemoveScriptToExecuteOnDocumentCreatedAsync via reflection if it exists.
                try
                {
                    var core = webView21.CoreWebView2;
                    var removeMethod = core.GetType().GetMethod("RemoveScriptToExecuteOnDocumentCreatedAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    if (removeMethod != null && !string.IsNullOrEmpty(_registeredScriptId))
                    {
                        // method exists; invoke it
                        var taskObj = removeMethod.Invoke(core, new object[] { _registeredScriptId });
                        if (taskObj is Task removeTask)
                        {
                            await removeTask.ConfigureAwait(false);
                            removedViaApi = true;
                            Debug.WriteLine("Removed registered script via API: " + _registeredScriptId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Reflection attempt to call RemoveScriptToExecuteOnDocumentCreatedAsync failed: " + ex);
                }

                // If API removal wasn't available or didn't work, do a best-effort removal by deleting the style tag in the current document.
                try
                {
                    string removeScript = @"(function(){ try { const el = document.getElementById('winui-css-injector-v1'); if(el) el.remove(); } catch(e){} })();";
                    await webView21.CoreWebView2.ExecuteScriptAsync(removeScript);
                    Debug.WriteLine("Executed remove-style script in page.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ExecuteScriptAsync remove failed: " + ex);
                }

                // Clear our stored id regardless
                _registeredScriptId = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UnregisterCssInjectionAsync failed: " + ex);
            }
        }

        // Handle messages sent from the web content (via window.chrome.webview.postMessage)
        private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string msg = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(msg)) return;

                // messages: "window-drag", "window-min", "window-max", "window-close"
                switch (msg)
                {
                    case "win-controls-created":
                        Debug.WriteLine("Web: win-controls-created");
                        break;
                    case "win-controls-attached":
                        Debug.WriteLine("Web: win-controls-attached");
                        break;
                    case "win-controls-detached":
                        Debug.WriteLine("Web: win-controls-detached");
                        break;
                    case "window-drag":
                        BeginWindowDrag();
                        break;
                    case "window-min":
                    case "window-minimize":
                        this.Invoke(() => this.WindowState = FormWindowState.Minimized);
                        break;
                    case "window-max":
                    case "window-maximize":
                        this.Invoke(() =>
                        {
                            if (this.WindowState == FormWindowState.Maximized) this.WindowState = FormWindowState.Normal;
                            else this.WindowState = FormWindowState.Maximized;
                        });
                        break;
                    case "window-close":
                        this.Invoke(() => this.Close());
                        break;
                    default:
                        Debug.WriteLine("WebMessageReceived unknown message: " + msg);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Core_WebMessageReceived failed: " + ex);
            }
        }

        private void BeginWindowDrag()
        {
            try
            {
                // Initiate native move of the window
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BeginWindowDrag failed: " + ex);
            }
        }

        // Toggle button handler (wired in Designer)
        private async void btnToggleCss_Click(object? sender, EventArgs e)
        {
            _injectCss = !_injectCss;

            if (_injectCss)
            {
                await RegisterCssInjectionAsync();
            }
            else
            {
                await UnregisterCssInjectionAsync();
            }

            // reload the current page to ensure injection takes effect on new contents
            try
            {
                webView21.CoreWebView2?.Reload();
            }
            catch { }
        }

        private void btnDevTools_Click(object? sender, EventArgs e)
        {
            try
            {
                webView21.CoreWebView2?.OpenDevToolsWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open DevTools: " + ex.Message, "DevTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                webView21?.Dispose();
            }
            catch { }
        }

        private void MainForm_Load_1(object sender, EventArgs e)
        {

        }

        private void btnHostMin_Click(object? sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnHostMax_Click(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized) this.WindowState = FormWindowState.Normal;
            else this.WindowState = FormWindowState.Maximized;
        }

        private void btnHostClose_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void titleBarPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                BeginWindowDrag();
            }
        }
    }
}