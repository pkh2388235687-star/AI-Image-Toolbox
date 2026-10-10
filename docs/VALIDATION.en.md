# Validation record

English | [简体中文](VALIDATION.md) | [Home](../README.en.md)

Updated: 2026-10-10. Earlier repository preparation: 2026-10-03. Creator: 光影若水 (Guangying Ruoshui).

## 2026-10-10 Camera compatibility, custom strength and near-white QR optimization

This section records this update; subsequent sections describe earlier builds.

- Windows uses asynchronous system Media Foundation capture, advertised-size negotiation, ordinary color-camera preference, device selection, reconnect and privacy settings. Errors include a category and code. The ASUS FHD webcam was opened twice, delivering four 640×480 frames per cycle and reopening after cleanup. No camera images were saved. Infrared and other camera models were not hardware-tested.
- Android configures advertised sizes and handles NV21/YV12 luminance layout, orientation, focus, camera switching and cleanup. Host JVM checks cover padded/16-byte-aligned rows, truncated-input rejection and size ranking. This is not an Android camera device test.
- Source colors adds custom effective strength and Restore automatic, hidden for other modes. Complete bilingual Windows UI checks include manual values, restore, visibility and stable strength-refresh layout/scrolling. Automatic covers adapt independent woven ribbons to global chroma richness without tracing source outlines. Tests at 0/25/75/100 and with two different covers limit black-view changes to one level per channel; zero strength is grayscale, and PNG encoding preserves transparent pixels. This measures app compositing, not arbitrary third-party viewers.
- QR optimization calibrates the whole image and code area to background level 250, with black-view code targets of 16/112. All 768 pixel cases (256 cover levels × backgrounds 245/250/255) pass: ink/paper remnants differ by at most one level on 250 and three on 245/255. Pure-white differences from the prior calibration stay within six levels; this describes calibration changes rather than a universal remnant bound.
- Windows core/settings/batch and complete bilingual UI regression checks pass, including saved transparent PNG and black-view decoding. The host Android-core decoder verifies 20 PNG/background combinations, including six near-white covers that must not decode. All 320 source-color C#/JVM pixel vectors match.
- Android test/release sources compile. Release signatures retain the original certificate and v1/v2/v3 schemes. Android device bitmap/UI/camera permissions, other PC cameras, iOS and QQ/Bilibili round trips remain untested; host checks do not replace device checks. Platforms can still destroy the effect by rewriting alpha.
- EXE/APK packages are updated; see [RELEASES.md](RELEASES.md). App name/icon, Android package and permissions are unchanged; build/signing/verification outputs stay outside the public folder.

## 2026-10-10 Numeric wheel adjustment restored

This section records the earlier numeric-input fix.

- All Windows numeric inputs directly increment/decrement with an ordinary wheel, without Ctrl/Shift. Both the edit and arrow areas use the same handling, without also scrolling the outer page.
- Windows core and complete bilingual UI regression checks pass. Native window-message checks cover unfocused inputs, one adjustment per native-child event, bounds, accumulation of two partial wheel inputs, decimal increments and disabled inputs. Source-color explanation height and scroll position remain stable during value changes.
- Only the Windows EXE is rebuilt. The Android APK, image algorithms and other feature sources are unchanged. See [RELEASES.md](RELEASES.md) for package checksums.

## 2026-10-10 Scrolling, text layers and source-color retention

This section records the earlier UI/color update; its numeric-wheel behavior is superseded by the fix above.

- Windows core, settings, batch and complete bilingual UI checks pass at 900×700 and 1500×950. Source-color guidance and effective strength share one measured explanation. Repeated strength changes preserve actual page height and scroll position, with guidance visible. Wheel checks cover numeric inputs and their native edit children: ordinary scrolling moves the outer page without changing the value.
- Layer names follow current content, normalize whitespace, shorten long text and label empty text. Actual selector updates, add/delete/switch and caption checks pass. A burst of 500 drag events produces at most three position notifications and commits the correct release position. Windows retains the previous preview until its replacement; Android coalesces touch/preview work and updates existing text controls after dragging.
- Source-color optimization now limits chroma locally. Regional sampling increases from a 12- to a 24-pixel longest edge, and the nonzero-strength chroma ceiling rises from 20 to 48 code levels. Effective strength is measured from composed transparent pixels. In the same synthetic six-color sample, mean black-view RGB channel range rises from 13.886 to 40.737, about 2.93 times the earlier value. This is a sample-specific metric, not a universal improvement factor. Retaining more color can increase white-view leakage.
- Android test sources and the release APK compile. Host JVM and C# results match for 320 color-pixel vectors. Release v1/v2/v3 signatures retain the original certificate. Android native touch/layout behavior has not run on a device or emulator; QQ/Bilibili round trips and iOS rendering remain untested.
- Both release packages are rebuilt; see [RELEASES.md](RELEASES.md). Private test output and build artifacts stay outside the public tree. Application name, icons, Android package and permissions are unchanged.

