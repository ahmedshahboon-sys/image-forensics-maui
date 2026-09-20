# Release guide — 1.0.0

## Version

- Display version: **1.0.0**
- Android version code: **100**
- Package ID: `com.shahboun.imageforensics`

## CI release candidate

Every push to `main` runs the full release-candidate gate:
- security audit;
- tests;
- Release APK build;
- ephemeral beta signing;
- APK signature verification;
- zipalign verification;
- ZIP integrity;
- Android Emulator install + launch smoke test;
- SHA-256 generation;
- artifact upload.

The CI key is intentionally ephemeral and is **not** the production signing identity.

## Production signing

Keep the permanent Android signing key outside source control. Configure repository secrets:

- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_ALIAS`
- `ANDROID_KEY_PASSWORD`

Then manually run **Production Release**.

It creates:
- signed APK;
- signed AAB;
- `SHA256SUMS.txt`.

The workflow verifies APK signature/alignment/archive and verifies the AAB signature before artifact upload.

Back up the production signing key offline. Losing it can prevent normal upgrade continuity.

## Physical-device QA before broad public rollout

CI proves the package builds, signs, installs and launches on an Android Emulator. A representative physical-device pass is still recommended for:
- Gallery/File Picker and Android share-in;
- native Arabic/English OCR accuracy;
- Share Sheet;
- Maps intent;
- Light/Dark + RTL rendering;
- low-memory/older devices;
- HEIF/HEIC support on target devices.

Do not claim those physical-device behaviors were tested unless an actual device pass was recorded.
