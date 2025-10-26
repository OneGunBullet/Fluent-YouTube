(function(){
 try {
 if (window.__winui_wincontrols_injected) return; window.__winui_wincontrols_injected = true;

 // Helper to create elements
 function el(tag, props, style) {
 var e = document.createElement(tag);
 if (props) Object.keys(props).forEach(function(k){ e[k]=props[k]; });
 if (style) Object.keys(style).forEach(function(k){ e.style.setProperty(k, style[k]); });
 return e;
 }

 // Find mast element
 function findMast() {
 return document.querySelector('#masthead-container') || document.querySelector('ytd-masthead') || document.querySelector('#masthead') || document.querySelector('header');
 }

 // Inject mast padding style
 function addMastPadding() {
 try {
 var id = 'winui-mast-padding';
 if (document.getElementById(id)) return;
 var s = document.createElement('style');
 s.id = id;
 s.textContent = '#container.ytd-masthead, div#container.style-scope.ytd-masthead { padding:0 198px 0 16px !important; }';
 document.head.appendChild(s);
 } catch(_){ }
 }

 // Create host + shadow root so site CSS cannot break our visuals
 function createShadowControls() {
 try {
 var existing = document.getElementById('winui-win-controls-host');
 if (existing) return existing;
 var host = el('div', { id: 'winui-win-controls-host' });
 // host should not block page interactions except for our controls
 host.style.setProperty('position','fixed','important');
 host.style.setProperty('inset','0','important');
 host.style.setProperty('pointer-events','none','important');
 host.style.setProperty('z-index','2147483647','important');
 document.body.appendChild(host);

 // ensure mast padding is applied
 addMastPadding();

 var shadow = host.attachShadow({ mode: 'open' });
 var style = document.createElement('style');
 style.textContent = `
:host{ all: initial; position: fixed; inset:0; pointer-events: none; }
#winui-controls { position: absolute; top:6px; right:6px; display:flex; gap:8px; align-items:center; pointer-events: auto; padding:4px; left: auto; }
/* Button base styles - square buttons (width = height) */
.btn { pointer-events: auto; display: inline-flex; align-items: center; justify-content: center; height:36px; width:36px; box-sizing: border-box; border-radius:6px; background: transparent; color: #ffffff; border: none; cursor: pointer; position: relative; overflow: visible; transition: background-color200ms ease, transform120ms ease, box-shadow200ms ease; }
.btn:focus{ outline:2px solid rgba(0,0,0,0.12); outline-offset:2px; }

/* Close button: transparent by default, red icon */
.btn.close{ background: transparent; color: #e53e3e; }

/* min/max transparent by default */
button#win-min.btn, button#win-max.btn { background: transparent; color: #ffffff; }

/* Hover highlight overlay for non-close controls (no lift/shadow) */
.btn::after { content: ''; position: absolute; inset:0; border-radius: inherit; pointer-events: none; opacity:0; transform: scale(0.95); transition: opacity240ms cubic-bezier(.2,.9,.2,1), transform300ms cubic-bezier(.2,.9,.2,1); }
.btn:not(.close):hover::after { background: var(--button-color, rgba(255,255,255,0.06)); opacity:1; transform: scale(1); }

/* Remove lift/box-shadow on hover for all buttons */
.btn:hover { transform: none !important; box-shadow: none !important; }

/* Close button hover: apply red gradient background and white icon */
.btn.close:hover { background: linear-gradient(180deg,#e53e3e,#c53030); color: #fff; }

/* SVG sizing and inherit color */
.btn svg{ width:16px; height:16px; display:block; stroke: currentColor; fill: currentColor; }
`;
 shadow.appendChild(style);

 var container = document.createElement('div');
 container.id = 'winui-controls';
 // set requested inline element.style values immediately
 container.style.position = 'absolute';
 container.style.top = '6px';
 container.style.right = '6px';
 container.style.left = 'auto';

 function makeBtn(id, title, svgEl, extraClass) {
 var b = document.createElement('button');
 b.id = id; b.title = title; b.className = 'btn' + (extraClass? ' ' + extraClass : '');
 b.setAttribute('type','button');
 b.appendChild(svgEl);
 // stop propagation on pointer down so drag logic doesn't get confused
 b.addEventListener('pointerdown', function(e){ e.stopPropagation(); });
 b.addEventListener('click', function(e){ e.stopPropagation(); try{ window.chrome.webview.postMessage(id.replace('win-','window-')); }catch(_){ } });
 return b;
 }

 function createSvg(type){
 var ns = 'http://www.w3.org/2000/svg';
 var svg = document.createElementNS(ns, 'svg');
 // correct small-icon viewBox and sizes
 svg.setAttribute('viewBox', '001616');
 svg.setAttribute('width', '16');
 svg.setAttribute('height', '16');
 svg.setAttribute('aria-hidden', 'true');

 if (type === 'min') {
 var r = document.createElementNS(ns, 'rect');
 r.setAttribute('x', '3');
 r.setAttribute('y', '11');
 r.setAttribute('width', '10');
 r.setAttribute('height', '1');
 r.setAttribute('fill', 'currentColor');
 svg.appendChild(r);

 } else if (type === 'max') {
 var r = document.createElementNS(ns, 'rect');
 r.setAttribute('x', '3');
 r.setAttribute('y', '3');
 r.setAttribute('width', '10');
 r.setAttribute('height', '10');
 r.setAttribute('fill', 'none');
 r.setAttribute('stroke', 'currentColor');
 r.setAttribute('stroke-width', '1.2');
 r.setAttribute('rx', '1');
 svg.appendChild(r);

 } else if (type === 'close') {
 // two lines forming an X
 var p1 = document.createElementNS(ns, 'path');
 p1.setAttribute('d', 'M44 L1212');
 p1.setAttribute('stroke', 'currentColor');
 p1.setAttribute('stroke-width', '1.4');
 p1.setAttribute('stroke-linecap', 'round');
 p1.setAttribute('fill', 'none');

 var p2 = document.createElementNS(ns, 'path');
 p2.setAttribute('d', 'M124 L412');
 p2.setAttribute('stroke', 'currentColor');
 p2.setAttribute('stroke-width', '1.4');
 p2.setAttribute('stroke-linecap', 'round');
 p2.setAttribute('fill', 'none');

 svg.appendChild(p1);
 svg.appendChild(p2);
 }

 return svg;
 }

 var btnMin = makeBtn('win-min','Minimize', createSvg('min'));
 var btnMax = makeBtn('win-max','Maximize', createSvg('max'));
 var btnClose = makeBtn('win-close','Close', createSvg('close'), 'close');

 container.appendChild(btnMin);
 container.appendChild(btnMax);
 container.appendChild(btnClose);
 shadow.appendChild(container);

 return host;
 } catch (e) { return null; }
 }

 // Position controls near mast right and vertically centered
 function positionControls(){
 try {
 var host = document.getElementById('winui-win-controls-host');
 if (!host) host = createShadowControls();
 if (!host) return;
 var shadow = host.shadowRoot;
 if (!shadow) return;
 var container = shadow.getElementById('winui-controls');
 if (!container) return;

 // prefer the specific div#container.style-scope.ytd-masthead if present
 var preferred = document.querySelector('div#container.style-scope.ytd-masthead');
 var mast = preferred || findMast();
 // always apply the fixed inline top/right
 container.style.position = 'absolute';
 container.style.top = '6px';
 container.style.right = '6px';
 container.style.left = 'auto';

 if (!mast) return;
 var mastRect = mast.getBoundingClientRect();
 if (!(mastRect && mastRect.width>0 && mastRect.height>0)) return;
 // compute vertical center (center of mast) and nudge up by2px, then adjust top only
 var centerY = mastRect.top + mastRect.height/2 -2;
 var overlayRect = container.getBoundingClientRect();
 var top = Math.max(6, Math.round(centerY - overlayRect.height/2));
 container.style.top = top + 'px';
 } catch (e) {}
 }

 // Drag logic: start tracking on pointerdown outside our controls and within mast vertical bounds
 function setupDrag(){
 try{
 function onPointerDown(e){
 try{
 var mastElem = document.querySelector('#masthead-container') || document.querySelector('ytd-masthead') || document.querySelector('#masthead') || document.querySelector('header');
 if (!mastElem) return;
 var path = e.composedPath ? e.composedPath() : (function(){ var n=e.target, a=[]; while(n){ a.push(n); n=n.parentNode; } return a; })();
 for(var i=0;i<path.length;i++){ var node=path[i]; if(!node) continue; var h = document.getElementById('winui-win-controls-host'); if (node===h || (node.shadowRoot && node.shadowRoot.host===h)) return; }
 var el = e.target; var tag = (el.tagName||'').toUpperCase();
 if (tag==='A' || tag==='BUTTON' || tag==='INPUT' || tag==='TEXTAREA' || tag==='SELECT') return;
 var rect = mastElem.getBoundingClientRect(); if (e.clientY < rect.top || e.clientY > rect.bottom) return;
 var threshold =6; var active = true; var sx = e.clientX, sy = e.clientY; var pid = e.pointerId || null;
 function onMove(ev){ try{ if(!active) return; if(pid!=null && ev.pointerId!==pid) return; var dx=Math.abs(ev.clientX-sx), dy=Math.abs(ev.clientY-sy); if (dx>=threshold || dy>=threshold){ active=false; try{ window.chrome.webview.postMessage('window-drag'); }catch(_){ } cleanup(); } }catch(_){ } }
 function onUp(){ cleanup(); }
 function cleanup(){ try{ active=false; document.removeEventListener('pointermove', onMove, true); document.removeEventListener('pointerup', onUp, true); document.removeEventListener('pointercancel', onUp, true); }catch(_){ } }
 document.addEventListener('pointermove', onMove, true);
 document.addEventListener('pointerup', onUp, true);
 document.addEventListener('pointercancel', onUp, true);
 }catch(_){ }
 }
 document.removeEventListener('pointerdown', onPointerDown, true);
 document.addEventListener('pointerdown', onPointerDown, true);
 }catch(_){ }
 }

 // Initialize
 function ensure(){ try{ createShadowControls(); positionControls(); setupDrag(); }catch(_){ } }
 if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', ensure, { once: true }); else ensure();

 // Observe and re-position
 var mo = new MutationObserver(function(){ positionControls(); });
 mo.observe(document.documentElement || document.body, { childList: true, subtree: true });
 window.addEventListener('resize', positionControls);
 window.addEventListener('scroll', positionControls, { passive: true });
 // Also re-run after history navigation events
 (function(){ var _push = history.pushState; history.pushState = function(){ var res = _push.apply(this, arguments); setTimeout(positionControls,100); return res; }; var _rep = history.replaceState; history.replaceState = function(){ var res = _rep.apply(this, arguments); setTimeout(positionControls,100); return res; }; window.addEventListener('popstate', function(){ setTimeout(positionControls,100); }); })();

 } catch(e) {}
})();