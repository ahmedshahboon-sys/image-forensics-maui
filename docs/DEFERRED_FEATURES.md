# Deferred / limited features — 1.0.0

These are documented rather than simulated:

- **Physical Android device QA:** CI installs and launches on an emulator, not a physical handset. Real-device OCR/share/maps/HEIF/memory checks remain recommended.
- **Production signing key:** never stored in the repository. The Production Release workflow requires owner-supplied GitHub Secrets.
- **Camera capture:** optional in the master prompt; omitted to avoid CAMERA permission.
- **Face detection/count/privacy blur:** allowed by policy but not shipped because no additional validated local model was adopted.
- **Face recognition / identity:** intentionally not implemented.
- **Object/logo/license-plate recognition:** optional and not shipped without a vetted offline model.
- **EXIF thumbnail visual comparison:** thumbnail metadata is detected, but raw thumbnail bytes are not surfaced by the current safe metadata layer.
- **RAW/HEIF deep decode:** best-effort platform/library dependent.
- **Windows UI/build:** architecture remains portable, but 1.0.0 ships Android.
- **Automatic VirusTotal/file reputation upload:** not implemented; the app does not silently upload images/files.
