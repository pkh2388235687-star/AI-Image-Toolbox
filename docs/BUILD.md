# 构建与测试

[English](BUILD.en.md) | [返回首页](../README.md)

## Windows

需要 Windows 的 .NET Framework 4.x 编译器、PowerShell 和 Python 3.10+；程序建议使用 .NET Framework 4.8。无需 NuGet 包。在仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -Test
python tests/windows/verify_apng.py
python tests/windows/verify_tools.py
python tests/windows/verify_inspection.py
python tests/windows/verify_obfuscation.py
```

测试使用 `build/windows/AI-Image-Editing-Tools.exe`，结果存放在 `build/verification/`。`-Test` 才编译开发自测代码。四个 Python 检查须按上面的顺序执行，后续检查使用前面生成的合成样本。

正式包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -OutputDirectory releases/Windows
```

图标已随仓库提供；需要重新生成时运行 `build-icon.ps1`。只生成必要 ICO，不生成预览图片。

## Android

需要 Python 3.10+、JDK 17 或以上、官方 Android SDK `platforms;android-35` 与 `build-tools;35.0.0`。构建不依赖 Gradle 或第三方运行时库。SDK 下载仅发生在准备开发环境时；程序运行不需要联网。

设置 `ANDROID_SDK_ROOT` 和 `JAVA_HOME`，或者使用覆盖变量 `AIPT_SDK_ROOT`、`AIPT_JAVA_HOME`。使用标准 SDK 布局：

```text
<SDK>/platforms/android-35/android.jar
<SDK>/build-tools/35.0.0/
```

```powershell
$env:ANDROID_SDK_ROOT = 'C:/Android/Sdk'
$env:JAVA_HOME = 'C:/Java/jdk-21'
python android/build.py
```

正式包输出至 `releases/Android/AI-Image-Editing-Tools.apk`。`python android/build.py --test` 生成 `build/android/test.apk`，包含测试类与解压后的小型样本。使用 Android SDK 的 adb 安装测试包后启动自测：

```sh
adb install -r build/android/test.apk
adb shell am start -n com.guangyingruoshui.imagetools/.MainActivity --ez selftest true
```

每次构建使用独立的中间目录，避免旧测试资产混入正式包；签名验证成功后才替换交付文件。

## 签名密钥

默认在用户主目录的 `.ai-image-tools/signing/` 创建开发者自己的密钥与密码。可用 `AIPT_KEYSTORE`、`AIPT_KEYSTORE_PASS_FILE` 指向已有密钥和密码文件；别名为 `guangyingruoshui`。脚本拒绝把密钥放在仓库内。新密钥签名的 APK 不能覆盖安装作者签名的 APK。

作者原有签名密钥已保存在仓库之外，后续更新需继续使用它；请备份，不要上传。不要把密码粘贴到命令或公开日志中。

## 上传

源文件、两端最新安装包和说明放在同一个仓库即可。`build/`、`__pycache__/`、本地 SDK、签名文件不上传。自动检查验证文件名、隐私文件和正式包结构。GitHub CI 不发布安装包、不提交构建后的二进制文件。
