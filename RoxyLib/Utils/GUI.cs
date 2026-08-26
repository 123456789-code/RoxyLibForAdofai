using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoxyLib.Utils;

/// <summary>
/// UGUI 控件工厂：集中创建设置界面用到的各类 UI 元素。
/// 所有配色/尺寸常量集中在类开头，方便统一调主题（改这一处即可）。
/// 与 Rules/Overlay 解耦：颜色直接硬编码，不读 RoxyLibRules。
/// </summary>
public static class UiFactory {
	// ==================== 配色 ====================
	// 注：Color 是结构体，C# 不允许 const，故颜色用 static readonly；数字用 const。

	// ---- 主色 #66CCFF ----
	public static readonly Color Accent       = new(0.40f, 0.80f, 1.00f, 1f);     // 强调色（开关/滑块/选中）
	public static readonly Color AccentDim    = new(0.40f, 0.80f, 1.00f, 0.16f);  // 强调色淡（选中行底）
	public static readonly Color AccentStrong = new(0.20f, 0.55f, 0.78f, 1f);     // 深强调（悬停/边框）

	// ---- 可爱风格配色（明亮粉彩系）----
	public static readonly Color Background   = new(0.98f, 0.96f, 0.94f, 1f);     // 背景：奶油白（暖白）
	public static readonly Color Panel        = new(0.95f, 0.88f, 0.92f, 1f);     // 面板：淡粉（左栏/右栏）
	public static readonly Color PanelLight   = new(1.00f, 0.95f, 0.97f, 1f);     // 控件底：粉白（按钮/输入框/卡片）
	public static readonly Color PanelHover   = new(0.85f, 0.90f, 1.00f, 1f);     // 悬停高亮：淡蓝（呼应主色）
	public static readonly Color Text         = new(0.20f, 0.18f, 0.25f, 1f);     // 主文字：深灰紫（清晰柔和）
	public static readonly Color TextDim      = new(0.50f, 0.48f, 0.55f, 1f);     // 次要文字：暖灰（占位/说明）

	// ==================== 尺寸 ====================
	public const int   FontSize      = 14;     // 常规字号
	public const int   FontSizeSmall = 12;     // 小字号（标题/占位）
	public const int   FontSizeTitle = 17;     // 窗口标题字号
	public const int   RowHeight     = 40;     // 每条规则行高
	public const int   SectionHeight = 28;     // 分类标题行高
	public const int   TopBarHeight  = 48;     // 顶栏高度
	public const int   ModListWidth  = 240;    // 左栏 mod 列表宽度
	public const float WindowWidth   = 1080f;  // 窗口宽
	public const float WindowHeight  = 680f;   // 窗口高
	public const float CornerRadius  = 10f;    // 圆角半径（像素）

	// ==================== 字体与 Sprite 缓存 ====================
	private static Font?   CachedFont;
	private static Sprite? CachedRoundedSprite;
	private static Sprite? CachedCircleSprite;

	private const int   SpriteSize   = 64;    // 生成纹理尺寸（像素）
	private const float SpriteRadius = 12f;   // 圆角半径（像素，决定 9-slice 边框）

	// ==================== 字体与 Sprite ====================

	/// <summary>获取内置字体（Unity 6 用 LegacyRuntime.ttf，回退 Arial.ttf）</summary>
	public static Font GetFont() {
		CachedFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
			?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		return CachedFont;
	}

	/// <summary>白色圆角矩形 Sprite（9-slice，拉伸保持圆角）</summary>
	public static Sprite RoundedSprite => CachedRoundedSprite ??= CreateRoundedSprite();

	/// <summary>白色圆形 Sprite（Simple，用于开关滑块/滑块手柄）</summary>
	public static Sprite CircleSprite => CachedCircleSprite ??= CreateCircleSprite();

	// ==================== 基础创建 ====================

