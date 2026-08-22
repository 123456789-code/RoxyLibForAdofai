# RoxyLib (RLA)

为 **A Dance of Fire and Ice（冰与火之舞）** 打造的 Unity mod 开发库，设计对标 Minecraft 的 **malilib**：业务 mod 只需定义规则字段 + 一行注册，即可自动获得 **设置 GUI、多语言、配置持久化、快捷键绑定** 等能力。

- **RoxyLib**：库本体（一个独立的 UMM mod，`Id = "RoxyLib"`）
- **RoxyExample**：示例业务 mod，展示完整用法（`Requirements: ["RoxyLib"]`）

---

## 特性

| 特性 | 说明 |
|---|---|
| 声明式规则 | 用 `[RoxyMod]` + `[RoxyRule]` 注解静态字段，自动注册 |
| 六种规则类型 | Switch / SliderInt / SliderFloat / Options / Color / String |
| 全自动 GUI | UGUI 实时构建的设置窗口，左栏 mod 列表 + 右栏规则面板，含搜索 |
| 多语言 | 内建 en-us / zh-cn / ja-jp / ko-kr，切语言即时刷新，回退链容错 |
| 配置持久化 | 每 mod 一个 `config.json`，原子写入、防抖保存 |
| 快捷键 | Switch 规则可绑定组合键（Ctrl/Shift/Alt + 主键），无默认热键 |
| 无需默认值 | 设计哲学：**任何 mod 都不应拥有默认快捷键** |

---

## 目录结构

```
RoxyLibForAdofai/
├── RoxyLib/                      # 库本体（独立 UMM mod）
│   ├── RoxyLib.cs                # 核心入口：Register / Tick / SaveAll / OpenSettings
│   ├── RoxyLibRules.cs           # RoxyLib 自身规则（OpenSettings / Language / DebugLogLevel）
│   ├── Main.cs                   # RoxyLib 的 UMM 入口（注册自身 + 驱动 Tick + UMM 面板按钮）
│   ├── Rules.cs                  # 规则系统（RoxyRuleType / RuleInfo / RoxyRules）
│   ├── Input.cs                  # 快捷键（KeyCombination / RoxyKeybind / RoxyInput）
│   ├── Lang.cs                   # 本地化（LanguageEnum / RoxyLang）
│   ├── Storage.cs                # 配置读写（RoxyStorage）
│   ├── Host.cs                   # mod 宿主抽象（RoxyHost / RoxyHosts.FromUmm）
│   ├── Gui.cs                    # 主题（#66CCFF）+ UGUI 控件工厂（UiFactory）
│   ├── GuiWindow.cs              # 设置主窗口（RoxyGui）
│   ├── DropdownControl.cs        # 库级下拉组件（RoxyDropdown）
│   ├── Info.json                 # UMM 元数据
│   └── lang/                     # RoxyLib 自身翻译表
├── RoxyExample/                  # 示例业务 mod
│   ├── Main.cs                   # 一行注册
│   ├── ExampleRules.cs           # 示例规则
│   ├── Info.json
│   └── lang/
├── Directory.Build.props         # 全局构建配置（游戏路径、清理 target）
├── RoxyLibForAdofai.slnx         # 解决方案
└── Run.bat                       # 一键部署到游戏 Mods 目录
```

---

## 快速开始（业务 mod 开发者）

### 1. 项目引用

在业务 mod 的 `.csproj` 中加入对 RoxyLib 的**私有引用**（`Private="false"`，不把 RoxyLib.dll 打进业务 mod 目录，而是由 UMM 先加载独立 RoxyLib）：

```xml
<ProjectReference Include="..\RoxyLib\RoxyLib.csproj" Private="false" />
```

> 部署模型（对标 malilib）：**RoxyLib.dll 只存在于 `Mods\RoxyLib\`**，业务 mod 通过 `Info.json` 的 `Requirements: ["RoxyLib"]` 声明依赖。

### 2. 定义规则类

用 `[RoxyMod]` 标记容器类（指定 `ModId`），用 `[RoxyRule]` 标记静态字段：

```csharp
using RoxyLib.Rules;

namespace MyMod;

[RoxyMod(ModId = "MyMod")]
public static class MyModRules {
	// Switch：bool，自动获得开关 + 快捷键绑定
	[RoxyRule(Category = "General")]
	public static bool EnableFeature = true;

