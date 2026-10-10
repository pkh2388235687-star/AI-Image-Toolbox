# Release files / 最新安装包

Repository / 仓库：`AI-Image-Toolbox`。Application / 应用：`AI Image Editing Tools`。

Prepared / 整理日期：2026-10-10。Only the current release packages are included / 仅保留最新版安装包。

| File / 文件 | Bytes / 字节 | SHA-256 |
| --- | ---: | --- |
| [AI-Image-Editing-Tools.exe](../releases/Windows/AI-Image-Editing-Tools.exe) | 1832960 | `d69b7e96a4b3ed5a0de4e598f71373d2a595ba40f69922ec00f778fd2345c9bb` |
| [AI-Image-Editing-Tools.apk](../releases/Android/AI-Image-Editing-Tools.apk) | 803348 | `a1f97ed89c0509a4c348a1250de2105cb24b3c68668dbcc156ca07d45c583f16` |

## Included update / 本次更新

- 相机兼容更新：Windows 使用 Media Foundation 异步采集及设备尺寸协商，增加设备选择、重连、隐私设置和分类错误提示；Android 处理前后摄像头、支持尺寸、NV21/YV12 行布局、方向、对焦及退出释放。
- Camera updates: Windows uses asynchronous Media Foundation capture with advertised-size negotiation, camera selection, reconnect, privacy settings and categorized errors. Android handles front/rear selection, supported sizes, NV21/YV12 rows, orientation, focus and cleanup.
- 原色优化新增自定义有效强度及恢复自动，仅在该方式下显示；手动值受实际色域限制，过高可能增加透色。自动封面按整图色彩增加独立柔和编织花纹，避免把花纹叠进黑底原图。
- Source colors adds custom effective strength and Restore automatic, visible only for that method. Manual values remain gamut-limited and higher values may leak more color. Automatic covers adapt independent soft woven ribbons to whole-image chroma without painting them into the black view.
- 二维码优化改用 250 灰度浅色背景校准整图和码区，减少常见浅灰聊天背景中的码块残影；黑底仍能扫描。平台转码或任意背景下的零残影不能保证。
- QR optimization calibrates the whole image and code area to near-white level 250, reducing remnants on pale chat backgrounds while retaining black-view decoding. Zero interference after transcoding or on arbitrary backgrounds is not guaranteed.

- 合并原色说明与有效强度，按实际文字和窗口宽度收紧留白；参数刷新保持高度与滚动位置稳定。Windows 数值框恢复普通滚轮直接调节，无需辅助键，覆盖输入区和箭头区；数值上下限与设定步长保持有效，不同时带动页面。同一行控件垂直居中。
- Source-color guidance and measured strength share one compact, width-aware explanation with stable height/scroll position during refresh. Windows numeric inputs now directly adjust with an ordinary wheel over either the edit or arrow area, without modifier keys. Bounds and configured increments apply, with no simultaneous page scrolling. Row controls are vertically centered.
- 文字图层实时显示当前内容，长文本缩略；合并连续拖动与预览任务，释放后保存最终位置，避免预览闪空与反复重建文字控件。
- Text-layer names follow current content and shorten long text. Drag/preview updates are coalesced, with final placement committed on release, avoiding empty intermediate previews and repeated text-control rebuilding.
- 原色优化逐处限制色差，提高区域采样和色彩预算，减少整图被过度降色；当前有效强度从实际输出计算。更多色彩可能增加封面透色，仍不能保证任意查看器零残影。
- Source-color optimization limits chroma locally with finer regional sampling and a larger chroma budget, reducing excessive whole-image desaturation. Effective strength is measured from actual output. More color can increase cover leakage; zero interference across viewers is not guaranteed.

