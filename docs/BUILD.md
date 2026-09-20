# Build guide — 1.0.0

## Requirements

- .NET 10 SDK.
- Android SDK/JDK supported by the .NET Android workload.
- Bash + curl + python3 for pinned model/font fetch scripts.
- `maui-android` workload.

## Prepare pinned assets

```bash
bash scripts/fetch-ocr-models.sh
bash scripts/fetch-report-font.sh
bash scripts/security-audit.sh
```

Both fetch scripts pin upstream commits and verify expected Git blob SHA values.

## Restore and test

```bash
dotnet workload install maui-android

dotnet restore tests/ImageForensics.Tests/ImageForensics.Tests.csproj
dotnet test tests/ImageForensics.Tests/ImageForensics.Tests.csproj \
  -c Release \
  --no-restore

dotnet restore src/ImageForensics.App/ImageForensics.App.csproj
```

## Build APK

```bash
dotnet publish src/ImageForensics.App/ImageForensics.App.csproj \
  -c Release \
  -f net10.0-android \
  -p:AndroidPackageFormat=apk \
  -o artifacts/android
```

A distributable build must be signed. Never commit a persistent keystore/password.

## CI release-candidate APK

`.github/workflows/android-ci.yml`:
1. installs .NET 10 + MAUI Android;
2. fetches/verifies OCR models and Arabic report font;
3. runs least-permission security audit;
4. runs tests including generated forensic corpus and performance budgets;
5. restores/publishes Release APK;
6. signs it with an ephemeral CI beta key;
7. verifies signature, zipalign and ZIP integrity;
8. installs and launches it on Android Emulator;
9. emits SHA-256 and uploads the artifact.

The ephemeral beta key changes across CI runs, so uninstall an older beta before installing a build from a different run.

## Production APK + AAB

Configure the signing secrets listed in `docs/RELEASE.md`, then manually run **Production Release**. That workflow builds signed APK + AAB, verifies the packages, and creates `SHA256SUMS.txt`.