## 2026-10-10 QR optimization, automatic black-view scanning and export fixes

This section records the earlier QR update; the current build also includes the UI and color-retention changes above.

- Windows core, settings, batch and full bilingual UI checks pass. QR exports create the “二维码制作” subfolder. URL paste/wrapping, 900×700/1500×950 layouts and exported-file import scanning pass.
- Imported PNGs retain transparency through the scan page and are tried on white and black automatically. The actual page decodes black-revealed codes without flattening away their alpha first.
- Background reveal returns to the Source colors default, removes the prior grayscale cover-only option and adds explicit QR optimization. Ordinary processing no longer forces a visible QR overlay. With automatic and custom covers, white-view differences before/after optimization stay within two grayscale code levels; white covers do not decode as QR, while black and saved transparent PNGs do. Actual page preview/export, missing-metadata errors and recovery to ordinary processing pass.
- Android test sources compile. Host Java using the Android ZXing core checks 16 Windows PNG/background cases, including two white covers where the code must remain hidden. These checks do not execute Android device bitmap/touch/camera behavior.
- Release EXE/APK builds, original APK certificate with v1/v2/v3 signing and repository checks pass. iOS, Android devices and QQ/Bilibili round trips remain untested. Platform removal of alpha or re-encoding can break the effect; removed information cannot be recovered by scanning.

Current package checksums: [RELEASES.md](RELEASES.md).

## 2026-10-09 QR codes, text layers, history and cover isolation

This records an earlier build; its QR overlay and default processing choice are superseded above.

- Windows core, batch and bilingual UI tests pass. URL validation, 90-degree rotation, 0/1/4/8/12-module borders, compact output, overlays, metadata CRC, white/black scans, independent text snapshots/settings, 45 lossless undo/redo steps and the 50-state cap pass. UI checks cover 900×700/1500×950 layouts, text selection/deletion/dragging and QR export followed by imported-image scanning.
- Linked QR areas are opaque at every pixel and decode on both appearance backgrounds. Cover isolation reuses grayscale optimization; 6,000 independent pixel vectors and actual encoded/decoded PNGs stay within one code level of the pure-white target cover. This does not promise zero ghosts on other backgrounds, color modes or after platform processing.
- Android test/release builds and original v1/v2/v3 signing pass. Camera is optional; no Internet permission is added. Host Java, using Android's ZXing core version, decodes 20 white/black Windows PNG cases; Windows ZXing.Net decodes the Java-generated QR. Complete Apache-2.0 licenses are bundled in both release packages.
- No Android device/emulator or iOS camera validation was available. New Android native bitmap/touch/camera/permission checks were compiled, not executed. Windows physical-camera capture and QQ/Bilibili platform round trips are also untested. Phone and website access compatibility needs practical testing.
- Windows uses child-subtree buffering, destination-first visibility and coalesced/cached previews. Android retains system UI hardware acceleration. No GPU image-compute backend is added; image calculations remain on the CPU. No cross-device hardware benchmark was performed.
- The public folder retains sources, pinned dependencies, necessary documentation and two release packages. Temporary APKs, screenshots/reports, SDKs and signing secrets are excluded.

See [RELEASES.md](RELEASES.md) for checksums.

## 2026-10-09 adaptive chroma limits and decorative covers

