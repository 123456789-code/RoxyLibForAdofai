using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using RoxyLib.Setting;
using RoxyLib.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using Vector2N = System.Numerics.Vector2;

namespace RoxyLib.Gui;

/// <summary>Owns an isolated ImGui context, input feed and Unity canvas renderer. No files are written by ImGui.</summary>
internal sealed class ImGuiHost : MonoBehaviour {
	private IntPtr Context;
	private IntPtr NativeLibrary;
	private Texture2D? Atlas;
	private Material? Material;
	private Canvas? Canvas;
	private CanvasRenderer? Backdrop;
	private Mesh? BackdropMesh;
	private int BackdropWidth;
	private int BackdropHeight;
	private readonly SettingsPage Page = new();
	private readonly List<CanvasRenderer> Renderers = [];
	private readonly List<Mesh> Meshes = [];
	private readonly CanvasMeshBatch Batch = new();
	private static readonly Dictionary<ImGuiKey, KeyCode> KeyMap = CreateKeyMap();
	private CursorLockMode PreviousLock;
	private bool PreviousCursor;
	private IMECompositionMode PreviousIme;
	private float DisplayScale = 1;
	private ImeCallback? UpdateIme;
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void ImeCallback(IntPtr context, IntPtr viewport, IntPtr data);
	public bool IsOpen { get; private set; }

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibraryEx(string file_name, IntPtr reserved, uint flags);
	[DllImport("kernel32.dll")]
	private static extern bool FreeLibrary(IntPtr module);

