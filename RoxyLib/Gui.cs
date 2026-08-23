using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoxyLib.Gui;

/// <summary>
/// RLA 设置界面的配色与尺寸主题。
/// 主色 #66CCFF（青蓝）。所有控件工厂统一从这里取值，改主题只需改这一处。
/// </summary>
public sealed class RoxyTheme {
	public Color Accent { get => RoxyLibRules.Accent; }
	public Color AccentDim { get => RoxyLibRules.AccentDim; }
	public Color AccentStrong { get => RoxyLibRules.AccentStrong; }
	public Color Background { get => RoxyLibRules.Background; }
	public Color Panel { get => RoxyLibRules.Panel; }
	public Color PanelLight { get => RoxyLibRules.PanelLight; }
	public Color PanelHover { get => RoxyLibRules.PanelHover; }
	public Color Text { get => RoxyLibRules.Text; }
	public Color TextDim { get => RoxyLibRules.TextDim; }

	// ---- 尺寸 ----
	public int FontSize = 14;            // 常规字号
	public int FontSizeSmall = 12;       // 小字号（标题/占位）
	public int FontSizeTitle = 17;       // 窗口标题字号
	public int RowHeight = 40;           // 每条规则行高
	public int SectionHeight = 28;       // 分类标题行高
	public int TopBarHeight = 48;        // 顶栏高度
	public int ModListWidth = 240;       // 左栏 mod 列表宽度
	public float WindowWidth = 1080f;    // 窗口宽
	public float WindowHeight = 680f;    // 窗口高
	public float CornerRadius = 10f;     // 圆角半径（像素）
}

/// <summary>
/// UGUI 控件工厂：集中创建设置界面用到的各类 UI 元素。
/// 统一负责 GameObject 创建、RectTransform 布局、字体/圆角 Sprite、主题配色与 hover 反馈。
/// </summary>
public static class UiFactory {
	public static RoxyTheme Theme = new RoxyTheme();

	// ---- 字体与 Sprite 缓存（只生成一次）----
	private static Font? CachedFont;
	private static Sprite? CachedRoundedSprite;
	private static Sprite? CachedCircleSprite;

	private const int SpriteSize = 64;         // 生成纹理尺寸（像素）
	private const float SpriteRadius = 12f;    // 圆角半径（像素，决定 9-slice 边框）

	/// <summary>获取内置字体（Unity 6 用 LegacyRuntime.ttf，回退 Arial.ttf）。</summary>
	public static Font GetFont() {
		if (CachedFont == null) {
			CachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		return CachedFont;
	}

	/// <summary>白色圆角矩形 Sprite（9-slice，拉伸保持圆角）。</summary>
	public static Sprite RoundedSprite => CachedRoundedSprite ??= CreateRoundedSprite();

	/// <summary>白色圆形 Sprite（Simple，用于开关滑块/滑块手柄）。</summary>
	public static Sprite CircleSprite => CachedCircleSprite ??= CreateCircleSprite();

	// ---------------- 基础创建 ----------------

	/// <summary>创建一个空的 RectTransform（不含任何渲染/交互组件）。</summary>
	public static RectTransform CreateRect(string name, Transform parent) {
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false); // 保持本地坐标
		return go.GetComponent<RectTransform>();
	}

	/// <summary>创建带 Image 背景的面板（直角矩形，常用于遮罩/容器底）。</summary>
	public static RectTransform CreatePanel(string name, Transform parent, Color color) {
		RectTransform rt = CreateRect(name, parent);
		rt.gameObject.AddComponent<Image>().color = color;
		return rt;
	}

	/// <summary>创建带圆角背景的面板（9-slice 圆角）。</summary>
	public static RectTransform CreateRoundedPanel(string name, Transform parent, Color color) {
		RectTransform rt = CreateRect(name, parent);
		Image img = rt.gameObject.AddComponent<Image>();
		SetRounded(img);
		img.color = color;
		return rt;
	}

	/// <summary>把 Image 设为圆角 9-slice。</summary>
	public static void SetRounded(Image img) {
		img.sprite = RoundedSprite;
		img.type = Image.Type.Sliced;
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
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return text;
	}

	// ---------------- 按钮 ----------------

	/// <summary>给 Button 应用统一的 hover/按下反馈（ColorTint 微亮/微暗）。</summary>
	public static void ApplyButtonStyle(Button button, Image target) {
		button.targetGraphic = target;
		ColorBlock cb = button.colors;
		cb.normalColor = Color.white;
		cb.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
		cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
		cb.selectedColor = Color.white;
		cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
		cb.fadeDuration = 0.06f;
		button.colors = cb;
	}

