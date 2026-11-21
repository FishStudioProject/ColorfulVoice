#!/usr/bin/env bash
# 把编译好的 libnanofish.so 复制到每个 C# 输出目录（net8.0 下的 bin/…）。
# 这样 `dotnet run` / 直接运行 AppHost 时，P/Invoke 能在程序目录里找到引擎。
# 也可用 NANOFISH_LIB=/absolute/path/libnanofish.so 覆盖。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NANOFISH="$(cd "$ROOT/../NanoFish" && pwd)"
CONFIG="${1:-release}"
SRC="$NANOFISH/target/$CONFIG/libnanofish.so"

[[ -f "$SRC" ]] || { echo "缺少 $SRC，请先 cargo build --$CONFIG" >&2; exit 1; }

count=0
while IFS= read -r dir; do
  install -m 0755 "$SRC" "$dir/libnanofish.so"
  count=$((count + 1))
done < <(find "$ROOT" -type d -path '*/bin/*/net8.0' -not -path '*/obj/*')

echo "    已同步到 $count 个输出目录"
[[ $count -gt 0 ]] || echo "    提示：尚未生成任何 C# 输出目录（先 dotnet build）" >&2