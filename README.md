# Jai — Thai-market concept prototype
Mobile-first, Thai/English 3D fictional temple garden with a customizable ordinary visitor avatar, walkable pavilion interior, optional one-minute quiet timer and on-device journal. No actual temple affiliation, religious merit scores, donations, commercial payments or health claims. Thai text and cultural imagery are drafts awaiting Thai-speaking and religious/cultural review.

## Run
Static files live in docs/. Run `python3 -m http.server 3000 --directory docs` from the checkout. GitHub Pages uses main:/docs. No dependencies or backend. Only name, shirt color, language and completed timer count are stored in browser localStorage; clear-data control resets them. No accounts, real location or server-side collection. No analytics are collected.

## Pilot
First validate the idea with Thai users and cultural reviewers: understand the fictional setting, comfort with imagery, interest in returning, and desire to customize. These are proposed research questions, not measured demand. No monetization is enabled. Consider optional avatar cosmetics only after local review; do not sell blessings or spiritual outcomes. Native iOS/Android builds, real temple partnerships and app-store distribution are outside this prototype.

## 3D controls and implementation
Hold on-screen arrows on touch devices, or use WASD / arrow keys. Walk forward along the central path and through the door to enter. Roof and front facade hide inside for a dollhouse view; side/rear/doorway collisions remain active. The reset arrow returns the visitor to the garden. Avatar shirt color changes are reflected in 3D. Desktop and mobile use the same scene. Modern WebGL2 browser required; a visible error appears if renderer creation fails. No external models or remote asset requests are used.

Vendored Three.js 0.180.0 from the official npm package: `docs/vendor/three.module.min.js`, `three.core.min.js`; MIT license in `THREE-LICENSE.txt`. Dependency was installed via npm with standard integrity verification. Initial download is larger than the previous 2D version. Rendering caps device pixel ratio at 1.7 and uses low-poly geometry.

Validated in headless Chromium at 390×844 with software WebGL: canvas rendering, keyboard doorway entry, on-screen forward control entry, location change, reset to garden, language change, no horizontal overflow or JavaScript errors. Physical iOS/Android GPU performance and cultural review remain unverified.
