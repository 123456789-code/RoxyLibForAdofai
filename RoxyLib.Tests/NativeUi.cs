using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using RoxyLib.Gui;
using RoxyLib.Setting;
using RoxyLib.Utils;
using Vector2 = System.Numerics.Vector2;

namespace RoxyLib.Tests;

internal static class NativeUi {
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibraryEx(string name, IntPtr reserved, uint flags);
	[DllImport("kernel32.dll")]
	private static extern bool FreeLibrary(IntPtr handle);

	internal static unsafe void Run(string root, string output) {
		IntPtr library = LoadLibraryEx(Path.Combine(output, "cimgui.dll"), IntPtr.Zero, 8);
		if (library == IntPtr.Zero)
			throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
		IntPtr previous = ImGui.GetCurrentContext();
		IntPtr context = IntPtr.Zero;
		try {
			Regression.Check(ImGui.GetVersion().StartsWith("1.91.6", StringComparison.Ordinal), "native and managed ImGui versions match");
			context = ImGui.CreateContext();
			ImGui.SetCurrentContext(context);
			ImGuiIOPtr io = ImGui.GetIO();
			io.NativePtr->IniFilename = null;
			io.NativePtr->LogFilename = null;
			io.DisplaySize = new Vector2(1280, 800);
			io.DeltaTime = 1f / 60;
			Theme.LoadFont(Path.Combine(output, "Resources", "Fonts", "NotoSansSC.ttf"));
			io.Fonts.GetTexDataAsRGBA32(out IntPtr atlas, out int width, out int height, out int bpp);
			byte[] pixels = new byte[width * height * bpp];
			Marshal.Copy(atlas, pixels, 0, pixels.Length);
			io.Fonts.SetTexID(new IntPtr(1));
			Theme.Apply();
			RoxyExample.CustomRuleTypes.Register();
			RoxyLib.Activate("RoxyExample", "Roxy Example", Path.Combine(root, "example"), typeof(RoxyExample.ExampleRules).Assembly);
			LanguageManager.Load(RoxyLib.MOD_ID, output);
			string configuration = new DirectoryInfo(output).Parent!.Name;
			LanguageManager.Load("RoxyExample", Path.GetFullPath(Path.Combine(output, "../../../../RoxyExample/bin", configuration, "net481")));
			RoxyLibRules.Language = LanguageEnum.ZhCn;
			SettingsPage page = new();
			for (int i = 0; i < 3; i++) { ImGui.NewFrame(); page.Draw(); ImGui.Render(); }
			Regression.Check(ImGui.GetDrawData().TotalVtxCount > 1000, "fullscreen GUI produces draw data");
			string preview = Path.Combine(root, "settings-preview.png");
			DrawToFile(ImGui.GetDrawData(), pixels, width, height, preview, 1280, 800);
			// Also exercise both external editor paths independent of scrolling/visibility.
			ImGui.NewFrame();
			ImGui.Begin("external-editor-test");
			RuleInfo range = RuleManager.Find("RoxyExample", "Extensions.Range")!;
			RuleInfo progress = RuleManager.Find("RoxyExample", "Extensions.Progress")!;
			RuleEditorRegistry.Draw(range, new EditorState());
			ImGui.PushID("custom");
			RuleEditorRegistry.Draw(progress, new EditorState());
			ImGui.PopID();
			ImGui.End();
			ImGui.Render();
			Regression.Check(range.Error == null && progress.Error == null, "schema and custom drawers execute");
			io.DisplaySize = new Vector2(900, 640);
			for (int i = 0; i < 3; i++) { ImGui.NewFrame(); page.Draw(); ImGui.Render(); }
			DrawToFile(ImGui.GetDrawData(), pixels, width, height, Path.Combine(root, "settings-compact.png"), 900, 640);
			Console.WriteLine("PREVIEW: " + preview);
			RoxyLib.Deactivate("RoxyExample");
		}
		finally {
			if (context != IntPtr.Zero)
				ImGui.DestroyContext(context);
			ImGui.SetCurrentContext(previous);
			FreeLibrary(library);
		}
	}

