# Build guide

## Requirements

- .NET 10 SDK.
- Android SDK/JDK supported by the installed .NET Android workload.
- `maui-android` workload.
- Linux, Windows or macOS development environment supported by .NET MAUI tooling.

## Restore and test

Fetch the pinned offline Arabic/English OCR models first:

```bash
bash scripts/fetch-ocr-models.sh
```

Then restore/test/build:

```bash
dotnet workload install maui-android
dotnet restore tests/ImageForensics.Tests/ImageForensics.Tests.csproj
dotnet test tests/ImageForensics.Tests/ImageForensics.Tests.csproj -c Release
dotnet restore src/ImageForensics.App/ImageForensics.App.csproj
```

## Local unsigned/debug-oriented publish

```bash
dotnet publish src/ImageForensics.App/ImageForensics.App.csproj \
  -c Release \
  -f net10.0-android \
  -p:AndroidPackageFormat=apk \
  -o artifacts/android
```

For distribution, sign with a persistent Android signing key. Do not commit keystore files or passwords.

## CI beta

`.github/workflows/android-ci.yml`:
1. restores and runs unit tests;
2. builds Release Android;
3. creates a temporary beta signing key;
4. verifies the signed APK signature/alignment/archive;
5. writes SHA256SUMS;
6. uploads the artifact `image-forensics-android-apk`.

The beta key is intentionally ephemeral, so builds from different CI runs are not upgrade-compatible.

## Production

Use `.github/workflows/production-release.yml` after configuring the signing secrets described in `docs/RELEASE.md`. It is designed to emit APK + AAB + SHA-256 hashes.
