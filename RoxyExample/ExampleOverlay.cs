using RoxyLib.Attribute;
using RoxyLib.Overlay;

namespace RoxyExample;

[RoxyMod(ModId = "RoxyExample")]
public static class ExampleOverlay {
	public static OverlayInfo Overlay1 = new(OverlayType.AnyPosition);

	[RoxyRule(Category = "Overlay")]
	public static bool Overlay1_B = true;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static int Overlay1_X = 1;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static int Overlay1_Y = 1;

	[RoxyRule(Category = "Overlay")]
	public static OverlayAlignment Overlay1_A = OverlayAlignment.MiddleMiddle;

	[RoxyRule(Category = "Overlay", Min = 0, Max = 65535)]
	public static uint Overlay1_S = 10;

	[RoxyRule(Category = "Overlay")]
	public static UnityEngine.Color Overlay1_C = new(255f, 255f, 255f, 255f);

	public static OverlayInfo TimeOverlay = new(OverlayType.LeftTop);

	[RoxyRule(Category = "Overlay")]
	public static bool TimeOverlay_B = true;
}
