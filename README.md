# YouTube in WinUI3 🐢

A WinUI3-based YouTube theme that brings native-looking mica/acrylic surfaces and Fluent-style accents to YouTube's web app.

## Installation (recommended)
I'm planning on making an installable app but for now use any CSS injector extension.

## Notes & Limitations
- Not tested on light mode
- "Mica" and "accent" colors are hardcoded into the CSS theme but can be changed
- 90% of the code is ChatGPT, sorry
- The web app's (or PWA's) titlebar can't be modified so it won't really look like a native application. I'm going to resolve this issue by, well, making a native webview2 application. 

## Customization
If you want to change the mica or accent colors, edit the variables near the top of the CSS:
- Mica Tabbed
- Mica Standard
- Mica Alt
- Mica Card
- Accent Color

## Known Issues & Future work
- Pages other than the homepage and when a video is open have not been fully fixed
- The currently "finished" parts of the UI don't fully match WinUI guidelines
