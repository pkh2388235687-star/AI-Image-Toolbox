# Vendored QR dependencies / 二维码依赖

- ZXing.Net 0.16.11, .NET Framework 4.0 assembly: [upstream](https://github.com/micjahn/ZXing.Net), [NuGet package](https://www.nuget.org/packages/ZXing.Net/0.16.11). Embedded inside the Windows executable; no runtime download.
- ZXing core 3.5.4: [upstream](https://github.com/zxing/zxing/tree/zxing-3.5.4), [Maven artifact](https://repo.maven.apache.org/maven2/com/google/zxing/core/3.5.4/). Compiled into the Android APK; no runtime download.
- Both use [Apache-2.0](LICENSE.md). Copyright ZXing authors and ZXing.Net authors. Original copyright notices remain in the distributed libraries.
- 两端均在本机生成和识别二维码，不上传输入图片。Windows 嵌入 DLL，Android 打包 JAR，无运行时下载。

| Dependency | SHA-256 |
|---|---|
| zxing-core.jar | `71de5d89341b5fcf5dd89da7f44e84d825d0e084cdf3ec77c9abe26b0f0ceb13` |
| zxing-net.dll | `643a5a3db0ae02998b507beb82dbc362a2d5593b429963db17eb78089aabb95a` |
