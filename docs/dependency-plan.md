# 依赖、操作边界与恢复

用户批准在“可通过 Git 和项目内文件恢复”的条件下引入下列依赖。源码与配置由 Git 恢复；被 Git 忽略的缓存和生成物需要单独清理或还原。原构建产物在依赖操作前已备份，因此恢复不需要修改系统环境。

## 实际引入的清单

| 对象 | 固定版本 / 来源 | 项目内位置 |
| --- | --- | --- |
| ImGui.NET | 1.91.6.1，nuget.org | .packages/imgui.net/1.91.6.1 |
| System.Buffers | 4.5.1，nuget.org | .packages/system.buffers/4.5.1 |
| System.Numerics.Vectors | 4.5.0，nuget.org | .packages/system.numerics.vectors/4.5.0 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0，nuget.org | .packages/system.runtime.compilerservices.unsafe/6.0.0 |
| cimgui.dll | 上述 ImGui.NET 包附带的 Windows x64 原生库 | RoxyLib/bin/<配置>/net481/cimgui.dll |
| Noto Sans SC | Google Fonts 官方字体，SIL OFL 1.1 | RoxyLib/Resources/Fonts/NotoSansSC.ttf 和 OFL.txt |

三个项目的 packages.lock.json 记录相同的四个包及其内容哈希，无清单外 NuGet 依赖。测试框架未另外安装。许可证随 Resources/Licenses 和 Resources/Fonts/OFL.txt 分发。

来源：
- [ImGui.NET 包](https://www.nuget.org/packages/ImGui.NET/1.91.6.1)
- [ImGui.NET 依赖声明](https://github.com/ImGuiNET/ImGui.NET/blob/v1.91.6.1/src/ImGui.NET/ImGui.NET.csproj)
- [Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)
- [Dear ImGui 平台接口](https://github.com/ocornut/imgui/blob/v1.91.6/imgui.h)

字体原文件 17,772,300 字节，Git blob：fb0637bafbcd804fe32152370a1225990745b4bc。
SHA-256：A3041811A78C361B1DE50F953C805E0244951C21C5BD412F7232EF0D899AF0DA。
OFL 原文件 Git blob：1c9f43281b8f216c5461fe9ac729afbade7724e4；
SHA-256：1C05C68C34F9708415AADA51F17E1B0092D2CEA709BF4A94CD38114F9E73D7D9。

## 已批准的访问与持久影响

构建脚本使用现有 C:/Program Files/dotnet/dotnet.exe、现有 Framework 引用程序集，以及：
D:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed
中的项目引用。用户另外指定读取 E:/code/.editorconfig。

Restore 使用项目 NuGet.config，清空继承源/fallback，只访问 nuget.org：
`dotnet restore RoxyLibForAdofai.slnx --locked-mode --configfile NuGet.config --packages .packages -p:NuGetAudit=false -p:NuGetInteractive=false -p:AutomaticallyUseReferenceAssemblyPackages=false`。

所有 CLI、TEMP/TMP、HTTP 和插件缓存均定位到 RoxyLib/obj/.build，包缓存为 .packages。关闭遥测、首次运行证书生成、全局工具 PATH 修改、workload 通知和编译服务复用。构建/测试进程结束后恢复进程环境变量。

持久写入只发生在本项目：源码、项目配置、字体和许可证、.packages、各项目 bin/obj、artifacts/refactor-baseline 备份。ImGui 原生验证只加载项目内 cimgui，不启动游戏。未安装 SDK、Unity、全局包、系统字体或常驻程序；未写用户 NuGet 配置、系统 PATH、注册表、服务、计划任务或游戏 Mods。

## 恢复到重构前

本节是恢复说明，本次未执行回退或清理。

1. 保留本工作分支和后续用户改动。原始源码快照为 6b29449，原 master 保留在该提交；切回它即可恢复 Git 跟踪的旧源码。新增字体、许可证、测试源码和项目配置随分支切换恢复。
2. Git 不会自动恢复被忽略的 bin/obj 或清掉 .packages。确认停止使用这些目录的构建/测试后，只清理本项目新增的 .packages、RoxyLib.Tests/bin、RoxyLib.Tests/obj；RoxyLib/bin、RoxyLib/obj、RoxyExample/bin、RoxyExample/obj 按下一项恢复。
3. 依赖操作前已把原构建目录压缩到 artifacts/refactor-baseline。需要恢复原输出时，先检查压缩包完整，确认目标绝对路径仍位于 E:/code/RoxyLibForAdofai 内，再以对应备份替换四个构建目录：

| 备份 | 恢复目标 | 备份字节数 |
| --- | --- | --- |
| artifacts/refactor-baseline/RoxyLib-bin.zip | RoxyLib/bin | 50,120 |
| artifacts/refactor-baseline/RoxyLib-obj.zip | RoxyLib/obj | 65,545 |
| artifacts/refactor-baseline/RoxyExample-bin.zip | RoxyExample/bin | 9,687 |
| artifacts/refactor-baseline/RoxyExample-obj.zip | RoxyExample/obj | 12,032 |

4. 原输出恢复后，RoxyLib/obj/.build、测试预览和测试配置也会随新生成物清理消失。保留备份至确认恢复成功；之后可清理本次新增的 artifacts/refactor-baseline 目录。
5. 不要使用全仓库 git clean -xfd：它可能删除与本次重构无关的个人文件。执行任何清理前核对实际路径、当前内容和后续用户更改；不涉及任何项目外删除。

Git 快照负责源文件，以上 ZIP 负责原先被忽略的输出；两者结合才能恢复本次操作涉及的原项目状态。电脑上没有新增系统组件，因此无需全局卸载。若后续用户自行部署到游戏，游戏内文件需另行备份和恢复，不属于此次已执行的操作。