	// SliderFloat：需 Min/Max，滑条 + 输入框
	[RoxyRule(Category = "Gameplay", Min = 0.5f, Max = 3f)]
	public static float Speed = 1.0f;

	// SliderInt：需 Min/Max
	[RoxyRule(Category = "Gameplay", Min = 0, Max = 100)]
	public static int ComboLimit = 50;

	// Options：枚举，自动生成下拉框
	[RoxyRule(Category = "Visual")]
	public static TrailStyle Trail = TrailStyle.Glow;

	// Color：UnityEngine.Color（含 alpha，序列化 #RRGGBBAA）
	[RoxyRule(Category = "Visual")]
	public static UnityEngine.Color TrailColor = new UnityEngine.Color(1f, 1f, 1f, 1f);

	// String：文本输入框
	[RoxyRule(Category = "Visual")]
	public static string Label = "Hello";
}

public enum TrailStyle { Glow, Stripes, Fade }
```

> ⚠️ **Slider（整型/浮点）必须提供 `Min` 和 `Max`**，否则注册时抛 `InvalidOperationException`（编译期无法校验，运行时检查）。

### 3. 一行注册

```csharp
using UnityModManagerNet;

namespace MyMod;

public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		RoxyLib.RoxyLib.Register(mod_entry);   // 生命周期全权接管
	}
}
```

RoxyLib 会自动完成：扫描规则 → 加载语言 → 读取配置 → 接线（值变更自动防抖保存、快捷键触发改值）→ 注册进 GUI。

### 4. `Info.json`

```json
{
	"Id": "MyMod",
	"DisplayName": "My Mod",
	"Author": "You",
	"Version": "0.1.0",
	"ManagerVersion": "0.28.0",
	"AssemblyName": "MyMod.dll",
	"EntryMethod": "MyMod.Main.Setup",
	"Requirements": [ "RoxyLib" ]
}
```

### 5. 部署

```
<游戏根目录>\Mods\RoxyLib\
<游戏根目录>\Mods\MyMod\        (MyMod.dll + Info.json + lang/)
```

UMM 会先加载 RoxyLib，再按 `Requirements` 加载你的 mod。

---

## 规则类型参考

| 类型 | C# 字段类型 | 注解要求 | GUI 控件 |
|---|---|---|---|
| `Switch` | `bool` | 无 | 开关 + 快捷键绑定/清除 |
| `SliderInt` | `int` / `long` / 等整型 | `Min`、`Max` | 滑块 + 输入框 |
| `SliderFloat` | `float` / `double` / `decimal` | `Min`、`Max` | 滑块 + 输入框 |
| `Options` | 任意枚举 | 无 | 下拉框（库级 RoxyDropdown） |
| `Color` | `UnityEngine.Color` | 无 | RGBA 四条滑块 + 输入框 + 实时色块 |
| `String` | `string` | 无 | 文本输入框 |

> 字段值即**初始默认值**；每条规则行尾的 ↺ 按钮可恢复该默认值。

---

## 配置持久化

每个 mod 一个 `config.json`，存放在 mod 目录下：

```json
{
	"ModId": "RoxyExample",
	"Rules": {
		"Gameplay.Speed": "1.25",
		"Visual.ShowStats": "true",
		"General.Language": "zh_cn"
	},
	"Keybinds": {
		"General.EnableFeature": "LeftControl+K"
	}
}
```

- 规则值变更后 **0.5s 防抖自动保存**（也有 `OnSaveGUI` 兜底）。
- 写入为**原子写**（`.tmp` + 移动），避免中断损坏。
- 配置读取优先级：**config 最优先**（持久化过的值/快捷键以 config 为准）。

---

## 本地化

- 语言是 RoxyLib 自身注册的一条 Options 规则（`General.Language`），在设置面板里通过下拉切换。
- 支持：`en-us`、`zh-cn`、`ja-jp`、`ko-kr`。
- 语言文件放在 mod 目录的 `lang\` 下，文件名即语言代码（如 `zh-cn.json`）。

**键格式**：

| 用途 | 键 |
|---|---|
| 规则名 | `{ModId}.{Category}.{RuleName}` |
| 规则描述 | `{ModId}.{Category}.{RuleName}.Desc` |
| 分类标题 | `{ModId}.Category.{Category}` |

```json
// lang/zh-cn.json
{
	"MyMod.Category.Gameplay": "玩法",
	"MyMod.Gameplay.Speed": "全局速度",
	"MyMod.Gameplay.Speed.Desc": "整体播放速度倍率"
}
```

**回退链**：当前语言 → `en-us` → 代码里的 fallback 文本。缺失翻译不会报错。

> RoxyLib 自身的窗口/通用文案使用 `RoxyLib.*` 前缀键（`RoxyLib.Window.Title`、`RoxyLib.Left.Title`、`RoxyLib.Search.Placeholder` 等）。

---

## 快捷键

- 仅 **Switch** 规则拥有快捷键。
- **无默认快捷键**：所有 Switch 默认 `None`，由用户在 GUI 里绑定。
- 点击快捷键按钮进入"请按键"捕获态：按任意主键（支持 Ctrl/Shift/Alt 组合），按 `Esc` 取消。
- 快捷键为 `None` 时，行尾的 ✕ 清除按钮自动隐藏；绑定后才出现，用于**设回 None**。
- 冲突策略：**不拒绝、同时触发**（两个 mod 绑同一键会都响应）。
- 快捷键按下 = 切换 bool 值（`SetValue(!current)`，触发既有 `ValueChanged`，不新增事件）。

---

## 设计哲学（重要约定）

1. **任何 mod 都不应有默认快捷键** —— 所有 Switch 初始为 `None`。
2. **config 最优先** —— 持久化过的值/快捷键以 config 为准（含显式 `None`）。
3. **快捷键按下 = 改值** —— 复用既有 `ValueChanged`，不引入新事件。
4. **语言是普通规则** —— 是 RoxyLib 自己注册的 Options 规则，不是特殊机制。
5. **浮点用默认文化存储**（`ToString()`，不考虑德语区等小数分隔符差异）。
6. **Color 含 alpha**，序列化为 `#RRGGBBAA`。
7. **Slider 缺 Min/Max 运行时抛异常**（编译期无法校验）。

