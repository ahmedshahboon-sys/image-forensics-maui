# Image Forensics / Photo Inspector

تطبيق C# / .NET MAUI لتحليل الصور والملفات الرسومية محليًا قدر الإمكان، مع Android كمنصة أساسية وبنية قابلة للتوسع إلى Windows لاحقًا.

> التطبيق يفصل بين الحقائق التقنية والقرائن الاحتمالية. لا يصدر حكمًا قطعيًا بأن الصورة أصلية أو مزورة، ولا يحدد هوية أشخاص من الوجوه، ولا يستنتج عنوانًا دقيقًا من مظهر الصورة عند غياب GPS/بيانات صريحة.

## الإصدار

**1.0.0 — Android / .NET 10**

Package ID: `com.shahboun.imageforensics`

## أهم الميزات

- Quick Scan وDeep Scan مع Progress/Cancel وtimeouts.
- File signature / MIME / extension mismatch.
- SHA-256 / SHA-1 / MD5 للمقارنة / CRC32 / entropy.
- Dimensions, orientation, alpha, frames, bit depth, DPI/palette حسب الصيغة.
- EXIF / GPS / IPTC / XMP / ICC / JFIF وحقول metadata المتاحة.
- JPEG/PNG/WebP/GIF container inspection وtrailing-data indicators.
- aHash / dHash / pHash.
- JPEG quantization/quality/chroma وblock/grid/noise/histogram/ELA/resampling/copy-move heuristics.
- LSB / bit planes / RGB channels / entropy map / embedded signatures / printable strings.
- OCR عربي + إنجليزي Offline عبر Tesseract.
- QR / Barcode Offline، والرابط يظهر كنص ولا يفتح تلقائيًا.
- Privacy Risk Report وMetadata Cleaner مع إعادة فحص النسخة النظيفة وعدم لمس الأصل.
- Comparison Lab: hashes, pixels, metadata/ICC diff, crop/resize heuristics, overlay, heatmap.
- Batch حتى 50 ملفًا مع CSV/JSON وفلاتر GPS/privacy/duplicates/errors.
- تقارير TXT / JSON / PDF عربي Unicode + SHA-256 manifest.
- واجهة RTL عربية، Light/Dark، result sections، metadata search/copy/explain، zoom، histogram.
- History محلي مختصر + Privacy Mode يمنع حفظ السجل.
- Optional Online Mode متوقف افتراضيًا؛ الميزات الخارجية تحتاج اختيار المستخدم وموافقته.
- Android Share-to-App / Share-out وفتح GPS الصريح في الخرائط.
- Least-permission security audit في CI.
- bounded preview decoding للمهمات التي لا تحتاج البكسلات الأصلية وsession-only scan cache.

## الخصوصية والأمان

- Offline-first.
- لا Location permission لقراءة GPS المخزن في EXIF.
- لا رفع صور تلقائيًا.
- لا فتح QR تلقائيًا.
- لا تنفيذ payload أو ملفات مضمنة.
- File-size / decoded-pixel caps وtimeouts.
- temp files داخل مساحة التطبيق مع تنظيفها.
- logs المحلية تحجب OCR/GPS/paths/image payloads.
- Privacy Mode يمنع حفظ سجل الفحص الجديد.

## البناء

راجع:
- `docs/BUILD.md`
- `docs/RELEASE.md`
- `THIRD_PARTY_NOTICES.md`

التحقق الأساسي:

```bash
bash scripts/fetch-ocr-models.sh
bash scripts/fetch-report-font.sh
bash scripts/security-audit.sh

dotnet workload install maui-android
dotnet restore tests/ImageForensics.Tests/ImageForensics.Tests.csproj
dotnet test tests/ImageForensics.Tests/ImageForensics.Tests.csproj -c Release --no-restore
dotnet restore src/ImageForensics.App/ImageForensics.App.csproj
```

## حدود موثقة

- نتائج ELA/JPEG/noise/resampling/copy-move/steganography قرائن احتمالية وليست دليلًا نهائيًا.
- RAW/HEIF depth يعتمد على دعم codec/platform.
- Camera capture اختياري ولم يُضف للحفاظ على least permissions.
- Face recognition وتحديد الهوية غير موجودين عمدًا.
- التعرف العام على الشعارات/اللوحات/الكائنات غير مضمّن لأن نموذج Offline موثوق لم يُعتمد.
- EXIF thumbnail preview/comparison محدود لأن طبقة metadata الحالية لا تعرض bytes المصغرة مباشرة.
- CI يبني ويثبت ويفتح الـAPK على Android Emulator؛ اختبار هاتف مادي وتقييم دقة OCR العربي بصريًا يظل QA ميدانيًا قبل نشر واسع.
- Production signing يحتاج keystore دائم يُحفظ خارج المستودع ويُمرر عبر GitHub Secrets.