	/// <summary>创建圆角按钮：PanelLight 底 + 居中文字 + hover 反馈。</summary>
	public static Button CreateButton(string name, Transform parent, string label, Action onClick, float width, float height) {
		RectTransform rt = CreateRect(name, parent);
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = Theme.PanelLight;
		SetRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
		Button button = rt.gameObject.AddComponent<Button>();
		ApplyButtonStyle(button, bg);
		Text text = CreateText("Label", rt, label, Theme.FontSize, Theme.Text, TextAnchor.MiddleCenter);
		Stretch(text.rectTransform);
		text.rectTransform.offsetMin = new Vector2(6, 0);
		text.rectTransform.offsetMax = new Vector2(-6, 0);
		button.onClick.AddListener(() => onClick?.Invoke());
		return button;
	}

	// ---------------- 开关（Switch 样式）----------------

	/// <summary>
	/// 创建开关（Switch）：胶囊轨道 + 滑动圆钮。
	/// 开 = Accent 底 + 圆钮靠右；关 = PanelLight 底 + 圆钮靠左。
	/// </summary>
	public static Toggle CreateToggle(string name, Transform parent, bool initial, Action<bool> on_changed, float box_size) {
		RectTransform rt = CreateRect(name, parent);
		Toggle toggle = rt.gameObject.AddComponent<Toggle>();

		// 胶囊轨道
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = initial ? Theme.Accent : Theme.PanelLight;
		toggle.targetGraphic = bg;

		// 圆钮
		RectTransform knob = CreateRect("Knob", rt);
		Image knob_img = knob.gameObject.AddComponent<Image>();
		knob_img.sprite = CircleSprite;
		knob_img.type = Image.Type.Simple;
		knob_img.color = Color.white;
		knob.pivot = new Vector2(0.5f, 0.5f);
		knob.sizeDelta = new Vector2(box_size, box_size);

		toggle.isOn = initial;
		PositionKnob(knob, initial);
		toggle.onValueChanged.AddListener(value => {
			bg.color = value ? Theme.Accent : Theme.PanelLight;
			PositionKnob(knob, value);
			on_changed?.Invoke(value);
		});
		return toggle;
	}

	private static void PositionKnob(RectTransform knob, bool on) {
		float inset = knob.sizeDelta.x / 2f + 4f;
		if (on) {
			knob.anchorMin = new Vector2(1, 0.5f);
			knob.anchorMax = new Vector2(1, 0.5f);
			knob.anchoredPosition = new Vector2(-inset, 0);
		} else {
			knob.anchorMin = new Vector2(0, 0.5f);
			knob.anchorMax = new Vector2(0, 0.5f);
			knob.anchoredPosition = new Vector2(inset, 0);
		}
	}

	// ---------------- 滑块 ----------------

	/// <summary>创建滑块：圆角轨道 + Accent 填充 + 圆形手柄。</summary>
	public static Slider CreateSlider(string name, Transform parent, float min, float max, float initial, Action<float> on_changed) {
		RectTransform rt = CreateRect(name, parent);
		Slider slider = rt.gameObject.AddComponent<Slider>();

		// 轨道（细长背景条，直角即可）
		RectTransform track = CreatePanel("Background", rt, Theme.Panel);
		track.anchorMin = new Vector2(0, 0.5f);
		track.anchorMax = new Vector2(1, 0.5f);
		track.pivot = new Vector2(0.5f, 0.5f);
		track.sizeDelta = new Vector2(0, 6);

		// 填充区（Accent，表示当前值比例）
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

		// 手柄区（圆形 Accent 手柄）
		RectTransform handle_area = CreateRect("Handle Slide Area", rt);
		handle_area.anchorMin = new Vector2(0, 0.5f);
		handle_area.anchorMax = new Vector2(1, 0.5f);
		handle_area.pivot = new Vector2(0.5f, 0.5f);
		handle_area.sizeDelta = new Vector2(-8, 0);
		RectTransform handle = CreateRect("Handle", handle_area);
		Image handle_img = handle.gameObject.AddComponent<Image>();
		handle_img.sprite = CircleSprite;
		handle_img.type = Image.Type.Simple;
		handle_img.color = Theme.Accent;
		handle.anchorMin = new Vector2(0, 0);
		handle.anchorMax = new Vector2(0, 1);
		handle.pivot = new Vector2(0.5f, 0.5f);
		handle.sizeDelta = new Vector2(16, 16);
		slider.handleRect = handle;

		slider.minValue = min;
		slider.maxValue = max;
		slider.value = initial;
		slider.onValueChanged.AddListener(value => on_changed(value));
		return slider;
	}

	// ---------------- 输入框 ----------------

