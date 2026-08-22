using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoxyLib.Gui;

/// <summary>
/// RLA 设置界面的配色与尺寸主题。
/// 所有控件工厂方法统一从这里取值，改主题只需改这一处。
/// </summary>
public sealed class RoxyTheme {
	// ---- 颜色 ----
	public Color Background = new Color(0.09f, 0.10f, 0.12f, 1f); // 窗口整体背景
	public Color Panel = new Color(0.15f, 0.16f, 0.19f, 1f); // 面板（左栏/顶栏）
	public Color PanelLight = new Color(0.22f, 0.23f, 0.27f, 1f); // 控件底（按钮/输入框）
	public Color Accent = new Color(106f / 255f, 148f / 255f, 210f / 255f, 1f); // 强调色（选中/勾选）
	public Color AccentDim = new Color(106f / 255f, 148f / 255f, 210f / 255f, 0.25f); // 强调色淡（选中行底）
	public Color Text = new Color(0.94f, 0.95f, 0.96f, 1f); // 主文字
	public Color TextDim = new Color(0.60f, 0.62f, 0.66f, 1f); // 次要文字/占位

	// ---- 尺寸 ----
	public int FontSize = 15;            // 常规字号
	public int FontSizeSmall = 13;       // 小字号（标题/占位）
	public int RowHeight = 36;           // 每条规则行高
	public int SectionHeight = 30;       // 分类标题行高
	public int TopBarHeight = 46;        // 顶栏高度
	public int ModListWidth = 240;       // 左栏 mod 列表宽度
	public float WindowWidth = 1100f;    // 窗口宽
	public float WindowHeight = 700f;    // 窗口高
}

/// <summary>
/// UGUI 控件工厂：集中创建设置界面用到的各类 UI 元素。
/// 统一负责 GameObject 创建、RectTransform 布局、字体加载与主题配色，
/// 调用方只需关心"建什么、放哪、多大"。
/// </summary>
public static class UiFactory {
	public static RoxyTheme Theme = new RoxyTheme();

	private static Font? CachedFont; // 字体缓存，只加载一次

	/// <summary>获取内置字体（Unity 6 用 LegacyRuntime.ttf，回退 Arial.ttf）。</summary>
	public static Font GetFont() {
		if (CachedFont == null) {
			CachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		return CachedFont;
	}

	/// <summary>创建一个空的 RectTransform（不含任何渲染/交互组件）。</summary>
	public static RectTransform CreateRect(string name, Transform parent) {
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false); // false = 保持本地坐标，避免位置跳变
		return go.GetComponent<RectTransform>();
	}

	/// <summary>创建一个带 Image 背景的面板（常作为容器或按钮底）。</summary>
	public static RectTransform CreatePanel(string name, Transform parent, Color color) {
		RectTransform rt = CreateRect(name, parent);
		rt.gameObject.AddComponent<Image>().color = color;
		return rt;
	}

