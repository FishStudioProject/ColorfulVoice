#!/usr/bin/env bash
# 构建 Android APK（需要 x86_64 宿主机 + .NET Android workload + NDK）。
#
# aarch64 环境跑不了官方 aapt2（x86-64 ELF），脚本会在架构检查处退出并说明步骤。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NANOFISH="$(cd "$ROOT/../NanoFish" && pwd)"
ANDROID_HOME="${ANDROID_HOME:-/opt/android-sdk}"
NDK_VERSION="${NDK_VERSION:-26.1.10909125}"
DOTNET="${DOTNET:-/opt/dotnet-ms/dotnet}"
[[ -x "$DOTNET" ]] || DOTNET="$(command -v dotnet || true)"

if [[ "$(uname -m)" != "x86_64" ]]; then
  cat >&2 <<MSG
错误：当前宿主是 $(uname -m)，Android 官方工具链只提供 x86_64 版本：
  - build-tools/aapt2 是 ELF x86-64
  - NDK clang 只提供 linux-x86_64 预编译
因此本机无法生成 APK。请在 x86_64 Linux / WSL2 / macOS 上运行本脚本。

需要准备：
  dotnet workload install android
  sdkmanager "platform-tools" "platforms;android-34" "build-tools;34.0.0" "ndk;$NDK_VERSION"
  cargo install cargo-ndk
MSG
  exit 1
fi

echo "==> 交叉编译 Rust 引擎 (arm64-v8a)"
cargo ndk -t arm64-v8a -o "$ROOT/src/ColorfulVoice.Android/lib/arm64-v8a" \
  build --release --manifest-path "$NANOFISH/Cargo.toml"

echo "==> 发布 APK"
"$DOTNET" publish "$ROOT/src/ColorfulVoice.Android/ColorfulVoice.Android.csproj" \
  -c Release -f net8.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:AndroidNdkDirectory="$ANDROID_HOME/ndk/$NDK_VERSION"

echo "APK 输出目录：bin/Release/net8.0-android/publish/"