	/// <summary>创建输入框：圆角底 + 文本 + 占位，左右留内边距。</summary>
	public static InputField CreateInputField(string name, Transform parent, string initial, Action<string> on_end_edit) {
		RectTransform rt = CreateRect(name, parent);
		InputField field = rt.gameObject.AddComponent<InputField>();
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = Theme.PanelLight;
		field.targetGraphic = bg;
		Text text = CreateText("Text", rt, initial, Theme.FontSize, Theme.Text);
		Stretch(text.rectTransform);
		text.rectTransform.offsetMin = new Vector2(8, 0);
		text.rectTransform.offsetMax = new Vector2(-8, 0);
		field.textComponent = text;
		Text placeholder = CreateText("Placeholder", rt, "", Theme.FontSize, Theme.TextDim);
		Stretch(placeholder.rectTransform);
		placeholder.rectTransform.offsetMin = new Vector2(8, 0);
		placeholder.rectTransform.offsetMax = new Vector2(-8, 0);
		field.placeholder = placeholder;
		field.text = initial;
		field.onEndEdit.AddListener(value => on_end_edit(value));
		return field;
	}

	// ---------------- 滚动区域 ----------------

	/// <summary>
	/// 创建可滚动区域（ScrollRect + Mask + Content + 右侧细滚动条）。
	/// Content 以「左上角为基准、向下增长」布局（配合 PlaceRow）。
	/// </summary>
	public static (ScrollRect scroll, RectTransform content) CreateScrollView(string name, Transform parent, Color background) {
		RectTransform rt = CreatePanel(name, parent, background);
		Stretch(rt);
		ScrollRect scroll = rt.gameObject.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 28;
		scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; // 手动留出滚动条宽度，固定显示

		RectTransform viewport = CreatePanel("Viewport", rt, background);
		Stretch(viewport);
		viewport.offsetMax = new Vector2(-8, 0); // 让出右侧滚动条
		viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

		RectTransform content = CreateRect("Content", viewport);
		content.anchorMin = new Vector2(0, 1);
		content.anchorMax = new Vector2(1, 1);
		content.pivot = new Vector2(0.5f, 1);
		content.sizeDelta = Vector2.zero;
		scroll.viewport = viewport;
		scroll.content = content;

		// 右侧细滚动条（半透明白灰色圆条）
		RectTransform sb_rt = CreateRect("Scrollbar", rt);
		sb_rt.anchorMin = new Vector2(1, 0);
		sb_rt.anchorMax = new Vector2(1, 1);
		sb_rt.pivot = new Vector2(1, 0.5f);
		sb_rt.anchoredPosition = new Vector2(-2, 0);
		sb_rt.sizeDelta = new Vector2(5, 0);
		Scrollbar sb = sb_rt.gameObject.AddComponent<Scrollbar>();
		sb.direction = Scrollbar.Direction.BottomToTop;
		RectTransform sliding = CreateRect("Sliding Area", sb_rt);
		Stretch(sliding);
		sliding.offsetMin = new Vector2(1, 1);
		sliding.offsetMax = new Vector2(-1, -1);
		RectTransform handle = CreateRect("Handle", sliding);
		Image handle_img = handle.gameObject.AddComponent<Image>();
		handle_img.color = new Color(0.55f, 0.62f, 0.70f, 0.45f);
		handle.anchorMin = new Vector2(0, 0);
		handle.anchorMax = new Vector2(0, 1);
		handle.pivot = new Vector2(0.5f, 0.5f);
		handle.sizeDelta = new Vector2(0, 20);
		sb.handleRect = handle;
		sb.value = 1f;
		scroll.verticalScrollbar = sb;
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

	// ---------------- Sprite 生成 ----------------

	private static Sprite CreateRoundedSprite() {
		Texture2D tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Bilinear;
		tex.wrapMode = TextureWrapMode.Clamp;
		float r = SpriteRadius / SpriteSize; // 归一化圆角半径
		for (int y = 0; y < SpriteSize; y++) {
			for (int x = 0; x < SpriteSize; x++) {
				float px = (x + 0.5f) / SpriteSize;
				float py = (y + 0.5f) / SpriteSize;
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, RoundedRectAlpha(px, py, r)));
			}
		}
		tex.Apply();
		float border = SpriteRadius;
		return Sprite.Create(tex, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
	}

	/// <summary>圆角矩形有符号距离场 → 抗锯齿 alpha。</summary>
	private static float RoundedRectAlpha(float px, float py, float r) {
		float qx = Mathf.Abs(px - 0.5f) - (0.5f - r);
		float qy = Mathf.Abs(py - 0.5f) - (0.5f - r);
		float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
		float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
		float dist = outside + inside - r; // 归一化有符号距离
		return Mathf.Clamp01(0.5f - dist * SpriteSize); // 约 1px 抗锯齿
	}

	private static Sprite CreateCircleSprite() {
		Texture2D tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Bilinear;
		tex.wrapMode = TextureWrapMode.Clamp;
		for (int y = 0; y < SpriteSize; y++) {
			for (int x = 0; x < SpriteSize; x++) {
				float px = (x + 0.5f) / SpriteSize;
				float py = (y + 0.5f) / SpriteSize;
				float dx = px - 0.5f;
				float dy = py - 0.5f;
				float dist = Mathf.Sqrt(dx * dx + dy * dy) - 0.5f;
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist * SpriteSize)));
			}
		}
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f);
	}
}
