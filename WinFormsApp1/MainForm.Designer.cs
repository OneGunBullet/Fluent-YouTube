using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinYouTube
{
    partial class MainForm
    {
        private IContainer components = null!;
        private global::Microsoft.Web.WebView2.WinForms.WebView2 webView21;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
            webView21 = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((ISupportInitialize)webView21).BeginInit();
            SuspendLayout();
            // 
            // webView21
            // 
            webView21.AllowExternalDrop = true;
            webView21.CreationProperties = null;
            webView21.DefaultBackgroundColor = Color.White;
            webView21.Dock = DockStyle.Fill;
            webView21.Location = new Point(0, 0);
            webView21.Name = "webView21";
            webView21.Size = new Size(965, 536);
            webView21.TabIndex = 0;
            webView21.ZoomFactor = 1D;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 16F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(965, 536);
            ControlBox = false;
            Controls.Add(webView21);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            ((ISupportInitialize)webView21).EndInit();
            ResumeLayout(false);

        }
    }
}