- The default Source colors method uses source luminance contrast and smoothed-region chroma statistics. A 99th percentile sets global strength while per-pixel limits contain outliers. The chroma budget adapts to contrast/strength, capped at 20 code levels. Colors are smoothed through a 12-pixel longest-edge intermediate; black-view luminance detail still comes from the source. No network service or model is used.
- Only automatic covers in color Source colors add independent petal/ribbon geometry to mask soft color fields without copying source contours. Custom covers are not replaced. Grayscale and other processing methods retain their algorithms; one local sample's grayscale PNG has the identical SHA-256 hash to the previous version.
- Windows core, batch and full bilingual UI checks pass. Actual white-view PNG channel range stays within ceil(budget)+1; six basic black-view hues remain distinguishable. 6,000 random capped-pixel checks, contrast adaptation, zero-strength grayscale, RGBA round trips, extraction, cancellation and previous algorithm regressions pass.
- On a private redacted sample, white PNG views resized to the same 400×600 dimensions show mean RGB channel range reduced from 13.286 to 5.789 (about 56%), maximum from 56 to 17, and the 99th percentile from 46 to 15. These are sample-specific chroma measurements, not secrecy or universal guarantees. Black-view colors also become paler while luminance detail remains. Private source/comparison files stay outside the public repository.
- Android test/release builds and original v1/v2/v3 signing pass. Host JVM matches 320 source-color vectors with varying limits, 640 previous adaptive vectors, 180 original and 360 original-priority vectors. Android bitmap/native UI/touch tests were not run on a device/emulator; QQ/Bilibili round trips were not executed.

Patterns and chroma limits reduce visual leakage; they are not encryption or reliable information hiding, and cannot guarantee ghost-free viewing. Saturation and fine color boundaries change. Regenerate from original sources; existing PNGs and appearance extraction remain compatible. Checksums: [RELEASES.md](RELEASES.md).

## 2026-10-09 dropdown explanations and image-picker popup fix

- Mode, output size, processing and cover style update explanations below the corresponding dropdown. Source-color results no longer sit above settings. Windows descriptions switch languages immediately and clear stale processing results.
- Fixed palettes appear only for color Contour priority (shared tint); other color methods offer automatic/custom covers. Stable semantic IDs keep an existing custom file when options are filtered. Leaving shared tint resets presets to automatic. Grayscale still hides color controls.
- Both platforms close menus before opening image pickers. Windows verifies that selection callbacks run after scrolling/ordinary menus close. Android closes the popup and restores the prior selection before launching the system picker, so cancellation does not commit an absent custom file.
- Windows core, full bilingual UI and batch checks pass, including 900×700/1500×950 layouts, immediate explanations, 2/5-choice filtering, custom-file preservation, grayscale visibility, actual export and collision protection. Bilingual screenshots were inspected. Composition and codec algorithm files have identical SHA-256 hashes to this update's baseline.
- Android test/release builds and v1/v2/v3 original-certificate verification pass. No device/emulator is available; Android native-picker/touch/UI runtime behavior was not exercised. QQ/Bilibili round trips were not executed. Existing rendering-compatibility limits remain.

Checksums: [RELEASES.md](RELEASES.md).

## 2026-10-09 source colors and separate color/grayscale covers

- Source colors is the default: retain source luminance detail and regional hues, soften fine chroma, and select exposure/strength from source brightness and color range. The source is no longer given one global cover tint. Covers show multiple-color fields and curves, constrained by source hues rather than arbitrary independent cover colors. Brightness, saturation and fine color edges still change; faint color regions may remain.
- The previous shared-tint method remains as Contour priority; previous balanced/original-priority methods remain. Grayscale always uses achromatic automatic processing, with no color-region filtering. Its actual cover contains no chroma and the page hides color processing, color strength and color-cover choices.
- Windows core, batch and full bilingual UI pass. Six basic regional hues survive actual PNG encoding/extraction; RGBA round trips, compatible extraction and cancellation pass. Changing cover palettes changes black appearances by at most one code level, and 6,000 fixed-profile independence pairs stay within 1.001 code value. Grayscale RGBA/covers are achromatic; 900×700/1500×950 layouts and hidden color-only controls pass. Previous 6,000 shared-tint, 16,000 grayscale, 500 independent color-reference and 4,000 priority checks remain.
- A private visual comparison from the author's locally redacted source image distinguishes red/gold/teal/yellow regions and shows faint color fields on white. No private image was committed or sent. This comparison does not establish zero interference across images.
- Android test/release builds and original certificate/v1/v2/v3 checks pass. Host JVM matches 320 source-color and 1,180 previous algorithm pixels. No device/emulator is available; new Android bitmap/touch/gallery/sharing and actual QQ/Bilibili round trips remain unexecuted. Names, icon and permissions are retained, without Internet permission.