	/// <summary>让 RectTransform 撑满父级（anchor 0..1，offset 0）。</summary>
	public static void Stretch(RectTransform rt) {
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	/// <summary>一次设置 RectTransform 的完整布局（锚点/中心点/位置/尺寸）。</summary>
	public static void SetRect(RectTransform rt, Vector2 anchor_min, Vector2 anchor_max, Vector2 pivot, Vector2 anchored_pos, Vector2 size) {
		rt.anchorMin = anchor_min;
		rt.anchorMax = anchor_max;
		rt.pivot = pivot;
		rt.anchoredPosition = anchored_pos;
		rt.sizeDelta = size;
	}

	/// <summary>创建文本。默认左对齐、关闭射线（不拦截点击）。</summary>
	public static Text CreateText(string name, Transform parent, string content, int font_size, Color color, TextAnchor align = TextAnchor.MiddleLeft) {
		RectTransform rt = CreateRect(name, parent);
		Text text = rt.gameObject.AddComponent<Text>();
		text.font = GetFont();
		text.text = content;
		text.fontSize = font_size;
		text.color = color;
		text.alignment = align;
		text.raycastTarget = false; // 文本不参与点击，避免挡住下层按钮
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return text;
	}

	/// <summary>创建按钮：PanelLight 底 + 居中文字 + onClick。</summary>
	public static Button CreateButton(string name, Transform parent, string label, Action onClick, float width, float height) {
		RectTransform rt = CreatePanel(name, parent, Theme.PanelLight);
		SetRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
		Button button = rt.gameObject.AddComponent<Button>();
		button.targetGraphic = rt.GetComponent<Image>();
		Text text = CreateText("Label", rt, label, Theme.FontSize, Theme.Text, TextAnchor.MiddleCenter);
		Stretch(text.rectTransform);
		button.onClick.AddListener(() => onClick?.Invoke());
		return button;
	}

	/// <summary>创建复选框（Toggle）：PanelLight 底 + 内部高亮方块作为勾选图形。</summary>
	public static Toggle CreateToggle(string name, Transform parent, bool initial, Action<bool> on_changed, float box_size) {
		RectTransform rt = CreateRect(name, parent);
		Toggle toggle = rt.gameObject.AddComponent<Toggle>();
		Image bg = rt.gameObject.AddComponent<Image>();
		bg.color = Theme.PanelLight;
		toggle.targetGraphic = bg;
		RectTransform check = CreatePanel("Checkmark", rt, Theme.Accent);
		SetRect(check, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(box_size - 8, box_size - 8));
		toggle.graphic = check.GetComponent<Image>();
		toggle.isOn = initial;
		toggle.onValueChanged.AddListener(value => on_changed(value));
		return toggle;
	}

	/// <summary>创建滑块（Slider）：轨道 + 填充条 + 手柄。</summary>
	public static Slider CreateSlider(string name, Transform parent, float min, float max, float initial, Action<float> on_changed) {
		RectTransform rt = CreateRect(name, parent);
		Slider slider = rt.gameObject.AddComponent<Slider>();

		// 轨道（细长背景条）
		RectTransform track = CreatePanel("Background", rt, Theme.Panel);
		track.anchorMin = new Vector2(0, 0.5f);
		track.anchorMax = new Vector2(1, 0.5f);
		track.pivot = new Vector2(0.5f, 0.5f);
		track.sizeDelta = new Vector2(0, 6);

		// 填充区（从左到右高亮，表示当前值比例）
		RectTransform fill_area = CreateRect("Fill Area", rt);
		fill_area.anchorMin = new Vector2(0, 0.5f);
		fill_area.anchorMax = new Vector2(1, 0.5f);
		fill_area.pivot = new Vector2(0.5f, 0.5f);
		fill_area.sizeDelta = new Vector2(-8, 0);
		RectTransform fill = CreatePanel("Fill", fill_area, Theme.Accent);
		fill.anchorMin = new Vector2(0, 0);
		fill.anchorMax = new Vector2(0, 1);
		fill.pivot = new Vector2(0.5f, 0.5f);
		fill.sizeDelta = Vector2.zero;
		Image fill_img = fill.GetComponent<Image>();
		fill_img.type = Image.Type.Filled;
		fill_img.fillMethod = Image.FillMethod.Horizontal;
		slider.fillRect = fill;

		// 手柄区（可拖动的圆钮）
		RectTransform handle_area = CreateRect("Handle Slide Area", rt);
		handle_area.anchorMin = new Vector2(0, 0.5f);
		handle_area.anchorMax = new Vector2(1, 0.5f);
		handle_area.pivot = new Vector2(0.5f, 0.5f);
		handle_area.sizeDelta = new Vector2(-8, 0);
		RectTransform handle = CreatePanel("Handle", handle_area, Theme.Text);
		handle.anchorMin = new Vector2(0, 0);
		handle.anchorMax = new Vector2(0, 1);
		handle.pivot = new Vector2(0.5f, 0.5f);
		handle.sizeDelta = new Vector2(14, 22);
		slider.handleRect = handle;

		slider.minValue = min;
		slider.maxValue = max;
		slider.value = initial;
		slider.onValueChanged.AddListener(value => on_changed(value));
		return slider;
	}

	/// <summary>创建输入框（InputField）：PanelLight 底 + 文本 + 占位文本。</summary>
	public static InputField CreateInputField(string name, Transform parent, string initial, Action<string> on_end_edit) {
		RectTransform rt = CreateRect(name, parent);
		InputField field = rt.gameObject.AddComponent<InputField>();
		Image bg = rt.gameObject.AddComponent<Image>();
		bg.color = Theme.PanelLight;
		field.targetGraphic = bg;
		Text text = CreateText("Text", rt, initial, Theme.FontSize, Theme.Text);
		Stretch(text.rectTransform);
		field.textComponent = text;
		Text placeholder = CreateText("Placeholder", rt, "", Theme.FontSize, Theme.TextDim);
		Stretch(placeholder.rectTransform);
		field.placeholder = placeholder;
		field.text = initial;
		field.onEndEdit.AddListener(value => on_end_edit(value));
		return field;
	}

	/// <summary>
	/// 创建可滚动区域（ScrollRect + Mask + Content）。
	/// 返回 content 用于后续往里放行。Content 以「左上角为基准、向下增长」布局（配合 PlaceRow）。
	/// </summary>
	public static (ScrollRect scroll, RectTransform content) CreateScrollView(string name, Transform parent, Color background) {
		RectTransform rt = CreatePanel(name, parent, background);
		Stretch(rt);
		ScrollRect scroll = rt.gameObject.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 28;
		RectTransform viewport = CreatePanel("Viewport", rt, background);
		Stretch(viewport);
		viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false; // 只裁剪不显示遮罩
		RectTransform content = CreateRect("Content", viewport);
		content.anchorMin = new Vector2(0, 1);
		content.anchorMax = new Vector2(1, 1);
		content.pivot = new Vector2(0.5f, 1);
		content.sizeDelta = Vector2.zero;
		scroll.viewport = viewport;
		scroll.content = content;
		return (scroll, content);
	}

	/// <summary>把一行放到 content 的 y 处（从上往下排），并给出行高。</summary>
	public static void PlaceRow(RectTransform row, RectTransform content, float y, float height) {
		row.anchorMin = new Vector2(0, 1);
		row.anchorMax = new Vector2(1, 1);
		row.pivot = new Vector2(0.5f, 1);
		row.anchoredPosition = new Vector2(0, -y);
		row.sizeDelta = new Vector2(0, height);
	}

	/// <summary>设置 content 总高度（决定滚动范围），y 累计到多高就传多高。</summary>
	public static void SetContentHeight(RectTransform content, float height) {
		content.sizeDelta = new Vector2(0, height);
	}
}
