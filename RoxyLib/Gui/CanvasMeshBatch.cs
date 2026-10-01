using System;
using ImGuiNET;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoxyLib.Gui;

/// <summary>Produces self-contained, 16-bit UI meshes without base-vertex offsets.</summary>
internal sealed class CanvasMeshBatch {
	// Multiple of three, below uGUI's 65,000-vertex boundary.
	internal const int MAX_VERTICES = 60000;
	internal const IndexFormat INDEX_FORMAT = IndexFormat.UInt16;
	internal Vector3[] Vertices = [];
	internal Vector2[] Uvs = [];
	internal Color32[] Colors = [];
	internal ushort[] Indices = [];
	internal int Count { get; private set; }
	internal Rect ClipRect { get; private set; }

	internal unsafe void Build(ImDrawDataPtr data, ImDrawListPtr list, ImDrawCmdPtr command,
		int first_element, int framebuffer_width, int framebuffer_height) {
		int elements = checked((int)command.ElemCount);
		if (elements % 3 != 0 || first_element < 0 || first_element >= elements || first_element % 3 != 0)
			throw new ArgumentException("A draw batch must contain complete triangles.");
		Count = Math.Min(MAX_VERTICES, elements - first_element);
		if ((long)command.IdxOffset + first_element + Count > list.IdxBuffer.Size)
			throw new ArgumentException("ImGui command exceeds its index buffer.");
		if (Vertices.Length < Count) {
			Vertices = new Vector3[Count];
			Uvs = new Vector2[Count];
			Colors = new Color32[Count];
			Indices = new ushort[Count];
			for (int i = 0; i < Count; i++)
				Indices[i] = (ushort)i;
		}
		float scale_x = data.FramebufferScale.X, scale_y = data.FramebufferScale.Y;
		float half_width = framebuffer_width / 2f, half_height = framebuffer_height / 2f;
		float left = (command.ClipRect.X - data.DisplayPos.X) * scale_x;
		float top = (command.ClipRect.Y - data.DisplayPos.Y) * scale_y;
		float right = (command.ClipRect.Z - data.DisplayPos.X) * scale_x;
		float bottom = (command.ClipRect.W - data.DisplayPos.Y) * scale_y;
		ClipRect = new Rect(left - half_width, half_height - bottom, right - left, bottom - top);
		var vertices = (ImDrawVert*)list.VtxBuffer.Data;
		ushort* indices = (ushort*)list.IdxBuffer.Data + command.IdxOffset + first_element;
		for (int i = 0; i < Count; i++) {
			long source = (long)command.VtxOffset + indices[i];
			if (source >= list.VtxBuffer.Size)
				throw new ArgumentException("ImGui index exceeds its vertex buffer.");
			ImDrawVert vertex = vertices[source];
			Vertices[i] = new Vector3((vertex.pos.X - data.DisplayPos.X) * scale_x - half_width,
				half_height - (vertex.pos.Y - data.DisplayPos.Y) * scale_y, 0);
			Uvs[i] = new Vector2(vertex.uv.X, vertex.uv.Y);
			Colors[i] = new Color32((byte)vertex.col, (byte)(vertex.col >> 8), (byte)(vertex.col >> 16), (byte)(vertex.col >> 24));
		}
		// Expand indexed triangles: modest duplication avoids baseVertex and large-list dependencies in CanvasRenderer.
	}
}
