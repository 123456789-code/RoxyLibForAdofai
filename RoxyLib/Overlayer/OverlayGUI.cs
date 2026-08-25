using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RoxyLib.Utils;
using RoxyLib.Setting;
using RoxyLib.Overlay;

namespace RoxyLib.Gui;

/// <summary>
/// Overlay 的 HUD 渲染层：订阅 OverlayManager 的信号，负责创建/更新/移除文本。
/// - 单个变动（OverlayInfo.PositionChanged）：只更新那一个 overlay 的文本/字号/颜色/位置。
/// - 结构变化（OverlayManager.UpdateAll）：整体重建（新增/移除/顺序/侧色/显隐变化）。
/// 显示格式：本地化标题 + 冒号 + 内容。
/// </summary>
public sealed class OverlayGUI {
	private RectTransform? Root;          // Overlay 画布根
	private RectTransform? LeftRoot;      // 左上堆叠容器
	private RectTransform? RightRoot;     // 右上堆叠容器

	// 已渲染的文本对象：OverlayInfo → RectTransform
	private readonly Dictionary<OverlayInfo, RectTransform> Rects = [ ];

	public bool Ready { get; private set; }

	public void Initialize() {
		if (Ready)
			return;
		Ready = true;

		EnsureRoot();
		OverlayManager.UpdateAll += OnUpdateAll;
		foreach (var o in OverlayManager.GetOverlays())
			Subscribe(o);

		RebuildAll();
	}

	private void Subscribe(OverlayInfo o) {
		o.TextChanged += (_, text) => OnTextChanged(o, text);
		o.PositionChanged += _ => OnLayoutChanged(o);
	}

	// ---------------- 结构变化：整体重建 ----------------

	private void OnUpdateAll() {
		RebuildAll();
	}

	private void RebuildAll() {
		if (Root == null)
			return;
		ClearAll();
		foreach (var o in OverlayManager.GetOverlays()) {
			CreateOverlay(o);
			Subscribe(o);
		}
		LayoutStacked();
		foreach (var o in OverlayManager.GetOverlays())
			ApplyVisibility(o);
	}

	private void ClearAll() {
		foreach (var rect in Rects.Values) {
			if (rect != null)
				UnityEngine.Object.Destroy(rect.gameObject);
		}
		Rects.Clear();
	}

	// ---------------- 创建单个 overlay 文本 ----------------

	private void CreateOverlay(OverlayInfo o) {
		RectTransform parent = o.OverlayType switch {
			OverlayType.LeftTop => LeftRoot!,
			OverlayType.RightTop => RightRoot!,
			_ => Root!,
		};
		if (parent == null)
			return;

		Text text = UiFactory.CreateText(o.Name, parent, FormatText(o, o.Name), 16, GetOverlayColor(o));
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		text.gameObject.AddComponent<Outline>();

		Rects[o] = text.rectTransform;
	}

	// ---------------- 单个变动：只更新那一个 ----------------

	/// <summary>拼接显示文本：本地化标题 + 冒号 + 内容。标题键为 Overlay.{ModId}.{Name}。</summary>
	private static string FormatText(OverlayInfo o, string content) {
		string title = LanguageManager.Translate($"Overlay.{o.ModId}.{o.Name}", o.Name);
		return $"{title}: {content}";
	}

	private void OnTextChanged(OverlayInfo o, string text) {
		if (Rects.TryGetValue(o, out var rect) && rect != null && rect.GetComponent<Text>() is Text t)
			t.text = FormatText(o, text);
	}

	/// <summary>布局/字号/颜色变化（X/Y/A/S/C 规则变动）：重定位 + 字号 + 颜色。</summary>
	private void OnLayoutChanged(OverlayInfo o) {
		if (!Rects.TryGetValue(o, out var rect) || rect == null)
			return;
		Text text = rect.GetComponent<Text>();
		if (text == null)
			return;

		// 字号（_S 规则：0~65535 → 8~64 像素）
		int size = o.RuleS != null ? Mathf.Clamp((int)(Convert.ToSingle(o.RuleS.GetValue()) / 65535f * 56f) + 8, 8, 64) : 16;
		text.fontSize = size;
		// 颜色
		text.color = GetOverlayColor(o);
		// 位置（AnyPosition 的 _X/_Y/_A）
		if (o.OverlayType == OverlayType.AnyPosition)
			LayoutAnyPosition(o);
	}

	/// <summary>颜色：AnyPosition 用绑定的 RuleC；LeftTop/RightTop 用 RoxyLib 的 LeftTopColor/RightTopColor 统一调配。</summary>
	private Color GetOverlayColor(OverlayInfo o) {
		if (o.OverlayType == OverlayType.LeftTop)
			return GetRoxyLibColor("LeftTopColor");
		if (o.OverlayType == OverlayType.RightTop)
			return GetRoxyLibColor("RightTopColor");
		if (o.RuleC != null)
			return (Color)o.RuleC.GetValue();
		return UiFactory.Theme.Text;
	}

