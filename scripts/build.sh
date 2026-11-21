#!/usr/bin/env bash
# 构建 NanoFish（Rust cdylib）与 ColorfulVoice（C#），并把引擎库同步到托管输出目录。
# 用法: scripts/build.sh [debug|release]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NANOFISH="$(cd "$ROOT/../NanoFish" && pwd)"
CONFIG="${1:-release}"
DOTNET="${DOTNET:-$(command -v dotnet || echo /opt/dotnet/dotnet)}"

case "$CONFIG" in
  debug)   CARGO_ARGS=();            MSBUILD_CONFIG=Debug ;;
  release) CARGO_ARGS=(--release);   MSBUILD_CONFIG=Release ;;
  *) echo "用法: $0 [debug|release]" >&2; exit 2 ;;
esac

if [[ ! -x "$DOTNET" ]]; then
  echo "找不到 dotnet，可用 DOTNET=/path/to/dotnet $0 $CONFIG" >&2
  exit 1
fi

echo "==> NanoFish ($CONFIG)"
cargo build "${CARGO_ARGS[@]}" --manifest-path "$NANOFISH/Cargo.toml"

echo "==> ColorfulVoice ($MSBUILD_CONFIG)"
mapfile -t PROJECTS < <(find "$ROOT" -name '*.csproj' \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -not -path '*/ColorfulVoice.Android/*')
for proj in "${PROJECTS[@]}"; do
  echo "    $(basename "$proj")"
  "$DOTNET" build "$proj" -c "$MSBUILD_CONFIG" --nologo -v q
done

echo "==> 同步 libnanofish.so"
"$ROOT/scripts/sync-native.sh" "$CONFIG"

echo "完成。"