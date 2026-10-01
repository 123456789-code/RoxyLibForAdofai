# 重构记录

原始工作区先提交为 `6b29449`（chore: preserve existing AI changes before refactor），再创建 `codex/rule-registry-gui-refactor`。分支起点包含其他 AI 的原有更改。

## 已确认的范围

- 不兼容旧 API / 配置，允许彻底重做。
- 类型开放注册，同时支持字段推断与显式 TypeId。
- 外部类型同时支持通用控件描述和 ImGui 自定义绘制回调。
- ImGui.NET 全屏设置页，参考 CheryTools 视觉风格，屏蔽底层输入。
- 移除全部旧 Overlay 功能。
- bool 无默认快捷键，同键同时触发。
- OpenSettings 值仅本次运行有效，快捷键持久化。
- 依赖操作限定已批准的项目内清单。未安装系统组件，未部署游戏。
- 遵循用户指定的 E:/code/.editorconfig：UTF-8 BOM、CRLF、制表符、文件范围 namespace、成员 PascalCase、参数/局部变量 snake_case、方法使用语句体。Harmony 注入参数 __result 保留框架规定的名称，并局部说明例外。

## 职责与修复

| 模块 | 当前职责 / 修复 |
| --- | --- |
| RuleType / RuleTypeRegistry | 独立类型解析、校验与编解码；拒绝未知或重复类型，取消固定工厂和静默字符串回退 |
| RuleInfo / NumericRange | 所有值修改共用校验路径；默认值快照、相同值不重复通知、精确整数/decimal、完整有限浮点范围 |
| RuleManager | 限定所属 mod 的字段扫描、重复标识检查、动态规则接入、注册失败回滚 |
| RoxyLib / ModHost | UMM 回调协作、启用/禁用、逐 mod 配置会话；配置先载入再通知业务启用 |
| ConfigStore | 修复首次保存路径；逐条读取容错、未知项保留、原配置保护、临时文件替换、失败保留脏状态 |
| KeybindManager | 统一修饰键、按下边沿、同键批量触发、页面/捕获期间抑制、Esc 取消捕获 |
| LanguageManager | mod 所有权、英文回退链、停用释放 |
| RuleEditorRegistry / RuleEditorSchema | 类型 ID 到编辑器的独立映射；通用组件与自定义回调；输入草稿提交与错误显示 |
| SettingsPage | 全屏导航、搜索、分类、重置、快捷键绑定、保存错误；不拥有规则值 |
| ImGuiHost / InputBlock | 独立 context、字体和 Unity 渲染、鼠标/IME、底层输入阻断 |
| RoxyExample | 字段注册与两种外部 GUI 的完整可编译示例 |

## 验证和边界

构建为 0 警告、0 错误，70 项断言通过。无额外测试依赖的回归程序覆盖类型注册/释放、值校验与通知、区域设置、数值边界、颜色、同键冲突、按住/捕获抑制、输入草稿、配置保护和替换、动态注册、重新启用默认值、注册回滚、翻译隔离。

测试实际加载项目内 cimgui，创建中文字体图集，绘制全屏设置页和两种外部编辑器，并在 1280×800 和 900×640 生成预览。预览是 ImGui 绘制数据的软件栅格化结果，不是游戏内截图。

Unity 侧渲染、RDInput/Harmony 拦截、UMM 共存、IME 和光标行为已编译，但尚未在运行中的游戏验证。其他 mod 直接轮询 Unity Input 时需配合 IsInputBlocked。自定义 GUI 当前不支持外部纹理和原生 draw callback。

回退步骤和生成物位置见 dependency-plan.md。未运行部署脚本。
