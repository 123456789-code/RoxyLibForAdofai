using RoxyLib.Rules;
using RoxyLib.Lang;

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
}