Previews show the actual transparent composition, not an original-image overlay. A single ordinary transparent PNG cannot universally guarantee complete original colors, an independent multicolor cover and zero interference. This version prioritizes regional source hues and reduces fine chroma. Regenerate from originals. Hashes: [RELEASES.md](RELEASES.md).

## 2026-10-09 automatic optimization and color-flow covers

- Background reveal now defaults to adaptive tone distributions and a global chroma budget. Automatic color covers use a shared tint; conflicting custom colors can use it too, with an explicit notice that full original colors are not retained. Previous balanced/original-priority options remain. Other feature source code stays as it was before this update.
- The three-row scrolling Cover style dropdown offers source-based color, blue-violet, warm orange, teal and custom images. Covers use soft gradients and a few flowing curves, only global source color/brightness/dimensions, no source contours or added text. Neutral sources use blue-violet.
- Windows core, batch and full bilingual UI regressions pass, including four palettes, custom covers, top previews, 900×700/1500×950 layouts, actual exports/extraction and collisions. Across 6,000 fixed-profile independence checks, changing the other image changes each appearance by at most 1.51 code-value levels per channel. Flat-color covers from actual encoded/decoded PNGs vary by at most 2 levels. Source-dependent palette changes, genuine color covers, grayscale, RGBA round trips and cancellation are checked.
- Previous 16,000 grayscale, 500 independent color-reference and 4,000 original-priority checks plus other core tests pass. Previews and exports use the same composition method; bounded preview and other output resolutions can produce slightly different histogram analysis.
- Android test/release compilation, original v1/v2/v3 signatures and certificate checks pass. Host JVM matches 640 adaptive, 180 previous-method and 360 priority pixels. New bitmap tests compile but were not executed without a device/emulator. App/package names, icons and permissions stay unchanged; no Internet permission is added.

These checks cover standard pure-white/pure-black encoded-value compositing, not universal ghost-free viewers. Actual QQ maximum zoom, Bilibili round trips and low-end-device performance remain unverified. Regenerate from original sources; extraction returns adjusted appearances. Package hashes are in [RELEASES.md](RELEASES.md).

## 2026-10-09 top previews, automatic covers and original-priority color

- Both clients move Background reveal previews and refresh to the top; Windows retains Fit to window. Its preview layout explicitly rebuilds single/dual columns and rows, calculates height from cards, and removes unused rows when returning to a wide layout.
- Text-free gradient covers are generated at each black-image output size by default. Custom white-background images and returning to automatic covers are supported. No text/numbering is inserted. Existing Double image and other cover/text logic remain unchanged.
- The author-supplied local PNG passes structure/CRC checks and has color strength 70, black background 0. Its existing balanced algorithm already crosses colors under standard local white/black rendering; the native Windows thumbnail also has a mixed appearance. This does not establish QQ byte retention or its actual rendering pipeline.
- Adds Original priority (black) as the color default. The black target colors are independent of the cover; alpha compromises the white view. Balanced color (previous) remains selectable and grayscale is unchanged. Color may still affect the white cover; arbitrary full-color pairs cannot be separated exactly.
- Windows core, batch and full UI checks pass, including bilingual 900×700/1500×950 layouts, top previews, narrow/wide transitions without unused rows, settings screenshots, actual automatic-cover exports, collisions and previous features. Across 4,000 priority inputs, black target error is at most 0.501 code-value level and changing covers changes the black appearance by at most 1.001 level. Existing balanced/grayscale checks remain.
- Android test/release APKs compile, retain app/package names, icon and permissions, and verify the original certificate plus v1/v2/v3 signing. Host JVM/C# match exactly across 180 previous-method and 360 priority vectors including transparent inputs. New Android bitmap/touch/gallery/sharing tests were not executed on a device/emulator.