- 二维码制作支持网址粘贴和换行显示，导出自动建立“二维码制作”子文件夹。保留直接生成、图片叠加、自由拖动与定位，以及 0–12 格白边调节；推荐至少 4 格。程序不上传图片，不提供远程托管。
- QR generation adds URL paste/wrapping and automatically exports into a feature subfolder. QR-only/image-overlay output, dragging/positioning and 0–12 module borders remain; at least 4 modules is recommended. The app neither uploads images nor provides remote hosting.
- 两端支持导入图片扫描及相机扫码，结果可复制，HTTP/HTTPS 地址可手动打开浏览器。Android 增加可选 CAMERA 权限，只在扫码时请求；Windows 使用系统 Media Foundation 采集，不支持的相机可改用导入扫描。打开网站后的登录、显示和下载由网站决定。
- Both clients support image-import and camera scanning, copying results and explicit HTTP/HTTPS browser launch. Android adds optional CAMERA permission on demand. Windows uses system Media Foundation capture, with image import as a fallback. Website login/display/download behavior remains controlled by the website.
- 图片叠加、双图切换及文件伪装支持最多 32 条独立文字，单独拖动、定位、调色及删除。二维码始终盖在文字上；直接生成只输出二维码。
- Image overlays, Double image and File disguise support up to 32 independently draggable, positioned, colored and removable text layers. QR pixels stay above text; QR-only mode outputs the code alone.
- 背景显图恢复“原色优化”默认项，移除原“封面防透”选项，替换为可选“二维码优化”：使用灰度封面，白底保留封面，黑底增强码区对比。需使用本工具生成并保留参数的二维码 PNG；普通方式不强制叠加可见二维码。尺寸不足或缺少参数明确报错，导出前自检。
- Background reveal restores Source colors as the default and replaces the prior cover-isolation choice with optional QR optimization: grayscale cover on white, stronger QR contrast on black. It requires app-generated QR PNGs with intact metadata. Ordinary methods no longer force a visible QR overlay. Missing metadata and inadequate resolution are reported; export self-checks the code.
- 两端导入扫描自动尝试白底、黑底，Windows 修复扫描前白底合成导致透明数据丢失的问题，无需手动制作黑底图片。平台缩小、转码或合成白底仍可能破坏效果，无法从扫描中找回已删除的信息。
- Both clients automatically scan imported images against white and black. Windows no longer loses alpha by flattening to white before scanning. No manual black-view conversion is needed. Platform resizing, re-encoding or white flattening can still break the effect; scanning cannot recover removed information.
- 打码增加重做，最多 50 个历史状态、256 MiB 本地无损 PNG 缓存预算；较大图片可能减少保留次数，新编辑清空重做。修复解码位图文件生命周期和编辑像素格式问题。
- Redaction adds redo and up to 50 history states under a 256 MiB local lossless-PNG cache budget. Large images can reduce retained steps; new edits clear redo. Decoded bitmap lifetime and editable pixel-format issues are fixed.
- 左侧工具区/手机菜单滚动适配；Windows 页面子控件缓冲绘制、显示新页面后再隐藏旧页、合并与缓存预览，减轻闪烁。Android 沿用系统界面硬件加速。图像计算仍使用 CPU，本次未新增 GPU 图像运算后端，未宣称所有设备性能一致。
- Navigation scrolling is adaptive. Windows buffers child painting, reveals the destination before hiding the prior page, and coalesces/caches previews to reduce flicker. Android keeps system UI hardware acceleration. Image calculations still use the CPU; no GPU image-compute backend is added and universal performance is not claimed.

## Verification / 验证

- Windows 核心、设置、批量及完整中英文界面检查通过，包含自定义强度、恢复自动、模式隐藏、滚轮调节和刷新布局稳定。更换封面对照在 0/25/75/100 强度下黑底变化不超过 1 色阶；这不保证第三方查看器表现一致。
- Windows core/settings/batch and complete bilingual UI checks pass, including manual strength, restore, mode visibility, numeric wheels and stable refresh layouts. Cover swaps at 0/25/75/100 change the black view by at most one level per channel; this does not guarantee identical third-party rendering.
- ASUS FHD webcam 实机开启两次，各取得 4 帧 640×480，关闭释放后可重开；未保存画面。其他型号及红外相机未实测。
- The ASUS FHD webcam opens twice and delivers four 640×480 frames per cycle, reopening after cleanup. No camera images are saved. Other models and infrared cameras are untested.
- 二维码 768 组像素检查通过：245/255 背景码块残差不超过 3 色阶，250 不超过 1；纯白校准变化不超过 6。导出透明 PNG 黑底解码通过。Android 同款主机解码库检查 20 组 PNG/背景组合，含 6 组浅色封面不解码；320 组原色 C#/JVM 像素向量一致。
- All 768 QR pixel cases pass: ink/paper remnants differ by at most three levels on backgrounds 245/255 and one on 250; pure-white calibration differences stay within six. Saved transparent PNGs decode on black. The host Android-core decoder checks 20 PNG/background combinations, including six hidden pale covers; all 320 source-color C#/JVM vectors match.
- Android 相机亮度行、截断数据拒绝和尺寸排序的主机检查通过。测试源码及正式 APK 编译通过，原证书 v1/v2/v3 签名通过。Android 真机位图/触摸/相机权限、iOS 与 QQ/Bilibili 传输往返未执行，主机检查不等同手机实测。
- Host Android camera-luminance, truncation-rejection and size-ranking checks pass. Test/release sources compile and original-certificate v1/v2/v3 signatures verify. Android device bitmap/touch/camera permissions, iOS and QQ/Bilibili round trips remain untested; host checks are not device tests.

Application name, icon and Android package remain unchanged. No Internet permission was added. Signing secrets and temporary outputs stay outside the public repository / 应用名、图标及 Android 包名不变，未增加联网权限，密钥及临时产物不进入公开目录。

Signer certificate SHA-256 / 签名证书：`4a6e6b133417dd34f3c4b357590e7500f7d92daada25b2f39621ab7b598f1508`。

Original code remains [MIT](../LICENSE); bundled ZXing/ZXing.Net use Apache-2.0. Preserve [third-party notices](../THIRD_PARTY_NOTICES.md) and [dependency licenses](../vendor/zxing/LICENSE.md) / 原创 MIT 与第三方许可须保留。

[Validation / 验证](VALIDATION.md) | [Validation in English](VALIDATION.en.md)
