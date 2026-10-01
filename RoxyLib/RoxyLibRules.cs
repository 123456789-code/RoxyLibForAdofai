using RoxyLib.Attribute;
using RoxyLib.Utils;

namespace RoxyLib;

[RoxyMod(ModId = RoxyLib.MOD_ID)]
public static class RoxyLibRules {
	[RoxyRule(Category = "General", Persistent = false)]
	public static bool OpenSettings = false;

	[RoxyRule(Category = "General")]
	public static LanguageEnum Language = LanguageEnum.EnUs;
}