---

## 构建与部署（库/开发环境）

### 前置要求

- **Unity 6**（`6000.3.10f1`），Mono 运行时
- **Unity Mod Manager**（`0.32.4.0`，`0Harmony`）
- 目标框架 **net481**
- ADOFAI 游戏路径（在 `Directory.Build.props` 里配置）

### 目录/属性

`Directory.Build.props` 中的关键配置：

```xml
<GameDir>D:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice</GameDir>
<GameManagedDir>$(GameDir)\A Dance of Fire and Ice_Data\Managed</GameManagedDir>
<GameModsDir>$(GameDir)\Mods</GameModsDir>
```

> `CleanDuplicateUnityAssemblies` target 会在构建后清理业务 mod 输出目录里被 SDK 连带复制的游戏程序集副本（`UnityEngine.SharedInternalsModule.dll`、`netstandard.dll`、`dnlib.dll` 等），避免污染 Mods 目录。**不要移除该清理**。

### 构建

用 Visual Studio 打开 `RoxyLibForAdofai.slnx`，重新生成 `RoxyLib` 与 `RoxyExample` 两个项目（输出到各自 `bin\Debug\net481\`）。

### 部署

运行仓库根目录的 `Run.bat`（robocopy 把两个项目的构建输出复制到游戏 `Mods\` 目录，排除 `*.pdb`）：

```
Mods\RoxyLib\      ← RoxyLib\bin\Debug\net481\  (RoxyLib.dll + Info.json + lang/)
Mods\RoxyExample\  ← RoxyExample\bin\Debug\net481\
```

> ⚠️ **重要**：重新部署后请删除 `<Mods\RoxyLib\>` 与 `<Mods\RoxyExample\>` 下的旧 `*.cache` 文件。UMM 按程序集时间戳生成缓存，不清除会加载旧代码。

### 验证清单

- 打开 UMM → 启用 `RoxyLib` → 游戏内 UMM 面板可见"Open Config Screen"按钮
- 打开设置窗口：左栏 mod 列表 + 右栏规则面板，可搜索
- 语言下拉切换，界面即时刷新
- 规则改动后 `config.json` 自动写入；重启后值回填
- Switch 快捷键绑定 / ✕ 清除 / 冲突同触发

---

## License

MIT License
