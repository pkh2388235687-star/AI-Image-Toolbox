# AI Image Toolbox

English | [简体中文](README.md)

Repository: `AI-Image-Toolbox`. Application name: `AI Image Editing Tools`.

A local image toolbox by **光影若水 (Guangying Ruoshui)**, with native Windows and Android clients and Chinese/English interfaces.

## Download

| Platform | Latest package | Requirements |
| --- | --- | --- |
| Windows | [AI-Image-Editing-Tools.exe](releases/Windows/AI-Image-Editing-Tools.exe) | Windows 10/11, .NET Framework 4.8 |
| Android | [AI-Image-Editing-Tools.apk](releases/Android/AI-Image-Editing-Tools.apk) | Android 5.0 / API 21 or later |

Run the EXE directly, or install the APK after allowing installation from your download app. Both application names are **AI Image Editing Tools**. Change the interface language in Settings.

## Features

| Page | Capabilities |
| --- | --- |
| Dual image | Animated PNG with a cover and hidden image; per-item covers, text, positioning, numbering and batch export; same-page restoration of older dual files |
| Background reveal | Independent color/grayscale transparent PNG generation, no added text or numbering; white/black appearance previews, batch export and appearance extraction |
| Image scramble | Local pixel permutation and inverse, batches, custom JPEG quality percentages and actual encoded size previews |
| File disguise | Four methods for MP4/ZIP/RAR/7Z: copy /b-equivalent JPG concatenation, existing JPG container, PNG embedded ZIP and PNG IDAT embedded ZIP; batch covers, text and numbering, SHA-256 checked restoration; `_v2i` for videos and `_a2i` for archives |
| GIF | Frame ordering, delay, looping and local preview |
| Clean metadata | Remove WebUI/ComfyUI prompts and workflows from PNG/APNG, JPEG, WebP and GIF |
| Redact | Mosaic, blur, solid fill, rectangle and brush selections, up to 50 undo/redo states and full-image export |
| Tag reader | Separate WebUI/ComfyUI positive/negative prompts and parameters, per-field copy and separate TXT exports; empty panels are skipped |
| QR codes | Website QR generation, image overlays, adjustable border, camera/image scan, copying results and browser launch |
| Settings | Language, defaults, remembered destinations and creator credits |

Android includes a collapsible navigation drawer, three visible queue entries in about 29% of available screen height, single-line names with middle ellipsis, three-row scrolling position menus, remembered gallery/custom-folder destinations and system sharing. Painting is enabled only after the redaction page has stopped scrolling for two seconds. Windows provides adaptive previews, wheel zoom, middle-button panning and adjustable panel heights.

## Limitations

- Original-file scramble mode embeds the source bytes and can restore them exactly. Lossless PNG mode preserves pixels; compressed JPG is lossy and cannot restore the original file. Scrambling is not password encryption.
- Dual-image restoration recovers the hidden image, not necessarily the input file's original encoding and metadata.
- File disguise does not transcode videos and defaults to the existing JPG method. Renamed JPG archive detection depends on the extractor; simply renaming a video container to MP4 is not guaranteed to make it playable. Rename either PNG method to `.zip` and extract the complete original MP4, ZIP, RAR or 7Z, or restore it in the app. Both PNG methods are experimental; see the [tested scope](docs/VALIDATION.en.md). IDAT contains additional bytes after the image stream, so decoder compatibility is not universal and re-encoding may discard the data.
- Share and save original images to retain hidden data. Screenshots, recompression and platform processing may destroy it. Actual QQ group roundtrips for the new methods remain unverified; no fix for group-member visibility is claimed.
- Bounded previews and sequential batches help older devices, but original-size processing remains limited by device memory. Performance has not been verified on every 2015 phone.
- Processing runs locally. The Android app has no Internet permission, WebView or remote code loading.

## Repository

```text
src/                     Windows C# sources
android/src/             Android Java sources
android/res/             Android icon
android/tests/           Android tests and small fixture bundle
vendor/zxing/            Pinned QR libraries and licenses
assets/                  Windows icon and English translations
tests/windows/           Windows automated tests
releases/Windows/         Latest EXE only
releases/Android/         Latest APK only
docs/                    Bilingual build and validation documentation
.github/                 Checks and issue templates
```

[Build](docs/BUILD.en.md) | [Validation](docs/VALIDATION.en.md) | [Checksums](docs/RELEASES.md) | [Contributing](CONTRIBUTING.md) | [Security](SECURITY.md)

Upload this directory as one repository root. SDKs, emulators, build caches, signing secrets, debug output and older packages are excluded. Automated test sources and small synthetic fixtures are retained for reproducible checks; release apps exclude self-test entry points and fixtures.

## Dual image

Combines a cover and hidden image into an animated PNG. Existing cover text, positioning, numbering and batch operations are retained. Restoration recognizes animated and older transparent PNGs automatically. Platform re-encoding or removal of animation may leave only the cover; switching is not guaranteed after uploading.

## QR generation and scanning

QR codes appears immediately before Settings. Enter a full HTTP/HTTPS URL, such as a Pixiv artwork page. The app neither uploads images nor creates hosting addresses. The QR contains a URL; phone scanning opens the website. Login, image display and downloads depend on that website.

The URL field wraps long addresses and includes Paste URL. Choose QR-only output or an image overlay. Exports automatically use the “二维码制作” subfolder under the selected output root. Overlay QR placement supports dragging and XY percentages. Adjust the border from 0 to 12 modules; at least 4 is recommended. Smaller borders can prevent phone-camera scanning. Image overlays, Dual image and File disguise support up to 32 independently positioned, colored and removable text layers.

