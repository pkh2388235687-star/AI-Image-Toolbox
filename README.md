# AI Image Toolbox

[English](README.en.md) | 简体中文

仓库名：`AI-Image-Toolbox`（AI 图片工具箱）；应用名称：`AI Image Editing Tools`。

由 **光影若水（Guangying Ruoshui）** 制作的本地图片工具集合，提供 Windows 与 Android 原生客户端，支持中英文界面。

## 下载与运行

| 平台 | 最新文件 | 要求 |
| --- | --- | --- |
| Windows | [AI-Image-Editing-Tools.exe](releases/Windows/AI-Image-Editing-Tools.exe) | Windows 10/11，.NET Framework 4.8 |
| Android | [AI-Image-Editing-Tools.apk](releases/Android/AI-Image-Editing-Tools.apk) | Android 5.0 / API 21 及以上 |

Windows 双击 EXE；Android 允许当前下载应用安装 APK 后安装。两端应用名为 **AI Image Editing Tools**。设置中可切换中文与英文，制作信息保留作者署名。

## 功能

| 功能 | 说明 |
| --- | --- |
| 双图切换 | 封面与隐藏图合成 PNG；批量处理、封面文字、九宫格位置、自由放置、编号；同页还原隐藏图 |
| 图片混淆 | 本地像素重排、解混淆、批量处理；自定义 JPG 质量百分比与实际编码大小预览 |
| 文件伪装 | MP4、ZIP、RAR、7Z 加 JPG 封面，批量封装与 SHA-256 校验还原；视频用 `_v2i`，压缩包用 `_a2i` 命名 |
| 合成 GIF | 队列排序、帧间隔、循环设置及本地预览 |
| 清信息 | 清理 WebUI/ComfyUI 工作流、提示词等元数据；支持 PNG/APNG、JPEG、WebP、GIF |
| 打码 | 马赛克、模糊、颜色遮盖；框选、画笔、撤销及完整图片导出 |
| tag 读取 | 独立显示 WebUI 与 ComfyUI 正负提示词及参数，逐项复制，分别导出 TXT；空板块跳过 |
| 设置 | 语言、默认参数、保存位置记忆与作者信息 |

Android 使用可收起的菜单；队列约占可用屏幕的 29%，同时显示三条记录，文件名单行并省略中间内容。位置菜单显示三行，可滚动选择。相册和自定义文件夹保存方式可记忆，支持系统文件分享。打码页面停止滚动满两秒后才允许涂抹。Windows 支持预览随窗口变化、滚轮缩放、中键移动和板块高度调整。

## 使用边界

- 图片混淆的“原图质量”附带原文件，使用“还原原文件”可恢复完整字节。无损 PNG 仅保证像素；压缩 JPG 有损，不能还原原文件。混淆不是密码加密。
- 双图切换还原的是隐藏图像；不等同于恢复导入文件的全部元数据或文件字节。
- 文件伪装不转码原视频。ZIP 改后缀直接解压经过特定解压器测试；RAR/7Z 的识别取决于解压软件。一键还原按原数据校验。
- 发送带隐藏数据的图片需选择原图，接收后保存原图。截图、重压缩或平台处理可能破坏隐藏数据，尚未完成真实 QQ 好友往返验证。
- 默认有界预览和逐个批处理适配较低配置设备，但超大图原尺寸处理仍受手机内存限制；未在所有 2015 年手机上验证。
- 所有处理在本机进行。Android 没有联网权限、WebView 或远程加载代码。

## 仓库结构

```text
src/                     Windows C# 源码
android/src/             Android Java 源码
android/res/             Android 图标
android/tests/           Android 测试代码及小型样本包
assets/                  Windows 图标与英文翻译
tests/windows/           Windows 自动化测试
releases/Windows/         唯一最新 EXE
releases/Android/         唯一最新 APK
docs/                    中英文构建与验证说明
tools/                   仓库检查工具
.github/                 自动检查与问题模板
```

[构建说明](docs/BUILD.md) · [验证记录](docs/VALIDATION.md) · [文件校验值](docs/RELEASES.md) · [贡献说明](CONTRIBUTING.md) · [安全报告](SECURITY.md)

本目录可作为一个仓库的根目录上传。SDK、模拟器、构建缓存、签名密钥、调试输出及旧安装包不随仓库提供。自动化测试源码和小型合成样本用于复现检查；正式安装包不包含自测入口或样本。

## 许可

原创代码采用 [MIT](LICENSE)，版权署名为光影若水。Gilbert 遍历算法的 BSD-2-Clause 版权与许可完整保留在 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。分发时保留这两个文件。
