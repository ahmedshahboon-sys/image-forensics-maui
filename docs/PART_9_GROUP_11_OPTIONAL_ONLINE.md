# Part 9 — Group 11: Optional Online Features

Online Mode is disabled by default.

Implemented external-service actions:
- Reverse-image-search entry: opens TinEye in the user's external browser. The app itself sends no image bytes; the user must choose/upload an image manually on the external site.
- SHA-256 reputation entry: opens VirusTotal search in the user's external browser and places only the already-computed SHA-256 value in the search URL.

Controls:
- a visible Online Mode toggle;
- a separate confirmation dialog every time an external service is opened;
- disclosure text states exactly whether an image or only a hash is transferred;
- no background request and no automatic image upload.

Android permissions:
- the app still declares no INTERNET permission;
- network activity is delegated to the user's external browser;
- no Location permission is added.

Deferred:
- in-app signature/rule database updates are not enabled because the offline APK intentionally lacks INTERNET permission. A future online-enabled build can add them with an explicit update source, integrity verification and consent policy.