	internal static ImGuiHost Create(string mod_path) {
		GameObject root = new("RoxyLib.Settings", typeof(RectTransform));
		DontDestroyOnLoad(root);
		ImGuiHost host = root.AddComponent<ImGuiHost>();
		try { host.Initialize(mod_path); return host; }
		catch { Destroy(root); throw; }
	}
	private unsafe void Initialize(string mod_path) {
		string native = Path.GetFullPath(Path.Combine(mod_path, "cimgui.dll"));
		if (!File.Exists(native))
			throw new FileNotFoundException("Deploy cimgui.dll beside RoxyLib.dll.", native);
		NativeLibrary = LoadLibraryEx(native, IntPtr.Zero, 8);
		if (NativeLibrary == IntPtr.Zero)
			throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), native);
		IntPtr previous = ImGui.GetCurrentContext();
		try {
			if (!ImGui.GetVersion().StartsWith("1.91.6", StringComparison.Ordinal))
				throw new InvalidOperationException("An incompatible cimgui library is already loaded.");
			Context = ImGui.CreateContext();
			ImGui.SetCurrentContext(Context);
			ImGuiIOPtr io = ImGui.GetIO();
			io.NativePtr->IniFilename = null;
			io.NativePtr->LogFilename = null;
			io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
			io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
			string font_path = Path.Combine(mod_path, "Resources", "Fonts", "NotoSansSC.ttf");
			if (!File.Exists(font_path))
				throw new FileNotFoundException("Deploy the bundled font.", font_path);
			Theme.LoadFont(font_path);
			UpdateIme = SetImePosition;
			ImGui.GetPlatformIO().Platform_SetImeDataFn = Marshal.GetFunctionPointerForDelegate(UpdateIme);
			io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bpp);
			if (width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
				throw new InvalidOperationException("Font atlas exceeds the device limit.");
			Atlas = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			Atlas.LoadRawTextureData(pixels, checked(width * height * bpp));
			Atlas.Apply(false, true);
			io.Fonts.SetTexID(new IntPtr(1));
			io.Fonts.ClearTexData();
			var shader = Shader.Find("UI/Default");
			if (shader == null)
				throw new InvalidOperationException("Unity UI/Default shader unavailable.");
			Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = Atlas };
			Canvas = gameObject.AddComponent<Canvas>();
			Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			Canvas.sortingOrder = short.MaxValue;
			Canvas.enabled = false;
			GameObject backdrop = new("FullscreenBackground", typeof(RectTransform), typeof(CanvasRenderer));
			backdrop.transform.SetParent(transform, false);
			Backdrop = backdrop.GetComponent<CanvasRenderer>();
			BackdropMesh = new Mesh { indexFormat = CanvasMeshBatch.INDEX_FORMAT };
			Backdrop.SetMaterial(Material, Texture2D.whiteTexture);
			Backdrop.SetColor(new Color(Theme.Background.X, Theme.Background.Y, Theme.Background.Z, 1));
			UpdateBackdrop();
			Theme.Apply();
		}
		finally { ImGui.SetCurrentContext(previous); }
	}

	internal bool SetOpen(bool open, bool discard = false) {
		if (IsOpen == open)
			return true;
		if (!open && !discard && !Page.CommitPending())
			return false;
		IsOpen = open;
		if (Canvas != null)
			Canvas.enabled = open;
		Page.ResetTransientState();
		if (open) {
			PreviousLock = Cursor.lockState;
			PreviousCursor = Cursor.visible;
			PreviousIme = Input.imeCompositionMode;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			Input.imeCompositionMode = IMECompositionMode.On;
			if (EventSystem.current != null)
				EventSystem.current.SetSelectedGameObject(null);
		}
		else {
			Cursor.lockState = PreviousLock;
			Cursor.visible = PreviousCursor;
			Input.imeCompositionMode = PreviousIme;
			InputBlock.HoldClosingFrame();
			ClearRenderers();
		}
		if (Context != IntPtr.Zero) {
			IntPtr previous = ImGui.GetCurrentContext();
			try { ImGui.SetCurrentContext(Context); ImGui.GetIO().ClearInputKeys(); ImGui.GetIO().ClearInputMouse(); }
			finally { ImGui.SetCurrentContext(previous); }
		}
		return true;
	}
	private void LateUpdate() {
		if (!IsOpen || Context == IntPtr.Zero)
			return;
		IntPtr previous = ImGui.GetCurrentContext();
		bool frame = false;
		try {
			ImGui.SetCurrentContext(Context);
			float scale = Mathf.Clamp(Screen.height / 900f, 0.75f, 1.75f);
			FeedInput(scale);
			ImGui.NewFrame();
			frame = true;
			Page.Draw();
			// ImGui writes edits before rendering; one frame remains blocked when closing.
			ImGui.Render();
			frame = false;
			if (IsOpen)
				Render(ImGui.GetDrawData());
		}
		catch (Exception ex) {
			if (frame)
				ImGui.EndFrame();
			RoxyLog.Error("Settings GUI", ex);
			SetOpen(false, discard: true);
			RoxyLib.SetSettingsOpen(false);
		}
		finally { ImGui.SetCurrentContext(previous); }
	}
	private void FeedInput(float scale) {
		DisplayScale = scale;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		ImGuiIOPtr io = ImGui.GetIO();
		io.DisplaySize = new Vector2N(Screen.width / scale, Screen.height / scale);
		io.DisplayFramebufferScale = new Vector2N(scale, scale);
		io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
		io.AddFocusEvent(Application.isFocused);
		Vector3 mouse = Input.mousePosition;
		io.AddMousePosEvent(mouse.x / scale, (Screen.height - mouse.y) / scale);
		for (int i = 0; i < 3; i++)
			io.AddMouseButtonEvent(i, Input.GetMouseButton(i));
		io.AddMouseWheelEvent(Input.mouseScrollDelta.x, Input.mouseScrollDelta.y);
		foreach (KeyValuePair<ImGuiKey, KeyCode> key in KeyMap)
			io.AddKeyEvent(key.Key, Input.GetKey(key.Value));
		KeyModifiers modifiers = KeybindManager.ReadModifiers();
		io.AddKeyEvent(ImGuiKey.ModCtrl, (modifiers & KeyModifiers.Control) != 0);
		io.AddKeyEvent(ImGuiKey.ModShift, (modifiers & KeyModifiers.Shift) != 0);
		io.AddKeyEvent(ImGuiKey.ModAlt, (modifiers & KeyModifiers.Alt) != 0);
		string text = Input.inputString;
		if (!string.IsNullOrEmpty(text))
			io.AddInputCharactersUTF8(text);
	}
	private unsafe void SetImePosition(IntPtr context, IntPtr viewport, IntPtr data) {
		// ImGui reports the caret in top-left coordinates, as expected by Unity's IME API.
		ImGuiPlatformImeDataPtr ime = new(data);
		if (ime.WantVisible)
			Input.compositionCursorPos = new Vector2(ime.InputPos.X * DisplayScale, (ime.InputPos.Y + ime.InputLineHeight) * DisplayScale);
	}
	private static Dictionary<ImGuiKey, KeyCode> CreateKeyMap() {
		Dictionary<ImGuiKey, KeyCode> map = new() {
			[ImGuiKey.Tab] = KeyCode.Tab,
			[ImGuiKey.LeftArrow] = KeyCode.LeftArrow,
			[ImGuiKey.RightArrow] = KeyCode.RightArrow,
			[ImGuiKey.UpArrow] = KeyCode.UpArrow,
			[ImGuiKey.DownArrow] = KeyCode.DownArrow,
			[ImGuiKey.PageUp] = KeyCode.PageUp,
			[ImGuiKey.PageDown] = KeyCode.PageDown,
			[ImGuiKey.Home] = KeyCode.Home,
			[ImGuiKey.End] = KeyCode.End,
			[ImGuiKey.Insert] = KeyCode.Insert,
			[ImGuiKey.Delete] = KeyCode.Delete,
			[ImGuiKey.Backspace] = KeyCode.Backspace,
			[ImGuiKey.Space] = KeyCode.Space,
			[ImGuiKey.Enter] = KeyCode.Return,
			[ImGuiKey.Escape] = KeyCode.Escape,
			[ImGuiKey.LeftCtrl] = KeyCode.LeftControl,
			[ImGuiKey.RightCtrl] = KeyCode.RightControl,
			[ImGuiKey.LeftShift] = KeyCode.LeftShift,
			[ImGuiKey.RightShift] = KeyCode.RightShift,
			[ImGuiKey.LeftAlt] = KeyCode.LeftAlt,
			[ImGuiKey.RightAlt] = KeyCode.RightAlt,
			[ImGuiKey.LeftSuper] = KeyCode.LeftCommand,
			[ImGuiKey.RightSuper] = KeyCode.RightCommand,
			[ImGuiKey.KeypadEnter] = KeyCode.KeypadEnter
		};
		for (int i = 0; i < 26; i++)
			map[ImGuiKey.A + i] = KeyCode.A + i;
		for (int i = 0; i < 10; i++)
			map[ImGuiKey._0 + i] = KeyCode.Alpha0 + i;
		for (int i = 0; i < 12; i++)
			map[ImGuiKey.F1 + i] = KeyCode.F1 + i;
		return map;
	}
	private void Render(ImDrawDataPtr data) {
		UpdateBackdrop();
		int used = 0;
		for (int list_index = 0; list_index < data.CmdListsCount; list_index++) {
			ImDrawListPtr list = data.CmdLists[list_index];
			for (int command_index = 0; command_index < list.CmdBuffer.Size; command_index++) {
				ImDrawCmdPtr command = list.CmdBuffer[command_index];
				if (command.UserCallback != IntPtr.Zero || command.ElemCount == 0 || command.TextureId != new IntPtr(1))
					continue;
				for (int first = 0; first < command.ElemCount; first += Batch.Count) {
					Batch.Build(data, list, command, first, Screen.width, Screen.height);
					if (Batch.ClipRect.width <= 0 || Batch.ClipRect.height <= 0)
						continue;
					if (used == Renderers.Count) {
						GameObject child = new("DrawCommand", typeof(RectTransform), typeof(CanvasRenderer));
						child.transform.SetParent(transform, false);
						Renderers.Add(child.GetComponent<CanvasRenderer>());
						Mesh mesh = new() { indexFormat = CanvasMeshBatch.INDEX_FORMAT };
						mesh.MarkDynamic();
						Meshes.Add(mesh);
					}
					Mesh target = Meshes[used];
					target.Clear();
					target.SetVertices(Batch.Vertices, 0, Batch.Count);
					target.SetUVs(0, Batch.Uvs, 0, Batch.Count);
					target.SetColors(Batch.Colors, 0, Batch.Count);
					target.SetIndices(Batch.Indices, 0, Batch.Count, MeshTopology.Triangles, 0);
					CanvasRenderer renderer = Renderers[used++];
					renderer.EnableRectClipping(Batch.ClipRect);
					renderer.SetMaterial(Material, Atlas);
					renderer.SetColor(Color.white);
					renderer.SetMesh(target);
				}
			}
		}
		for (int i = used; i < Renderers.Count; i++)
			Renderers[i].Clear();
	}
	private void ClearRenderers() {
		foreach (CanvasRenderer renderer in Renderers)
			if (renderer != null)
				renderer.Clear();
	}
	private void UpdateBackdrop() {
		if (BackdropMesh == null || Backdrop == null || (BackdropWidth == Screen.width && BackdropHeight == Screen.height))
			return;
		BackdropWidth = Screen.width;
		BackdropHeight = Screen.height;
		// Physical pixel bounds are independent of ImGui's rounded logical window size.
		BackdropMesh.vertices = CanvasMeshBatch.BackdropVertices(BackdropWidth, BackdropHeight);
		BackdropMesh.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
		BackdropMesh.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
		BackdropMesh.SetIndices(new ushort[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0);
		Backdrop.SetMesh(BackdropMesh);
	}
	private void OnDestroy() {
		SetOpen(false, discard: true);
		foreach (Mesh mesh in Meshes)
			Destroy(mesh);
		if (BackdropMesh != null)
			Destroy(BackdropMesh);
		if (Material != null)
			Destroy(Material);
		if (Atlas != null)
			Destroy(Atlas);
		if (Context != IntPtr.Zero) {
			IntPtr previous = ImGui.GetCurrentContext();
			ImGui.DestroyContext(Context);
			ImGui.SetCurrentContext(previous == Context ? IntPtr.Zero : previous);
			Context = IntPtr.Zero;
		}
		if (NativeLibrary != IntPtr.Zero) { FreeLibrary(NativeLibrary); NativeLibrary = IntPtr.Zero; }
	}
}