No images were automatically sent to QQ or other recipients. New QQ full-size/maximum-zoom and Bilibili round trips remain unverified. Other backgrounds, color management, resizing/compositing or platform processing may still mix views; this is not a universal viewer fix. Regenerate old exports from original sources to apply the new algorithm. Package hashes are in [RELEASES.md](RELEASES.md).

## 2026-10-09 independent Background reveal page

- Windows and Android add Background reveal directly below Double image, with Color overlay and Grayscale reveal. The new page supplies no default cover, text or numbering. Existing Double image text, numbering and queues remain; other existing feature logic is unchanged.
- Independently implements transparent compositing on pure white/black. Grayscale maps sources into light/dark ranges; color retains adjustable chroma and minimizes a convex quadratic error across at most three breakpoints, without per-pixel allocations or iterative search. Color is approximate and cannot exactly represent arbitrary pairs of full RGB images.
- Windows checks 16,000 random grayscale pairs within 1.01 code-value error per appearance and 500 color pairs against an independent exhaustive alpha reference. Genuine color output, zero-strength equivalence to grayscale, transparent inputs, letterboxing, RGBA PNG round trip, profile recognition, new/legacy extraction, collisions and cancellation pass. PNG contains no text or animation chunks.
- Windows core, batch, feature and full bilingual UI regressions pass. New-page layouts at 900×700/1500×950 are checked and rendered screenshots inspected. English output-size options display fully; actual batch/selected exports use the Background reveal feature folder and handle duplicate names. Existing animated dual images, dragged text, mixed legacy restoration and wheel-boundary handoff remain covered.
- Android test/release APKs compile. Host JVM and C# match byte-for-byte on 180 color/grayscale arithmetic vectors. This does not replace Android bitmap/touch/gallery/sharing tests; no device/emulator is available and the new runtime tests were not executed. Original certificate SHA-256 and v1/v2/v3 signatures verify; app/package names, icon and permissions remain, with no Internet permission added.
- Transparent PNGs store a standard black-background profile recognized by existing restoration on both clients. Extraction returns white/black appearances, not original bytes, metadata, brightness or complete original colors. Previews are bounded and asynchronous, stale results are discarded and batches run sequentially. Performance across all low-end devices was not measured.

Actual QQ/Bilibili upload, original-save and maximum-zoom testing for this update is pending. Text already in source images is not removed. Backgrounds, color management, resizing/compositing, screenshots and platform re-encoding may change results. Standard-background arithmetic tests do not establish universal ghost-free client rendering. Package hashes are in [RELEASES.md](RELEASES.md).

The following entries describe historical updates. The earlier removal record applies to that release; the latest release includes the independent page above.

## 2026-10-09 removal of background-generation modes

- Removes color/grayscale background generation, mode selectors, viewing-gray/color-strength controls and their encoders from Windows and Android. Animated dual PNG generation remains. Removed mode settings are no longer loaded or saved; unrelated settings are retained.
- Retains read-only extraction of older transparent dual files using their duAL display profile, or black for unprofiled transparent PNGs. Restoration automatically distinguishes animated and legacy transparent files. Legacy appearance extraction does not restore original-file bytes or repair existing ghosts.
- Before modification, the previous release EXE produced six 4×2 synthetic fixtures covering both legacy encoders and backgrounds 0/48/128, plus expected appearance views. Windows verifies old appearance pixels, CRC rejection, unprofiled files, collisions, cancellation, old-setting migration and animated PNG detection. A mixed restoration queue exports both formats correctly.
- Windows core, batch and full bilingual UI regressions pass, including 900×700 and 1500×950 layouts, absence of the two removed entries, retained queues/cover text and animated generation/restoration. The previous queue-wheel fix remains.
- Android test APK with the new compatibility cases compiles. No device/emulator is available here, so this update's Android bitmap/touch UI tests were not executed. The release retains the original certificate SHA-256 with v1/v2/v3 verification, app/package names, icon and permissions. Package hashes are recorded in [RELEASES.md](RELEASES.md).

The following entries are historical. They do not mean removed generation modes remain available or that social-platform re-encoding compatibility passed.

## 2026-10-09 Windows queue-wheel handoff and original-file comparison

