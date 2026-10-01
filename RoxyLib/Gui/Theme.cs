using System.Numerics;
using ImGuiNET;

namespace RoxyLib.Gui;

internal static class Theme {
	internal static readonly Vector4 Background = new(0.035f, 0.047f, 0.046f, 1);
	internal static readonly Vector4 Accent = new(0.48f, 0.90f, 0.79f, 1);
	internal static readonly Vector4 Muted = new(0.62f, 0.70f, 0.68f, 1);
	internal static readonly Vector4 Error = new(1, 0.47f, 0.45f, 1);
	internal static bool Toggle(string id, ref bool value) {
		Vector2 position = ImGui.GetCursorScreenPos();
		Vector2 size = new(48, ImGui.GetFrameHeight());
		ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
		bool changed = ImGui.Button(id, size);
		ImGui.PopStyleColor(3);
		if (changed)
			value = !value;
		float height = 24;
		Vector2 track = position + new Vector2(0, (size.Y - height) / 2);
		ImDrawListPtr draw = ImGui.GetWindowDrawList();
		Vector4 background = value ? Accent : new Vector4(0.26f, 0.34f, 0.31f, 1);
		if (ImGui.IsItemHovered())
			background *= new Vector4(1.15f, 1.15f, 1.15f, 1);
		draw.AddRectFilled(track, track + new Vector2(size.X, height), ImGui.ColorConvertFloat4ToU32(background), height / 2);
		draw.AddCircleFilled(track + new Vector2(value ? size.X - height / 2 : height / 2, height / 2), 9,
			ImGui.ColorConvertFloat4ToU32(value ? new Vector4(0.04f, 0.10f, 0.08f, 1) : new Vector4(0.92f, 0.96f, 0.94f, 1)));
		if (ImGui.IsItemFocused())
			draw.AddRect(position, position + size, ImGui.ColorConvertFloat4ToU32(Accent), 4);
		return changed;
	}
	internal static unsafe void LoadFont(string path) {
		ImFontConfigPtr config = new(ImGuiNative.ImFontConfig_ImFontConfig());
		try {
			config.RasterizerMultiply = 2;
			config.PixelSnapH = true;
			config.GlyphOffset = new Vector2(0, -2);
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
		style.ItemSpacing = new Vector2(12, 8);
		style.FramePadding = new Vector2(10, 8);
		style.CellPadding = new Vector2(6, 8);
		style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
		style.SelectableTextAlign = new Vector2(0, 0.5f);
		style.Colors[(int)ImGuiCol.WindowBg] = Background;
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
