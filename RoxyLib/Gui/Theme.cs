using System.Numerics;
using ImGuiNET;

namespace RoxyLib.Gui;

internal static class Theme {
	internal static readonly Vector4 Accent = new(0.48f, 0.90f, 0.79f, 1);
	internal static readonly Vector4 Muted = new(0.62f, 0.70f, 0.68f, 1);
	internal static readonly Vector4 Error = new(1, 0.47f, 0.45f, 1);
	internal static unsafe void LoadFont(string path) {
		ImFontConfigPtr config = new(ImGuiNative.ImFontConfig_ImFontConfig());
		try {
			config.RasterizerMultiply = 2;
			config.PixelSnapH = true;
			ImGuiIOPtr io = ImGui.GetIO();
			io.Fonts.AddFontFromFileTTF(path, 20, config, io.Fonts.GetGlyphRangesChineseFull());
		}
		finally { config.Destroy(); }
	}
	internal static void Apply() {
		ImGui.StyleColorsDark();
		ImGuiStylePtr style = ImGui.GetStyle();
		style.WindowRounding = 0;
		style.ChildRounding = 3;
		style.FrameRounding = 3;
		style.WindowBorderSize = 0;
		style.ChildBorderSize = 1;
		style.FrameBorderSize = 0;
		style.ItemSpacing = new Vector2(12, 10);
		style.FramePadding = new Vector2(9, 6);
		style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.035f, 0.047f, 0.046f, 1);
		style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.044f, 0.060f, 0.056f, 1);
		style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.06f, 0.085f, 0.075f, 1);
		style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.08f, 0.11f, 0.10f, 1);
		style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.12f, 0.21f, 0.18f, 1);
		style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.16f, 0.28f, 0.23f, 1);
		style.Colors[(int)ImGuiCol.Button] = new Vector4(0.10f, 0.18f, 0.15f, 1);
		style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.18f, 0.35f, 0.29f, 1);
		style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.23f, 0.46f, 0.37f, 1);
		style.Colors[(int)ImGuiCol.Header] = new Vector4(0.13f, 0.27f, 0.22f, 1);
		style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.17f, 0.34f, 0.28f, 1);
		style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.21f, 0.43f, 0.35f, 1);
		style.Colors[(int)ImGuiCol.CheckMark] = Accent;
		style.Colors[(int)ImGuiCol.SliderGrab] = Accent;
		style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.66f, 1, 0.88f, 1);
		style.Colors[(int)ImGuiCol.Text] = new Vector4(0.91f, 0.95f, 0.94f, 1);
		style.Colors[(int)ImGuiCol.TextDisabled] = Muted;
		style.Colors[(int)ImGuiCol.Border] = new Vector4(0.17f, 0.24f, 0.22f, 1);
		style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.15f, 0.21f, 0.19f, 1);
	}
}
