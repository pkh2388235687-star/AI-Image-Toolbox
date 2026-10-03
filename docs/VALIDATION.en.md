# Validation record

English | [简体中文](VALIDATION.md) | [Home](../README.en.md)

Repository preparation date: 2026-10-03. Creator: 光影若水 (Guangying Ruoshui).

## Current preparation and rebuild

- Repository directory renamed to `AI-Image-Toolbox`; application name remains `AI Image Editing Tools`. Public paths use ASCII English names.
- Windows production and test executables compile. Production excludes developer self-test entry points and synthetic image generation.
- Four independent Python suites pass: dual PNG/APNG, metadata cleanup/GIF/redaction core, WebUI/ComfyUI inspection, and scrambling/original-file restoration.
- Windows UI message-loop regression passes: navigation, cover pairing, text presets/free placement, merged restoration, export folders, brush apply/undo/reset, zoom/pan and live brush previews, file batches, separate TXT reports, bilingual switching and tooltips.
- C# file-container tests pass: unchanged inputs, exact SHA-256 restoration, collision protection, Chinese paths, tampering/truncation/offset/path-traversal rejection, disk-space checks, locked inputs, split-archive rejection and cancellation cleanup. An actual file beyond 2 GiB exercises structure/length handling, but copying is cancelled after the first MiB; this is not a full 2 GiB roundtrip.
- Production Windows was rebuilt from the renamed root. Production CLI checks confirm hidden-image pixels, original ZIP bytes and extraction through Python ZipFile after changing the extension.
- Android production and self-test APKs compile; official apksigner verifies v1/v2/v3 signatures, minimum API 21 and target API 35, with an English application label.
- Release APK ZIP integrity and assets are checked: `NOTICE.md` is the only asset, with no fixtures or developer self-test entry point. Signing secrets are excluded. Windows embeds MIT and third-party notices.
- Repository checks cover public filenames, secrets, relative Markdown links and exactly two release packages. GitHub CI is configured but has not run remotely.

## Earlier Android runtime validation

These records precede the same-day naming/license cleanup. Image-processing, queue-scrolling and redaction algorithms were unchanged by this rebuild. Full emulator runtime regression was not repeated after repository preparation.

- Android 5.0.2 and Android 15 emulators passed core/UI checks, including 29 metadata fixtures, desktop/mobile dual-image interchange, exact original hashes, a real 170 MiB ZIP streamed and cancelled, Chinese paths, duplicate names and GIF decoding.
- Twenty-four redaction combinations cover PNG/JPG, opaque/transparent inputs, circle/rectangle masks and mosaic/blur/fill. Outside-mask pixels are unchanged, full PNG saves match and undo restores input.
- Actual Activity touch dispatch confirms no painting while scrolling, renewed two-second delays after further scrolling, and new strokes only after the page is stationary.
- Queues occupy between one-quarter and one-third of usable height with three complete entries; internal scrolling and top/bottom handoff, including crossing a boundary within one gesture, pass. Three-row position menus and 800x360 landscape checks pass.
- First single-image import starts background actual-size encoding. Cached-page switching median was approximately 33 ms on emulators; this is not a claim for every old phone.
- Legacy gallery, MediaStore, system custom-folder grants/persistence and an independent share-receiver byte check pass.
- ZIP extension changes were checked with 7-Zip 25.01 and WinRAR 5.91; explicit RAR5 recognition was checked. Android 5's built-in ZipFile rejects prefixed ZIPs. App restoration remains the recommended fallback.

## Not covered

Actual QQ friend-to-friend original-image roundtrips, all mobile extractors, 512 MB physical devices and every 2015 phone, full mobile roundtrips beyond 2 GiB, and runtime permissions on each Android 6-9 release remain unverified. Hidden data is not guaranteed to survive platform processing.
