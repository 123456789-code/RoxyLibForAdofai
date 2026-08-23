using RoxyLib.Rules;
using RoxyLib.Lang;
using UnityEngine;

namespace RoxyLib;

/// <summary>
/// RoxyLib 自身注册的规则
/// </summary>
[RoxyMod(ModId = "RoxyLib")]
public static class RoxyLibRules {
	[RoxyRule(Category = "General")]
	public static bool OpenSettings = false;

	[RoxyRule(Category = "General")]
	public static LanguageEnum Language = LanguageEnum.en_us;

	[RoxyRule(Category = "Developer", Min = 0, Max = 3)]
	public static int DebugLogLevel = 1;

	// 主题颜色

	// ---- 主色 #66CCFF ----
	
	[RoxyRule(Category = "Theme")]
	public static Color Accent = new Color(0.40f, 0.80f, 1.00f, 1f);         // 强调色（开关/滑块/选中）

	[RoxyRule(Category = "Theme")]
	public static Color AccentDim = new Color(0.40f, 0.80f, 1.00f, 0.16f);   // 强调色淡（选中行底）

	[RoxyRule(Category = "Theme")]
	public static Color AccentStrong = new Color(0.20f, 0.55f, 0.78f, 1f);   // 深强调（悬停/边框）

	// ---- 可爱风格配色（明亮粉彩系） ----

	[RoxyRule(Category = "Theme")]
	public static Color Background = new Color(0.98f, 0.96f, 0.94f, 1f);     // 背景：奶油白（暖白）

	[RoxyRule(Category = "Theme")]
	public static Color Panel = new Color(0.95f, 0.88f, 0.92f, 1f);          // 面板：淡粉（左栏/右栏）

	[RoxyRule(Category = "Theme")]
	public static Color PanelLight = new Color(1.00f, 0.95f, 0.97f, 1f);     // 控件底：粉白（按钮/输入框/卡片）

	[RoxyRule(Category = "Theme")]
	public static Color PanelHover = new Color(0.85f, 0.90f, 1.00f, 1f);     // 悬停高亮：淡蓝（呼应主色）

	[RoxyRule(Category = "Theme")]
	public static Color Text = new Color(0.20f, 0.18f, 0.25f, 1f);           // 主文字：深灰紫（清晰柔和）

	[RoxyRule(Category = "Theme")]
	public static Color TextDim = new Color(0.50f, 0.48f, 0.55f, 1f);        // 次要文字：暖灰（占位/说明）
}
