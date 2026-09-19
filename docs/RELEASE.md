# Release guide

## CI beta APK
Every push to main runs tests and builds an installable Release APK. The beta APK is signed with an ephemeral CI key. Because the key changes between runs, uninstall a previous CI beta before installing a newer one.

## Production signing
Never commit the production signing key. Add these GitHub repository secrets:
- ANDROID_KEYSTORE_BASE64
- ANDROID_KEYSTORE_PASSWORD
- ANDROID_KEY_ALIAS
- ANDROID_KEY_PASSWORD

Then manually run the **Production Release** workflow. It emits signed APK + AAB + SHA256SUMS.

Keep the original production signing key backed up offline. Losing it can prevent normal app upgrades.
