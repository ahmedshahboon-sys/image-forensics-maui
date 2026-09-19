# Image Forensics / Photo Inspector

تطبيق C# / .NET MAUI لتحليل الصور والملفات الرسومية محليًا قدر الإمكان، مع Android كمنصة أساسية وبنية قابلة للتوسع إلى Windows لاحقًا.

> **مهم:** التطبيق يعرض حقائق تقنية وقرائن احتمالية. لا يصدر حكمًا قطعيًا بأن الصورة أصلية أو مزورة، ولا يحدد هوية الأشخاص من الوجوه، ولا يستنتج عنوانًا دقيقًا من الصورة إذا لم توجد بيانات GPS صريحة.

## الإصدار الحالي

**0.9.0 Beta** — Android / .NET 10.

البناء المستمر يشغّل الاختبارات ثم ينتج APK موقّعًا بمفتاح Beta مؤقت. إصدار Production الدائم يحتاج مفتاح توقيع Android خاصًا بصاحب المشروع.

## ما يعمل حاليًا

- فحص سريع وعميق.
- File signature / MIME / extension mismatch.
- SHA-256 / SHA-1 / MD5 وCRC32 helper وEntropy.
- الأبعاد، الاتجاه، نوع اللون، Alpha، عدد الإطارات.
- aHash / dHash / pHash ومقارنة صورتين.
- قراءة EXIF / GPS / IPTC / XMP / ICC والحقول المتاحة.
- تحليل بنية JPEG وPNG وبيانات ما بعد EOI/IEND.
- Privacy Risk Report.
- Metadata Cleaner ينشئ نسخة جديدة ولا يلمس الأصل.
- QR / Barcode Offline.
- Hidden-data signatures وprintable strings بدون تنفيذ أو فك payload.
- ELA مساعد، RGB channels، bit planes، block/noise/copy-move heuristics.
- Batch حتى 50 ملفًا وتصدير CSV/JSON مع اكتشاف النسخ المطابقة والمتشابهة احتماليًا.
- تقارير JSON / TXT / PDF منظمة إلى أقسام جنائية واضحة، مع PDF Unicode يدعم تشكيل العربية.
- Share-to-App وShare-out على Android.
- فتح GPS الصريح في تطبيق الخرائط.
- RTL عربي، Light/Dark، Progress وCancel.

## الخصوصية والأمان

- Offline-first.
- لا توجد صلاحية Internet في النسخة الأساسية.
- لا توجد صلاحية Location لقراءة GPS المخزن داخل EXIF.
- لا يتم فتح روابط QR تلقائيًا.
- لا يتم تشغيل أي ملف أو payload مضمن.
- حدود حجم وذاكرة ومعالجة لتقليل مخاطر OOM والملفات الخبيثة.
- الملفات الأصلية لا تعدّلها أداة التنظيف.

## البناء

راجع `docs/BUILD.md` و`docs/RELEASE.md`.

الاختبارات:
```bash
dotnet restore tests/ImageForensics.Tests/ImageForensics.Tests.csproj
dotnet test tests/ImageForensics.Tests/ImageForensics.Tests.csproj -c Release
```

بناء Android:
```bash
dotnet workload install maui-android
dotnet restore src/ImageForensics.App/ImageForensics.App.csproj
dotnet publish src/ImageForensics.App/ImageForensics.App.csproj -c Release -f net10.0-android
```

## حدود معروفة

- OCR العربي Offline مؤجل لحين اعتماد محرك/نموذج موثوق قابل للتوزيع محليًا.
- عمق RAW/HEIF يعتمد على دعم النظام والمكتبات.
- ELA وJPEG quality/noise/copy-move وغيرها قرائن مساعدة وليست دليلًا نهائيًا.
- PDF الحالي يستخدم مسار نص محافظ؛ JSON/TXT هما المرجع الأفضل للنصوص Unicode الكاملة.
- لا يوجد تحقق فعلي من التثبيت والفتح على هاتف مادي داخل GitHub Actions؛ CI يتحقق من الاختبارات، البناء، توقيع APK، zip alignment وسلامة أرشيف APK.

## الوثائق

- `docs/ARCHITECTURE.md`
- `docs/SECURITY.md`
- `docs/FEATURE_MATRIX.md`
- `docs/BUILD.md`
- `docs/RELEASE.md`
- `docs/TEST_RESULTS.md`
- `THIRD_PARTY_NOTICES.md`
