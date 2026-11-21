# 在 arm64 Android 手机上构建 Avalonia APK

结论：**可以**。Avalonia + .NET 9 Android 能在这台 aarch64 proot 里出 APK，
前提是替换掉 Android SDK pack 里的一批 x86-64 宿主工具。

## 环境

- .NET 9 SDK（arm64）：`dotnet-install.sh --channel 9.0`，装到 `/opt/dotnet9`
  - .NET 8 的 workload 清单虽然写着 `linux-arm64`，但自带清单 34.0.43 不含该平台；
    .NET 9 自带 35.0.7 才真正列出 `linux-arm64`。
- Android SDK：platform-tools、platforms;android-34/35、build-tools;34/35（arm64 替换后）
- JDK 17（arm64）
- `dotnet workload install android`（会拉 AOT cross 包，其中带 **arm64 的 llc**）

## 必须替换的 x64 工具

| 工具 | 替换来源 |
| :-- | :-- |
| `build-tools/*/aapt2` | [AndroidIDE/platform-tools](https://github.com/AndroidIDEOfficial/platform-tools) `aapt2-arm64-v8a`（静态） |
| `build-tools/*/zipalign` | 同一 release 的 `platform-tools-34.0.4-aarch64.tar.xz` |
| pack `tools/Linux/aapt2` | 同上 |
| pack `binutils/bin/llc` | `/opt/dotnet9/packs/Microsoft.NETCore.App.Runtime.AOT.linux-arm64.Cross.android-arm64/*/tools/llc`（arm64 LLVM 19），并复制 `libc++.so.1`、`libc++abi.so.1` |
| pack `binutils/bin/ld` | 包装脚本 `exec /usr/bin/ld "$@"`（只出 `android-arm64` 时系统的 aarch64 ld 够用） |
| pack `binutils/bin/llvm-objcopy` | 包装脚本 `exec /usr/bin/objcopy "$@"` |
| `libZipSharpNative-3-3.so` | 自行编译：`g++ -shared -fPIC -DLIBZIPSHARP_VERSION='"3.3.0"' sizes.cc values.cc version.cc -lzip -lbz2 -lzstd -lz` |
| `libMono.Unix.so` | NuGet `Mono.Posix.NETStandard` 的 `runtimes/linux-arm64/native/libMonoPosixHelper.so` 改名 |

另外 `platforms/android-35/android.jar` 的旧 aapt2 解析失败，构建时临时换成
`android-34/android.jar`（`android.jar.orig` 备份）。

## 构建

```bash
export DOTNET_ROOT=/opt/dotnet9
dotnet build ColorfulVoice/src/ColorfulVoice.Android/ColorfulVoice.Android.csproj \
  -c Debug -f net9.0-android \
  -p:AndroidSdkDirectory=/opt/android-sdk \
  -p:JavaSdkDirectory=/usr/lib/jvm/java-17-openjdk-arm64 \
  -p:AndroidEnableMarshalMethods=false
```

产物：`bin/Debug/net9.0-android/com.novavoice.colorfulvoice-Signed.apk`（约 9.5 MB）。

## 已知限制

- 只打 `android-arm64`（`RuntimeIdentifiers`），x86_64 ABI 需要能同时链接 elf_x86_64 的 ld。
- 引擎 `libnanofish.so` 还没有 Android 版本（需 Rust android target + NDK sysroot），
  所以 APK 里目前只有 UI，试听会走「找不到引擎」的降级路径。
