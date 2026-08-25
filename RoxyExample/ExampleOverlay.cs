using RoxyLib.Attribute;
using RoxyLib.Overlay;

namespace RoxyExample;

[RoxyMod(ModId = "RoxyExample")]
public static class ExampleOverlay {
	public static OverlayInfo Overlay1 = new(OverlayType.AnyPosition);

	public static OverlayInfo TimeOverlay = new(OverlayType.LeftTop);
}