	/// <summary>创建一个空的 RectTransform（不含任何渲染/交互组件）</summary>
	public static RectTransform CreateRect(string name, Transform parent) {
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false); // 保持本地坐标
		return go.GetComponent<RectTransform>();
	}

	/// <summary>创建带 Image 背景的面板（直角矩形，常用于遮罩/容器底）</summary>
	public static RectTransform CreatePanel(string name, Transform parent, Color color) {
		RectTransform rt = CreateRect(name, parent);
		rt.gameObject.AddComponent<Image>().color = color;
		return rt;
	}

	/// <summary>创建带圆角背景的面板（9-slice 圆角）</summary>
	public static RectTransform CreateRoundedPanel(string name, Transform parent, Color color) {
		RectTransform rt = CreateRect(name, parent);
		Image img = rt.gameObject.AddComponent<Image>();
		SetRounded(img);
		img.color = color;
		return rt;
	}

	/// <summary>把 Image 设为圆角 9-slice</summary>
	public static void SetRounded(Image img) {
		img.sprite = RoundedSprite;
		img.type = Image.Type.Sliced;
	}

	/// <summary>让 RectTransform 撑满父级（anchor 0..1，offset 0）</summary>
	public static void Stretch(RectTransform rt) {
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	/// <summary>一次设置 RectTransform 的完整布局（锚点/中心点/位置/尺寸）</summary>
	public static void SetRect(RectTransform rt, Vector2 anchor_min, Vector2 anchor_max, Vector2 pivot, Vector2 anchored_pos, Vector2 size) {
		rt.anchorMin = anchor_min;
		rt.anchorMax = anchor_max;
		rt.pivot = pivot;
		rt.anchoredPosition = anchored_pos;
		rt.sizeDelta = size;
	}

	/// <summary>创建文本。默认左对齐、关闭射线（不拦截点击）</summary>
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

	// ==================== 按钮 ====================

	/// <summary>给 Button 应用统一的 hover/按下反馈（ColorTint 微亮/微暗）</summary>
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

	/// <summary>创建圆角按钮：PanelLight 底 + 居中文字 + hover 反馈</summary>
	public static Button CreateButton(string name, Transform parent, string label, Action on_click, float width, float height) {
		RectTransform rt = CreateRect(name, parent);
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = PanelLight;
		SetRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
		Button button = rt.gameObject.AddComponent<Button>();
		ApplyButtonStyle(button, bg);
		Text text = CreateText("Label", rt, label, FontSize, Text, TextAnchor.MiddleCenter);
		Stretch(text.rectTransform);
		text.rectTransform.offsetMin = new Vector2(6, 0);
		text.rectTransform.offsetMax = new Vector2(-6, 0);
		button.onClick.AddListener(() => on_click?.Invoke());
		return button;
	}

	// ==================== 开关（Switch 样式）====================

	/// <summary>
	/// 创建开关（Switch）：胶囊轨道 + 滑动圆钮
	/// 开 = Accent 底 + 圆钮靠右；关 = PanelLight 底 + 圆钮靠左
	/// </summary>
	public static Toggle CreateToggle(string name, Transform parent, bool initial, Action<bool> on_changed, float box_size) {
		RectTransform rt = CreateRect(name, parent);
		Toggle toggle = rt.gameObject.AddComponent<Toggle>();

		// 胶囊轨道
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = initial ? Accent : PanelLight;
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
			bg.color = value ? Accent : PanelLight;
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
		}
		else {
			knob.anchorMin = new Vector2(0, 0.5f);
			knob.anchorMax = new Vector2(0, 0.5f);
			knob.anchoredPosition = new Vector2(inset, 0);
		}
	}

	// ==================== 滑块 ====================

	/// <summary>创建滑块：圆角轨道 + Accent 填充 + 圆形手柄</summary>
	public static Slider CreateSlider(string name, Transform parent, float min, float max, float initial, Action<float> on_changed) {
		RectTransform rt = CreateRect(name, parent);
		Slider slider = rt.gameObject.AddComponent<Slider>();

		// 轨道（细长背景条，直角即可）
		RectTransform track = CreatePanel("Background", rt, Panel);
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
		RectTransform fill = CreatePanel("Fill", fill_area, Accent);
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
		handle_img.color = Accent;
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

	// ==================== 输入框 ====================

	/// <summary>创建输入框：圆角底 + 文本 + 占位，左右留内边距</summary>
	public static InputField CreateInputField(string name, Transform parent, string initial, Action<string> on_end_edit) {
		RectTransform rt = CreateRect(name, parent);
		InputField field = rt.gameObject.AddComponent<InputField>();
		Image bg = rt.gameObject.AddComponent<Image>();
		SetRounded(bg);
		bg.color = PanelLight;
		field.targetGraphic = bg;
		Text text = CreateText("Text", rt, initial, FontSize, Text);
		Stretch(text.rectTransform);
		text.rectTransform.offsetMin = new Vector2(8, 0);
		text.rectTransform.offsetMax = new Vector2(-8, 0);
		field.textComponent = text;
		Text placeholder = CreateText("Placeholder", rt, "", FontSize, TextDim);
		Stretch(placeholder.rectTransform);
		placeholder.rectTransform.offsetMin = new Vector2(8, 0);
		placeholder.rectTransform.offsetMax = new Vector2(-8, 0);
		field.placeholder = placeholder;
		field.text = initial;
		field.onEndEdit.AddListener(value => on_end_edit(value));
		return field;
	}

	// ==================== 滚动区域 ====================

	/// <summary>
	/// 创建可滚动区域（ScrollRect + Mask + Content + 右侧细滚动条）
	/// Content 以「左上角为基准、向下增长」布局（配合 PlaceRow）
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

	/// <summary>把一行放到 content 的 y 处（从上往下排），并给出行高</summary>
	public static void PlaceRow(RectTransform row, RectTransform content, float y, float height) {
		row.anchorMin = new Vector2(0, 1);
		row.anchorMax = new Vector2(1, 1);
		row.pivot = new Vector2(0.5f, 1);
		row.anchoredPosition = new Vector2(0, -y);
		row.sizeDelta = new Vector2(0, height);
	}

	/// <summary>设置 content 总高度（决定滚动范围），y 累计到多高就传多高</summary>
	public static void SetContentHeight(RectTransform content, float height) {
		content.sizeDelta = new Vector2(0, height);
	}

	// ==================== Sprite 生成 ====================

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

	/// <summary>圆角矩形有符号距离场 → 抗锯齿 alpha</summary>
	private static float RoundedRectAlpha(float px, float py, float r) {
		float qx = Mathf.Abs(px - 0.5f) - (0.5f - r);
		float qy = Mathf.Abs(py - 0.5f) - (0.5f - r);
		float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
		float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
		float dist = outside + inside - r; // 归一化有符号距离
		return Mathf.Clamp01(0.5f - dist * SpriteSize); // 约 1px 抗锯齿
	}

	private static Sprite CreateCircleSprite() {
		var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
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

/// <summary>
/// 库级下拉组件：一个圆角触发按钮 + 点击后弹出的圆角菜单
///
/// 弹出菜单不挂在触发按钮的父级下——那样会被 ScrollRect 的 Mask 裁剪掉，
/// 而是挂在 Canvas 根（popup_root，ScreenSpaceOverlay 全屏）上，
/// 用屏幕坐标定位在触发按钮正下方，保证在滚动区域内也能完整弹出、不被裁剪
///
/// 生命周期：创建时构建触发按钮 → 点击展开/收起 → 选中选项回调上层
/// </summary>
public sealed class RoxyDropdown {
	private readonly RectTransform PopupRoot;     // 弹出菜单的挂载根（Canvas 根，全屏、pivot 0.5,0.5）
	private readonly RectTransform TriggerButton; // 触发按钮（挂在规则行的 control 区域里）
	private readonly List<string> Options;        // 选项文本
	private readonly Action<int> OnChanged;       // 选中回调（参数为选中索引）
	private bool Open;                             // 菜单是否展开

	/// <summary>当前选中索引（-1 表示无选中）。</summary>
	public int SelectedIndex { get; private set; }

	// 弹出菜单布局参数
	private const float MENU_WIDTH  = 190f;
	private const float ITEM_HEIGHT = 30f;
	private const float ITEM_GAP    = 2f;
	private const float PADDING     = 6f;

	/// <param name="parent">触发按钮的父节点（规则行的 control 区域）</param>
	/// <param name="popup_root">弹出菜单挂载根（Canvas 根，ScreenSpaceOverlay 下用屏幕坐标）</param>
	/// <param name="options">选项文本</param>
	/// <param name="initial_index">当前选中索引</param>
	/// <param name="on_changed">选择回调（选中索引）</param>
	public RoxyDropdown(RectTransform parent, RectTransform popup_root, IList<string> options, int initial_index, Action<int> on_changed) {
		PopupRoot = popup_root;
		Options = new List<string>(options);
		OnChanged = on_changed;
		SelectedIndex = Mathf.Clamp(initial_index, 0, Mathf.Max(0, Options.Count - 1));

		TriggerButton = CreateTrigger(parent);
		// 必须在 TriggerButton 赋值后再刷新文案（CreateTrigger 期间字段尚未赋值）
		UpdateLabel();
	}

	// ---------------- 触发按钮 ----------------

	private RectTransform CreateTrigger(Transform parent) {
		RectTransform btn_rt = UiFactory.CreateRect("RoxyDropdown", parent);
		btn_rt.anchorMin = new Vector2(0, 0.5f);
		btn_rt.anchorMax = new Vector2(1, 0.5f);
		btn_rt.pivot = new Vector2(0.5f, 0.5f);
		btn_rt.anchoredPosition = Vector2.zero;
		btn_rt.sizeDelta = new Vector2(-8, 30);

		Button button = btn_rt.gameObject.AddComponent<Button>();
		Image bg = btn_rt.gameObject.AddComponent<Image>();
		UiFactory.SetRounded(bg);
		bg.color = UiFactory.PanelLight;
		UiFactory.ApplyButtonStyle(button, bg);

		Text label = UiFactory.CreateText("Label", btn_rt, "", UiFactory.FontSize, UiFactory.Text, TextAnchor.MiddleLeft);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(12, 0);
		label.rectTransform.offsetMax = new Vector2(-30, 0);

		Text arrow = UiFactory.CreateText("Arrow", btn_rt, "▾", UiFactory.FontSizeSmall, UiFactory.TextDim, TextAnchor.MiddleRight);
		UiFactory.Stretch(arrow.rectTransform);
		arrow.rectTransform.offsetMin = new Vector2(-26, 0);
		arrow.rectTransform.offsetMax = new Vector2(-10, 0);

		button.onClick.AddListener(Toggle);
		return btn_rt;
	}

	/// <summary>把当前选中项刷新到触发按钮文案。</summary>
	private void UpdateLabel() {
		if (TriggerButton.Find("Label")?.GetComponent<Text>() is Text label) {
			label.text = SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex] : "";
		}
	}

	// ---------------- 展开 / 收起 ----------------

	private void Toggle() {
		Open = !Open;
		if (Open) {
			ShowPopup();
		}
		else {
			HidePopup();
		}
	}

	/// <summary>
	/// 在触发按钮正下方展开圆角菜单。
	/// 定位：WorldToScreenPoint 取触发按钮中心的屏幕坐标 → ScreenPointToLocalPointInRectangle
	/// 换算到 PopupRoot 本地坐标（自动处理 pivot/缩放），作为 anchor(0.5,0.5) 子节点的 anchoredPosition。
	/// </summary>
	private void ShowPopup() {
		HidePopup(); // 先清理上次的弹出项
		if (PopupRoot == null) {
			return;
		}

		Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, TriggerButton.position);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(PopupRoot, screen, null, out Vector2 local);

		// 圆角菜单容器（先建，作为选项的底）
		float menu_height = PADDING * 2 + Options.Count * ITEM_HEIGHT + (Options.Count - 1) * ITEM_GAP;
		RectTransform panel = UiFactory.CreateRoundedPanel("RoxyDropdownPanel", PopupRoot, UiFactory.Panel);
		panel.anchorMin = new Vector2(0.5f, 0.5f);
		panel.anchorMax = new Vector2(0.5f, 0.5f);
		panel.pivot = new Vector2(0, 1); // 容器左上角对齐定位点
		panel.anchoredPosition = local + new Vector2(0, -20f);
		panel.sizeDelta = new Vector2(MENU_WIDTH, menu_height);

		// 选项按钮
		for (int i = 0; i < Options.Count; i++) {
			int idx = i;
			bool selected = idx == SelectedIndex;
			string text = (selected ? "✓  " : "") + Options[idx];
			Button opt = UiFactory.CreateButton("RoxyDropdownOpt_" + idx, panel, text, () => Select(idx), MENU_WIDTH - PADDING * 2, ITEM_HEIGHT);
			RectTransform opt_rt = opt.GetComponent<RectTransform>();
			opt_rt.anchorMin = new Vector2(0, 1);
			opt_rt.anchorMax = new Vector2(1, 1);
			opt_rt.pivot = new Vector2(0.5f, 1);
			opt_rt.anchoredPosition = new Vector2(0, -(PADDING + idx * (ITEM_HEIGHT + ITEM_GAP)));
			// 只缩左右，保留 anchoredPosition 算好的 y 定位（offset 的 y 会覆盖 y 位置，不能置 0）
			opt_rt.offsetMin = new Vector2(PADDING, opt_rt.offsetMin.y);
			opt_rt.offsetMax = new Vector2(-PADDING, opt_rt.offsetMax.y);
			opt_rt.sizeDelta = new Vector2(0, ITEM_HEIGHT);
			if (selected) {
				opt.GetComponent<Image>().color = UiFactory.Accent;
				if (opt.transform.Find("Label") is Transform label_t && label_t.GetComponent<Text>() is Text label) {
					label.color = Color.white;
					label.fontStyle = FontStyle.Bold;
				}
			}
		}
	}

	/// <summary>移除弹出菜单（面板 + 选项，按名字前缀识别，避免误删 Canvas 根上的其他 UI）。</summary>
	private void HidePopup() {
		if (PopupRoot == null) {
			return;
		}
		for (int i = PopupRoot.childCount - 1; i >= 0; i--) {
			if (PopupRoot.GetChild(i).name.StartsWith("RoxyDropdown")) {
				UnityEngine.Object.DestroyImmediate(PopupRoot.GetChild(i).gameObject);
			}
		}
	}

	// ---------------- 选中 ----------------

	private void Select(int index) {
		if (index < 0 || index >= Options.Count) {
			return;
		}
		SelectedIndex = index;
		UpdateLabel();
		Open = false;
		HidePopup();
		OnChanged?.Invoke(index); // 通知上层（规则面板据此 SetValue 并触发防抖保存）
	}

	/// <summary>外部收起（如面板重建时调用，避免菜单残留）。</summary>
	public void Close() {
		Open = false;
		HidePopup();
	}
}

/// <summary>RectTransform 便捷布局扩展：以「左上角为基准、x 向右 y 向下」放置并给定尺寸。</summary>
internal static class RectTransformExtensions {
	public static void SetRectAt(this RectTransform rt, float x, float y, float width, float height) {
		rt.anchorMin = new Vector2(0, 1);
		rt.anchorMax = new Vector2(0, 1);
		rt.pivot = new Vector2(0, 1);
		rt.anchoredPosition = new Vector2(x, -y);
		rt.sizeDelta = new Vector2(width, height);
	}
}
