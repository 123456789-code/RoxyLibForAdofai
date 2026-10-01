using System;
using System.Numerics;
using ImGuiNET;
using RoxyLib.Gui;
using UnityEngine.Rendering;

namespace RoxyLib.Tests;

internal static class CanvasGeometry {
	internal static unsafe void Run() {
		ImGui.NewFrame();
		ImDrawListPtr draw = ImGui.GetBackgroundDrawList();
		draw.AddRectFilled(new Vector2(1, 2), new Vector2(11, 12), 0xFF123456);
		draw.PushClipRect(new Vector2(10, 20), new Vector2(800, 600));
		for (int i = 0; i < 22000; i++) {
			Vector2 position = new(20 + i % 700, 30 + i % 500);
			draw.AddRectFilled(position, position + new Vector2(3, 4), (uint)(0xFF000000 | i));
		}
		draw.PopClipRect();
		ImGui.Render();
		ImDrawDataPtr data = ImGui.GetDrawData();
		Regression.Check(data.TotalVtxCount > ushort.MaxValue, "stress frame exceeds 16-bit vertex address space");
		// Deliberately exercise nonzero display origin and unequal framebuffer scales.
		data.DisplayPos = new Vector2(7, 11);
		data.FramebufferScale = new Vector2(1.5f, 2);
		CanvasMeshBatch batch = new();
		bool offsets = false, split = false, index_offset = false;
		bool geometry = true, indices_valid = true, clip_valid = true;
		int total = 0;
		for (int n = 0; n < data.CmdListsCount; n++) {
			ImDrawListPtr list = data.CmdLists[n];
			var vertices = (ImDrawVert*)list.VtxBuffer.Data;
			ushort* indices = (ushort*)list.IdxBuffer.Data;
			for (int c = 0; c < list.CmdBuffer.Size; c++) {
				ImDrawCmdPtr command = list.CmdBuffer[c];
				offsets |= command.VtxOffset != 0;
				index_offset |= command.IdxOffset != 0;
				for (int first = 0; first < command.ElemCount; first += batch.Count) {
					batch.Build(data, list, command, first, 1920, 1080);
					split |= first != 0;
					total += batch.Count;
					indices_valid &= batch.Count <= 60000 && batch.Count % 3 == 0;
					clip_valid &= batch.ClipRect.x == (command.ClipRect.X - 7) * 1.5f - 960
						&& batch.ClipRect.y == 540 - (command.ClipRect.W - 11) * 2
						&& batch.ClipRect.width == (command.ClipRect.Z - command.ClipRect.X) * 1.5f
						&& batch.ClipRect.height == (command.ClipRect.W - command.ClipRect.Y) * 2;
					for (int i = 0; i < batch.Count; i++) {
						ImDrawVert expected = vertices[indices[command.IdxOffset + first + i] + command.VtxOffset];
						indices_valid &= batch.Indices[i] == i;
						geometry &= batch.Vertices[i].x == (expected.pos.X - 7) * 1.5f - 960
							&& batch.Vertices[i].y == 540 - (expected.pos.Y - 11) * 2
							&& batch.Uvs[i].x == expected.uv.X && batch.Uvs[i].y == expected.uv.Y
							&& batch.Colors[i].r == (byte)expected.col && batch.Colors[i].g == (byte)(expected.col >> 8)
							&& batch.Colors[i].b == (byte)(expected.col >> 16) && batch.Colors[i].a == (byte)(expected.col >> 24);
					}
				}
			}
		}
		Regression.Check(CanvasMeshBatch.INDEX_FORMAT == IndexFormat.UInt16, "Canvas meshes use 16-bit index format");
		Regression.Check(offsets && index_offset && split, "test exercises vertex offset, index offset and command splitting");
		Regression.Check(indices_valid && total == data.TotalIdxCount, "batches preserve every triangle with local 16-bit indices");
		Regression.Check(geometry, "batch positions, UVs and colors match the original indexed triangles");
		Regression.Check(clip_valid, "clipping uses the same display origin and framebuffer scale as vertices");
	}
}
