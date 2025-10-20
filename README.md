```markdown
# WinUI3 YouTube Theme (OneGunBullet)

A WinUI3-inspired YouTube theme that brings native-looking mica/acrylic surfaces and Fluent-style accents to YouTube's web app.

Colors (hardcoded per request):
- Tabbed mica: #10002e
- Regular mica: #241a34
- Mica alt: #2a2336
- Accent: #d88de1

## Files
- winui3-youtube.theme.user.css — the main userstyle CSS file you can install with Stylus or other userstyle managers.

## Installation (recommended)
1. Install a userstyle manager:
   - Stylus (Chrome/Edge/Firefox): https://add0n.com/stylus.html
   - Stylish or other managers may also work, but Stylus is recommended.

2. Create a new style and paste the contents of `winui3-youtube.theme.user.css` into it, or import the file if your manager supports direct file import.

3. Ensure the style matches `https://www.youtube.com/*` and enable it.

Optional: Use a userscript manager (Tampermonkey) to inject the CSS if needed.

## Notes & Limitations
- YouTube's frontend DOM and class names change frequently. This style targets many high-level elements (ytd-app, #masthead-container, #content, guide renderer, video renderer, etc.). Some pages or experimental/updated flows may require selector updates.
- For maximum compatibility, the stylesheet uses broad selectors and !important flags. That helps keep the theme working across YouTube updates but could occasionally clash with new UI components.
- The theme uses `backdrop-filter` for blur effects — this is widely supported in modern browsers but may not work in some older browsers or specific configurations.
- The theme favors a dark, WinUI-like aesthetic. If you encounter contrast issues, you can tweak the variables at the top of the CSS (`--muted-text`, `--text`, etc.).

## Customization
If you want to change the mica or accent colors, edit the variables near the top of the CSS:
```css
:root{
  --winui-mica-tabbed: #10002e;
  --winui-mica: #241a34;
  --winui-mica-alt: #2a2336;
  --winui-accent: #d88de1;
}
```

## Known Issues & Future work
- Some channel / community pages and experimental YouTube layouts may show elements that don't match perfectly — these require additional selectors.
- A future iteration could add:
  - A small JS toggler to switch between mica variants or lighten/darken for accessibility.
  - Support for light mode conversion (this release is dark-only per WinUI typical dark glass).
  - Packaging as a Chrome/Edge extension for distribution.

If you'd like, I can:
- Package this into a distributable browser extension (manifest + CSS/JS).
- Add a small toggler UI (JS) to switch between "Tabbed Mica", "Mica", and "Mica Alt" at runtime.
- Iterate on specific YouTube pages that you care about (e.g., Channel pages, Shorts, Studio, etc.)

Enjoy the native-feeling YouTube UI!
```