- Fixes wheel messages consumed by `SoftList` and `SoftGrid` at boundaries. Empty/short queues and top/bottom boundaries hand the original wheel message to an outer page that can still scroll. Native list scrolling takes priority while items remain. Existing Ctrl/Shift handling is retained.
- Adds native-window-message regression for empty lists/grids in both directions, short queues, long-list internal scrolling, top/bottom handoff and preserved queue contents/list selection. Final core regression passes; batch and full bilingual UI regressions pass. The release Windows EXE was rebuilt from the same feature sources.
- One author-supplied QQ-saved original and its generated PNG are both 990,806 bytes with identical SHA-256. That transfer retained the original bytes; this does not establish retention for other images, clients or routes.
- Independent display experiments show no cover contribution from complementary pixels when composed per pixel on pure black. A gray background or resizing transparent pixels before linear-light compositing can reveal the cover again. A controlled two-pixel average gives 64 for both encoded-value views, but 137 versus 74 after resize-then-linear compositing, a 63-level difference. These are simulations; QQ's actual rendering pipeline was not measured.
- The author reports remaining text/landscape ghosts from the complementary grayscale candidate in actual viewers. White-background interference from color candidates also fails acceptance. Diagnostic samples/programs stay outside the repository and do not replace the release dual-image algorithms. Android sources and APK are unchanged in this update; the APK hash matches the previous release.

Actual-viewer compatibility of color/grayscale background modes remains unresolved. Correct simulated black/white views do not establish a QQ or desktop-viewer fix. Current package hashes are in [RELEASES.md](RELEASES.md).

## 2026-10-09 viewing-background adaptation and color restoration

- Restores Color background dual with adjustable 0–100% color strength, default 18%. Grayscale retains average tones. Both clients remember a viewing background from 0 to 128, defaulting to 0 on mobile and 48 on desktop. Previews and all exports use the same captured parameters.
- Author feedback: dark-gray 48 was least ghosted in the local viewer, but revealed text at maximum mobile QQ zoom after loading the original. Pure-black was better than the linear-compositing sample in the same QQ. This supports a background mismatch explanation; QQ internals were not measured and other effects are not ruled out. The color-tag experiment showed mixing/grain and was not adopted.
- Independent numeric comparison: changing black/white covers for one hidden image produces up to 24 gray levels of difference when a 48-adapted PNG is displayed on black. With matching backgrounds 0/48/128, differences stay within one quantization level. This is algorithm validation, not measurement of a QQ screenshot.
- New PNGs carry a four-byte CRC-checked private duAL display profile: version, mode, dark level and color strength. Both readers use it for extraction; older profile-free files use black 0. Corrupted profiles are rejected and existing animated restoration is retained.
- Windows core, batch and full bilingual UI regressions pass, including 900×700/1500×950 layouts, three-row mode selection, persistence, gray independence within one level, genuine non-gray color output, profile extraction, CRC rejection, collisions and cancellation.
- Android test/release APKs compile and retain the original certificate, v1/v2/v3 signatures, app name and permissions. Host Java and C# agree on 20,000 color/alpha inputs and Windows-generated 0/48/128 display profiles. New Android bitmap/UI tests were not executed on a device or emulator.

Color remains a compromise. Gray separation requires matching backgrounds and compositing. Quantization, color management, re-encoding and background changes can affect the result. Author feedback of least-visible text does not establish zero ghosts or universal QQ maximum-zoom compatibility. See [RELEASES.md](RELEASES.md) for package hashes.

The following are historical build records. The automatic-grayscale entry has been superseded by restored color. Earlier pure-black checks do not establish universal client compatibility.

## 2026-10-09 background cover-text ghosting fix

- Color-mode black targets depend only on the source image, without cover chroma. Grayscale quantizes target tones and alpha before inversion to remove cover-dependent rounding errors. Source colors affect the white-background color cover.
- New Windows regression uses contrasting covers with the same source. After PNG encoding/decoding, black-view pixels match exactly. Core, batch and full UI regressions passed, including bilingual and narrow-window mode checks.
- Before/after rendering of the author's landscape was visually checked: the pure-black color view no longer contains cover text, numbering or purple tint. Grayscale tones and existing extraction, cancellation and collision checks pass.
- Host C# and Android Java match byte-for-byte across 20,000 color/alpha inputs, with cover-invariance checks passing. Android release/test APKs compile; the original certificate and v1/v2/v3 signatures verify. New Android bitmap/PNG regressions are included but were not executed on a physical device or emulator.

