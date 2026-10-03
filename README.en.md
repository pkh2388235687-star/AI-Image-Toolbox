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
| Dual image | Cover and hidden image in a PNG; per-item covers, batch operations, text, nine preset positions, free placement, numbering and hidden-image restoration on the same page |
| Image scramble | Local pixel permutation and inverse, batches, custom JPEG quality percentages and actual encoded size previews |
| File disguise | JPG covers for MP4/ZIP/RAR/7Z, batches, SHA-256 checked restoration; `_v2i` for videos and `_a2i` for archives |
| GIF | Frame ordering, delay, looping and local preview |
| Clean metadata | Remove WebUI/ComfyUI prompts and workflows from PNG/APNG, JPEG, WebP and GIF |
| Redact | Mosaic, blur, solid fill, rectangle and brush selections, undo and full-image export |
| Tag reader | Separate WebUI/ComfyUI positive/negative prompts and parameters, per-field copy and separate TXT exports; empty panels are skipped |
| Settings | Language, defaults, remembered destinations and creator credits |

Android includes a collapsible navigation drawer, three visible queue entries in about 29% of available screen height, single-line names with middle ellipsis, three-row scrolling position menus, remembered gallery/custom-folder destinations and system sharing. Painting is enabled only after the redaction page has stopped scrolling for two seconds. Windows provides adaptive previews, wheel zoom, middle-button panning and adjustable panel heights.

## Limitations

- Original-file scramble mode embeds the source bytes and can restore them exactly. Lossless PNG mode preserves pixels; compressed JPG is lossy and cannot restore the original file. Scrambling is not password encryption.
- Dual-image restoration recovers the hidden image, not necessarily the input file's original encoding and metadata.
- File disguise does not transcode videos. ZIP extension changes were checked with specific extractors; RAR/7Z detection depends on the extractor. App restoration verifies original bytes.
- Share and save original images to retain hidden data. Screenshots, recompression and platform processing may destroy it. Actual QQ friend-to-friend roundtrips remain unverified.
- Bounded previews and sequential batches help older devices, but original-size processing remains limited by device memory. Performance has not been verified on every 2015 phone.
- Processing runs locally. The Android app has no Internet permission, WebView or remote code loading.

## Repository

```text
src/                     Windows C# sources
android/src/             Android Java sources
android/res/             Android icon
android/tests/           Android tests and small fixture bundle
assets/                  Windows icon and English translations
tests/windows/           Windows automated tests
releases/Windows/         Latest EXE only
releases/Android/         Latest APK only
docs/                    Bilingual build and validation documentation
.github/                 Checks and issue templates
```

[Build](docs/BUILD.en.md) | [Validation](docs/VALIDATION.en.md) | [Checksums](docs/RELEASES.md) | [Contributing](CONTRIBUTING.md) | [Security](SECURITY.md)

Upload this directory as one repository root. SDKs, emulators, build caches, signing secrets, debug output and older packages are excluded. Automated test sources and small synthetic fixtures are retained for reproducible checks; release apps exclude self-test entry points and fixtures.

## License

Original code is [MIT licensed](LICENSE), copyright 光影若水. Preserve the Gilbert traversal algorithm's complete BSD-2-Clause notice in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Include both documents when redistributing.