	// CPU rasterization of the real ImGui triangles and atlas, for inspection without starting Unity.
	private static unsafe void DrawToFile(ImDrawDataPtr data, byte[] atlas, int atlas_width, int atlas_height,
		string path, int width, int height) {
		byte[] image = new byte[width * height * 4];
		for (int i = 3; i < image.Length; i += 4)
			image[i] = 255;
		for (int n = 0; n < data.CmdListsCount; n++) {
			ImDrawListPtr list = data.CmdLists[n];
			var vertices = (ImDrawVert*)list.VtxBuffer.Data;
			ushort* indices = (ushort*)list.IdxBuffer.Data;
			for (int c = 0; c < list.CmdBuffer.Size; c++) {
				ImDrawCmdPtr command = list.CmdBuffer[c];
				if (command.UserCallback != IntPtr.Zero)
					continue;
				int begin = (int)command.IdxOffset, end = begin + (int)command.ElemCount;
				for (int i = begin; i + 2 < end; i += 3) {
					ImDrawVert a = vertices[indices[i] + command.VtxOffset];
					ImDrawVert b = vertices[indices[i + 1] + command.VtxOffset];
					ImDrawVert v = vertices[indices[i + 2] + command.VtxOffset];
					float determinant = (b.pos.Y - v.pos.Y) * (a.pos.X - v.pos.X) + (v.pos.X - b.pos.X) * (a.pos.Y - v.pos.Y);
					if (Math.Abs(determinant) < 0.00001f)
						continue;
					int x0 = Math.Max(0, (int)Math.Max(command.ClipRect.X, Math.Floor(Math.Min(a.pos.X, Math.Min(b.pos.X, v.pos.X)))));
					int x1 = Math.Min(width, (int)Math.Min(command.ClipRect.Z, Math.Ceiling(Math.Max(a.pos.X, Math.Max(b.pos.X, v.pos.X)))));
					int y0 = Math.Max(0, (int)Math.Max(command.ClipRect.Y, Math.Floor(Math.Min(a.pos.Y, Math.Min(b.pos.Y, v.pos.Y)))));
					int y1 = Math.Min(height, (int)Math.Min(command.ClipRect.W, Math.Ceiling(Math.Max(a.pos.Y, Math.Max(b.pos.Y, v.pos.Y)))));
					for (int y = y0; y < y1; y++)
						for (int x = x0; x < x1; x++) {
							float u = ((b.pos.Y - v.pos.Y) * (x + 0.5f - v.pos.X) + (v.pos.X - b.pos.X) * (y + 0.5f - v.pos.Y)) / determinant;
							float w = ((v.pos.Y - a.pos.Y) * (x + 0.5f - v.pos.X) + (a.pos.X - v.pos.X) * (y + 0.5f - v.pos.Y)) / determinant;
							float t = 1 - u - w;
							if (u < 0 || w < 0 || t < 0)
								continue;
							int tx = Math.Max(0, Math.Min(atlas_width - 1, (int)((u * a.uv.X + w * b.uv.X + t * v.uv.X) * atlas_width)));
							int ty = Math.Max(0, Math.Min(atlas_height - 1, (int)((u * a.uv.Y + w * b.uv.Y + t * v.uv.Y) * atlas_height)));
							int texel = (ty * atlas_width + tx) * 4;
							float alpha = (u * (a.col >> 24) + w * (b.col >> 24) + t * (v.col >> 24)) / 255 * atlas[texel + 3] / 255;
							int target = (y * width + x) * 4;
							for (int channel = 0; channel < 3; channel++) {
								int shift = channel * 8;
								float color = u * ((a.col >> shift) & 255) + w * ((b.col >> shift) & 255) + t * ((v.col >> shift) & 255);
								int bgra = 2 - channel;
								image[target + bgra] = (byte)(color * atlas[texel + channel] / 255 * alpha + image[target + bgra] * (1 - alpha));
							}
						}
				}
			}
		}
		using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
		BitmapData locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try { Marshal.Copy(image, 0, locked.Scan0, image.Length); }
		finally { bitmap.UnlockBits(locked); }
		bitmap.Save(path, ImageFormat.Png);
	}
}
