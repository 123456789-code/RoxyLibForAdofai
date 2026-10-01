using RoxyLib.Attribute;
using UnityEngine;

namespace RoxyExample;

public enum TrailStyle { Glow, Stripes, Fade }

[RoxyMod(ModId = "RoxyExample")]
public static class ExampleRules {
	[RoxyRule(Category = "Gameplay", Min = 0.5f, Max = 3f)]
	public static float Speed = 1;
	[RoxyRule(Category = "Gameplay", Min = 0, Max = 99999)]
	public static int MaxCombo = 9999;
	[RoxyRule(Category = "Visual")]
	public static TrailStyle Trail = TrailStyle.Glow;
	[RoxyRule(Category = "Visual")]
	public static bool ShowStats = true;
	[RoxyRule(Category = "Visual")]
	public static Color TrailColor = new(0.45f, 0.9f, 0.8f, 1);
	[RoxyRule(Category = "Visual")]
	public static string Label = "Hello, Roxy!";
	[RoxyRule(Category = "Extensions")]
	public static RangeValue Range = new(10, 80);
	[RoxyRule(Category = "Extensions", TypeId = "example/percentage")]
	public static Percentage Progress = new(0.5);
}
