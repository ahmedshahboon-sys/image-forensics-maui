# Android security baseline

- No INTERNET permission is declared for the offline baseline.
- No location permission is requested to read EXIF GPS.
- The launcher activity accepts Android ACTION_SEND for image/* only.
- Shared content is copied into app-private cache with a 512 MiB cap.
- Pending shared cache files are replaced/deleted rather than accumulated.
- Android backup is disabled to reduce accidental extraction of local app data through device backup.
- Cleartext traffic is disabled.
- The application does not auto-open QR links or execute embedded files.