Import a clear image or use a camera. Results show the domain and complete content before an explicit browser-open action; Copy scan result copies all content. Only HTTP/HTTPS URLs enable opening. Android requests optional camera permission when scanning; import works without it. Windows uses the system Media Foundation reader and negotiates advertised sizes for RGB capture. Ordinary color cameras are preferred; device selection, reconnect and camera privacy settings are available. Errors distinguish permissions, missing media components, formats and device failures. Android prefers the rear camera, configures advertised sizes and NV21/YV12 layouts, handles orientation/focus, and releases capture when closed. Import remains available; actual hardware support depends on the OS and driver.

Transparent PNG imports are scanned against white and black automatically; no manual background conversion is needed. Background reveal → Color overlay → Processing → QR optimization uses app-generated QR PNGs with intact placement metadata. This option uses a grayscale cover and calibrates the whole image and code area to near-white level 250, reducing code remnants on common pale chat backgrounds while keeping scannable contrast on black. Zero interference and scan success after arbitrary backgrounds, transcoding or compression are not guaranteed. Other processing methods no longer force a visible QR overlay. Export checks the code before saving and rejects insufficient resolution. Metadata cleaning, platform resizing, re-encoding or flattening onto white can destroy this effect or the code; scanning cannot recover information that was removed.

Redaction stores lossless PNG history locally, with up to 50 states and a 256 MiB budget; large images can reduce the retained count. New edits clear redo. Buffered page painting and coalesced/cached previews reduce flicker. Image computations remain on the CPU; Android UI uses system hardware acceleration. No particular GPU vendor is required.

Layer menus show current text live, shortened after 18 text elements. Continuous drag updates are coalesced, with the final position committed on release. On Windows, the wheel over a numeric input or its arrow area directly adjusts the value by the configured increment, without modifier keys or simultaneous page scrolling. Controls are vertically centered within each row.

## Background reveal

Source colors limits chroma locally using contrast and regional colors, instead of letting a few saturated areas desaturate the whole image. Regional color sampling uses a 24-pixel longest edge; the contrast/strength-dependent chroma budget ranges from 16 to 48 code levels (zero strength remains grayscale). Effective color strength is measured from the composed result and may be lower than the requested setting. Custom effective strength and Restore automatic appear only for Source colors. A manual value overrides the automatic chroma budget but remains gamut-limited; higher values can increase cover leakage. Restore automatic reanalyzes the image. Automatic covers add independent petals/ribbons and adapt soft woven decorations to whole-image color richness, without following source contours or painting the decorations into the black view; custom covers are not replaced with decorative patterns. Less leakage comes with paler black-view colors and changed fine color boundaries. This is not encryption or reliable information hiding. Grayscale and other processing methods remain.

Windows and Android add an independent page directly below Double image. Import black-background images to use a text-free automatic cover fitted to each output. The Cover style dropdown offers a source-color pattern and a custom color cover image. Blue-violet, warm orange and teal presets appear only with Contour priority (shared tint). Automatic cover geometry uses soft gradients and a few flowing curves; the source-color method follows softened regional source colors. Select **Color overlay** or **Grayscale reveal** to export transparent PNGs. No text or numbering is added; existing text in source images is not removed. Appearance previews, refresh and fit controls are at the top. Explanations below dropdowns immediately follow the selection; source-color details and measured effective strength share one explanation below the processing option. Its height adapts to window width and stays stable during strength refreshes to avoid scroll jumps and excessive empty space. Menus close before opening the image picker; cancellation keeps the prior choice.

The default returns to **Source colors (softened)**. It retains regional source hues and fine luminance detail, while softening fine chroma and reducing saturation where needed; the source is no longer given one global cover tint. Covers show soft multiple-color fields and flowing curves from those softened source colors. Fixed palette choices are hidden for this method; the actual appearance preview is authoritative. Color strength is adjustable, but complete original color/brightness and zero interference are not guaranteed. **Contour priority (shared tint)** retains the previous contour-suppression method. **Balanced color (previous)** and **Original priority (black)** remain selectable. QR optimization is a separate grayscale linkage option. Grayscale independently optimizes tones, generates only grayscale covers, and hides color-only settings. One RGB value and alpha cannot exactly represent arbitrary pairs of independent full-color images. This is a visual effect, not password encryption.

The canvas follows each black-background image. Custom covers fit proportionally; automatic covers are generated at the matching aspect ratio without letterboxing. Export defaults to a 1200-pixel longest edge, with original-size limits determined by available memory. Keep transparency. JPEG conversion, screenshots, tinted/checkered backgrounds, resizing, compositing or platform processing may change the effect. QQ/Bilibili round-trip compatibility is not established. The software's white/black previews do not prove identical rendering in third-party viewers.

Extract white and black appearances exports the adjusted display images, not original bytes, metadata, brightness or complete original colors. Existing Double image restoration also recognizes these PNGs. All generation/extraction runs locally without uploading images or needing a network.

## License

Original code is [MIT licensed](LICENSE), copyright 光影若水. Preserve the Gilbert traversal algorithm's complete BSD-2-Clause notice in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). QR code dependencies ZXing and ZXing.Net use Apache-2.0. Their full license is bundled with the apps and repository. Preserve all applicable notices when redistributing.
