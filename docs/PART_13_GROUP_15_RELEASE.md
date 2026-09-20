# Part 13 — Group 15: Release

Release version: **1.0.0**  
Android version code: **100**  
Package: `com.shahboun.imageforensics`

Final release gate includes:
- restore;
- security audit;
- automated corpus, security, reporting and performance tests;
- Release APK publish;
- signed APK verification;
- zipalign verification;
- ZIP integrity;
- Android Emulator install/open smoke;
- SHA-256;
- artifact upload.

Production workflow additionally emits APK + AAB with owner-provided persistent signing secrets and verifies both package types.

The source repository contains build/release/security/test/feature/license documentation.

Important boundary: an emulator install/open is an actual Android package smoke test, but it is not a physical-device QA pass. Native OCR accuracy and vendor/device-specific integration should be spot-checked on representative hardware before broad public distribution.
