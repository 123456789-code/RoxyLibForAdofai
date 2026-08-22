using RoxyLib.Rules;

namespace RoxyExample;

[RoxyMod(ModId = "RoxyExample")]
public static class ExampleRules {
	[RoxyRule(
		Category = "Gameplay",
		Min = 0.5f, Max = 3f
	)]
	public static float Speed = 1.0f;

	[RoxyRule(Category = "Visual")]
	public static string TrailStyle = "Glow";

	[RoxyRule(Category = "Visual")]
	public static bool ShowStats = true;

	[RoxyRule(Category = "Input", Min = 0, Max = 99999)]
	public static int MaxCombo = 9999;
}