Ghost elimination applies to pure black. Gray, translucent or patterned backgrounds can still reveal the cover through transparent pixels. This is not a claim of ghost-free output across all Bilibili clients. Regenerate older images from the original sources.

## 2026-10-09 dual-image modes

- Windows and Android now include Animated dual, Color background dual and Grayscale background dual. Source, release packages and bilingual documentation are synchronized. Existing animated behavior remains the default, and subsequent mode selections are remembered.
- Windows core and full message-loop UI regression passed: existing hidden-image restoration, the eight original pages, extraction of both background views, batch naming, collision protection, cancellation, flattened-image rejection and grayscale target tones.
- New Windows controls were checked in Chinese and English at 900×700 and 1500×950: layouts, three-row selection, preservation of queues/text, saved mode, generation and previews passed; rendered screenshots were inspected. Final selection handlers measured 26–383 ms on this machine. This is a sample measurement, not a guarantee for every device or a full-image export timing.
- C# and Android Java matched byte-for-byte across 20,000 color/alpha inputs. Android release and test APKs with new regression cases compiled. The new Android layout, bitmap encoding, gallery and sharing tests have not been executed on a physical device or emulator in this update.
- Android release v1/v2/v3 signatures verified against the existing certificate. App name, package, icon and permissions are retained; no Internet permission was added. Current sizes and SHA-256 hashes are in [RELEASES.md](RELEASES.md).

Background modes use transparency and the viewing background. Extraction exports white/black appearance images, not original-file bytes or lossless copies of both sources. The earlier algorithm excludes cover text from the pure-black view. Source colors affect the color-mode white cover, and both modes adjust brightness. Screenshots, JPEG, flattening or removal of transparency invalidate the effect. Local success does not establish universal platform compatibility.

## Latest-build synchronization: 2026-10-08

The packages are replaced at the author's request, including experimental PNG and IDAT methods. This is not a claim that QQ compatibility passed. New-method tests ran on 2026-10-06; on 2026-10-08 the upload directory was synchronized, checksums were checked, its Windows sources compiled, APK signatures verified, and repository structure/license/document-link checks passed.

- Both panels offer four methods, defaulting to existing JPG. Actual Windows controls were checked for Chinese/English, independent selections, page persistence, three-row scrolling, and 900x700/1500x950 layouts. The new Android interface compiles but was not checked on a physical device.
- MP4/ZIP/RAR/7Z passed exact SHA-256 restoration and cross-platform generation/restoration through Windows and Android core sources running on a host JVM. Host JVM tests are not Android device tests.
- Renaming either PNG method to `.zip` passed testing and extraction with WinRAR 5.91 and 7-Zip 25.01, preserving original names and hashes. Results apply to the tested versions and samples only.
- PNG structure, CRC, image-stream checks and Windows System.Drawing cover decoding pass. Eight IDAT covers from both platforms match pixel for pixel; batch text, numbering and `_v2i`/`_a2i` checks pass.
- A real 178,258,034-byte IDAT input produces 178,328,878 bytes. Windows and Android core on a 96 MiB JVM heap restore the original hash; 7-Zip testing passes.
- Truncation, CRC corruption, extra tails, bad hashes/unsafe names with correct CRCs, collision protection and cancellation cleanup pass. Disk-full behavior was simulated in the Python prototype; actual Windows/Android disks were not filled.
- Chinese names, source ZIP comments, encrypted archives and small forced-ZIP64 entries were wrapped and restored. This is not a complete large ZIP64 matrix. Inputs exceeding a supported single chunk are rejected explicitly.
- APK v1/v2/v3 signatures match the existing signer. App name, package, icon, API range and permissions are retained, as are the project button and plain URL. Package checksums are in [RELEASES.md](RELEASES.md).

IDAT stores additional bytes after the zlib image stream. The PNG standard discourages encoders from emitting such trailing data; this method is experimental, with no universal decoder guarantee. Re-encoding, screenshots or platform processing may destroy attached data. QQ group visibility/roundtrips and physical Android gallery/sharing for the new methods remain unverified. No QQ interception cause is established.

## Preparation and rebuild: 2026-10-03

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
