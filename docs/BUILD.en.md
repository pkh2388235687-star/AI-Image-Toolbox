# Build and test

English | [简体中文](BUILD.md) | [Home](../README.en.md)

## Windows

Use Windows with the .NET Framework 4.x C# compiler, PowerShell and Python 3.10+. .NET Framework 4.8 is recommended for running the app. No NuGet packages are needed.

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -Test
python tests/windows/verify_apng.py
python tests/windows/verify_tools.py
python tests/windows/verify_inspection.py
python tests/windows/verify_obfuscation.py
```

Tests use `build/windows/AI-Image-Editing-Tools.exe` and write to `build/verification/`. Only `-Test` includes developer self-tests. Run the four Python checks in this order: later checks reuse earlier synthetic fixtures.

Build the release without self-tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -OutputDirectory releases/Windows
```

The icon is included. `build-icon.ps1` regenerates the required ICO without preview files.

## Android

Install Python 3.10+, JDK 17+ and the official Android SDK packages `platforms;android-35` and `build-tools;35.0.0`. No Gradle or third-party runtime libraries are needed. SDK downloads are development setup only; the app runs offline.

Set `ANDROID_SDK_ROOT` and `JAVA_HOME`, or overrides `AIPT_SDK_ROOT` and `AIPT_JAVA_HOME`. Use the standard SDK layout:

```text
<SDK>/platforms/android-35/android.jar
<SDK>/build-tools/35.0.0/
```

```sh
export ANDROID_SDK_ROOT=/path/to/android-sdk
export JAVA_HOME=/path/to/jdk-21
python android/build.py
```

The release is written to `releases/Android/AI-Image-Editing-Tools.apk`. `python android/build.py --test` builds `build/android/test.apk`, including test classes and the unpacked fixture bundle. Install and start its self-test using adb:

```sh
adb install -r build/android/test.apk
adb shell am start -n com.guangyingruoshui.imagetools/.MainActivity --ez selftest true
```

Each build has an isolated intermediate directory. Release assets contain notices only. Signing verification succeeds before the deliverable is replaced.

## Signing

By default, a developer key/password is created under `~/.ai-image-tools/signing/`. Set `AIPT_KEYSTORE` and `AIPT_KEYSTORE_PASS_FILE` to reuse an existing key and password file, with alias `guangyingruoshui`. Repository-contained keys are rejected. APKs signed with a new key cannot upgrade the author's signed APK in place.

The author's existing key is stored outside this repository. Back it up and reuse it for future updates; never publish it or its password.

## Upload

Upload sources, documentation and the two latest packages to one repository. Do not include `build/`, Python caches, SDKs or signing files. Automated checks inspect filenames, private files and release-package structure. CI does not publish packages or commit updated binaries.

## QR dependencies and new checks

Windows embeds ZXing.Net 0.16.11; Android compiles ZXing core 3.5.4. Pinned libraries, SHA-256 and the full Apache-2.0 license are in [vendor/zxing](../vendor/zxing/README.md). No runtime dependency downloads.

`--self-test` includes QR/border/opaque-linkage checks, grayscale isolation, text layers and history. `--ui-test` covers bilingual layouts and QR export/import. Android `--test` compiles corresponding native bitmap/touch checks; compiling them does not count as running device tests. Camera capture needs separate physical-device validation.