	/// <summary>取 RoxyLib 自身（ModId="RoxyLib"）的某条颜色规则值。</summary>
	private static Color GetRoxyLibColor(string rule_name) {
		foreach (RuleInfo rule in RuleManager.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == rule_name)
				return (Color)rule.GetValue();
		}
		return Color.white;
	}

	private void ApplyVisibility(OverlayInfo o) {
		if (!Rects.TryGetValue(o, out var rect) || rect == null)
			return;
		bool visible = (bool)o.RuleB!.GetValue();
		rect.gameObject.SetActive(visible);
	}

	// ---------------- 布局 ----------------

	private void LayoutStacked() {
		LayoutColumn(LeftRoot, OverlayType.LeftTop);
		LayoutColumn(RightRoot, OverlayType.RightTop);
	}

	private void LayoutColumn(RectTransform? container, OverlayType type) {
		if (container == null)
			return;
		var list = new List<OverlayInfo>();
		foreach (var o in OverlayManager.GetOverlays())
			if (o.OverlayType == type)
				list.Add(o);
		list.Sort((a, b) => (a.Order ?? uint.MaxValue).CompareTo(b.Order ?? uint.MaxValue));

		float y = 0f;
		foreach (var o in list) {
			if (Rects.TryGetValue(o, out var rect) && rect != null) {
				rect.anchorMin = new Vector2(0.5f, 1f);
				rect.anchorMax = new Vector2(0.5f, 1f);
				rect.pivot = new Vector2(0.5f, 1f);
				rect.anchoredPosition = new Vector2(0, -y);
				rect.sizeDelta = new Vector2(0, 24f);
			}
			y += 26f;
		}
	}

	/// <summary>AnyPosition：锚点来自 _A 对齐规则，位置来自 _X/_Y（0~65535 → 屏幕比例）。</summary>
	private void LayoutAnyPosition(OverlayInfo o) {
		if (!Rects.TryGetValue(o, out var rect) || rect == null)
			return;

		var align = o.RuleA != null ? (OverlayAlignment)o.RuleA.GetValue() : OverlayAlignment.Middle;
		Vector2 anchor = AlignmentToAnchor(align);

		float max = 65535f;
		float x = o.RuleX != null ? Convert.ToSingle(o.RuleX.GetValue()) : max * 0.5f;
		float y = o.RuleY != null ? Convert.ToSingle(o.RuleY.GetValue()) : max * 0.5f;

		rect.anchorMin = anchor;
		rect.anchorMax = anchor;
		rect.pivot = anchor;
		rect.anchoredPosition = new Vector2((x / max - anchor.x) * Screen.width, (y / max - anchor.y) * Screen.height);
		rect.sizeDelta = new Vector2(200f, 24f);
	}

	private static Vector2 AlignmentToAnchor(OverlayAlignment a) {
		return a switch {
			OverlayAlignment.Left => new Vector2(0f, 0.5f),
			OverlayAlignment.Middle => new Vector2(0.5f, 0.5f),
			OverlayAlignment.Right => new Vector2(1f, 0.5f),
			_ => new Vector2(0.5f, 0.5f),
		};
	}

	// ---------------- 画布 ----------------

	private void EnsureRoot() {
		if (Root != null)
			return;
		GameObject canvas_go = new("RoxyOverlayCanvas");
		Canvas canvas = canvas_go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10000; // 常驻 HUD，低于设置窗口(30000)
		canvas_go.AddComponent<GraphicRaycaster>();
		UnityEngine.Object.DontDestroyOnLoad(canvas_go);
		Root = canvas_go.GetComponent<RectTransform>();

		LeftRoot = new GameObject("LeftRoot", typeof(RectTransform)).GetComponent<RectTransform>();
		LeftRoot.SetParent(Root, false);
		LeftRoot.anchorMin = new Vector2(0f, 1f);
		LeftRoot.anchorMax = new Vector2(0f, 1f);
		LeftRoot.pivot = new Vector2(0f, 1f);
		LeftRoot.anchoredPosition = new Vector2(8f, -8f);
		LeftRoot.sizeDelta = new Vector2(0f, 0f);

		RightRoot = new GameObject("RightRoot", typeof(RectTransform)).GetComponent<RectTransform>();
		RightRoot.SetParent(Root, false);
		RightRoot.anchorMin = new Vector2(1f, 1f);
		RightRoot.anchorMax = new Vector2(1f, 1f);
		RightRoot.pivot = new Vector2(1f, 1f);
		RightRoot.anchoredPosition = new Vector2(-8f, -8f);
		RightRoot.sizeDelta = new Vector2(0f, 0f);
	}
}
