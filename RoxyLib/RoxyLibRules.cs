using UnityEngine;
using RoxyLib.Utils;
using RoxyLib.Attribute;

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

	[RoxyRule(Category = "Overlay")]
	public static bool OpenOverlay = false;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static uint LeftTopSize = 10;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static uint RightTopSize = 10;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static Color LeftTopColor = new(255f, 255f, 255f, 255f);

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static Color RightTopColor = new(255f, 255f, 255f, 255f);

	[RoxyRule(Category = "Developer", Min = 0, Max = 3)]
	public static int DebugLogLevel = 1;
}
