# 交接与启动说明（ColorfulVoice）

> 更新：2026-11 ｜ 对应引擎：同工作区的 `../NanoFish`

## 1. 这是什么

歌声合成器 **ColorfulVoice** 的宿主端：Avalonia/C#（SukiUI 主题 + Skia 自绘钢琴卷帘），
引擎是独立仓库 **NanoFish**（Rust，C ABI）。两者通过 P/Invoke 零拷贝交互。

当前状态：Phase 0–3 完成；P0（编辑/音源/音素器相关）完成；Android 可启动、可编辑、可保存，
**试听与导出仍有已知问题**（见第 5 节）。

## 2. 快速开始（桌面）

```bash
# 1) 先构建引擎（同工作区兄弟目录）
cd ../NanoFish && cargo build --release

# 2) 回到本仓库，同步原生库到输出目录
scripts/sync-native.sh release

# 3) 构建 & 运行桌面应用
dotnet build -c Release
dotnet run -c Release --project src/ColorfulVoice.App
```

无头验证（推荐用于改动后自检）：

```bash
dotnet run -c Release --project tests/ColorfulVoice.Headless -- --self-test
dotnet run -c Release --project tests/ColorfulVoice.Headless -- /tmp/editor.png --size 1080x640
```

## 3. 快速开始（Android）

```bash
# 引擎（手机试听需要 --audio）
../NanoFish/scripts/build-android-engine.sh --audio

# APK（默认程序集打包，切勿改压缩设置，见 AGENTS.md）
DOTNET_ROOT=/opt/dotnet9 dotnet publish -c Release -f net9.0-android \
  -p:RunAOTCompilation=false -p:AndroidEnableProfiledAot=false
```

包名：`com.colorfulvoice.app`（旧包名 `com.novavoice.colorfulvoice` 需卸载）。
详见 [android-ondevice.md](android-ondevice.md)。

## 4. 关键路径

| 路径 | 作用 |
| :-- | :-- |
| `src/ColorfulVoice.UI/Views/MainView.axaml(.cs)` | 编辑器主逻辑 |
| `src/ColorfulVoice.UI/Views/HomeView.axaml` | 主页（响应式：≥760 宽屏 / 窄屏竖排） |
| `src/ColorfulVoice.UI/Controls/PianoRoll.cs` | Skia 自绘钢琴卷帘、手势 |
| `src/ColorfulVoice.UI/Views/BankWindow.axaml.cs` | 音源管理器（浮层） |
| `src/ColorfulVoice.UI/Services/FileStaging.cs` | Android SAF 文件暂存（content:// → 缓存） |
| `src/ColorfulVoice.UI/Services/VoicebankImporter.cs` | 音源文件夹/zip 导入 |
| `src/NanoFish.Interop/` | P/Invoke、`AudioPlayback`、UST 读写 |
| `tests/ColorfulVoice.Headless/` | 无头渲染与自检入口 |

## 5. 已知问题

1. **试听音质**：手机端播放已接通（miniaudio AAudio/OpenSL），但存在“鬼畜/断续”待定位，
   需要区分是缓冲欠载还是 WSOLA 颗粒感。
2. **导出 WAV**：走同一渲染路径，波形读不到时会输出静音；已把渲染统计/警告显示到状态栏。
3. **音源导入**：Android SAF 目录枚举不稳定，提供 zip 导入兜底；采样缺失数量会显示在音源管理器。
4. **表情声源**：引擎已支持（`Track::expression`），UI 尚未接入。
5. 桌面弹窗已全部改为页面内浮层（Android 单视图不能创建 Window）。

## 6. 下一步

1. 定位试听音质：先让用户给出 A 断续 / B 颗粒 / C 变调 / D 爆音 的分类。
2. 表情声源 UI、批量渲染、参考音轨（P1）。
3. `dpkg-deb` 桌面分发包（Phase 4）。

## 7. 协作约定

- 默认不打 APK，改动后用无头自检 + 截图验证。
- Android 打包不要设置 `AndroidEnableAssemblyCompression=false` / `EmbedAssembliesIntoApk=true`（会导致启动即崩）。
- 设备 `/sdcard` 写入受限，**没有日志不代表代码没跑**；启动异常已改为直接显示在屏幕上。
