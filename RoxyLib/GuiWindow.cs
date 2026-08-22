using System;
using System.Collections.Generic;
using System.Linq;
using RoxyLib.Input;
using RoxyLib.Lang;
using RoxyLib.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoxyLib.Gui;

/// <summary>
/// RLA 设置主窗口。
///
/// 结构：全屏 Canvas（ScreenSpaceOverlay）→ Modal 遮罩 + Window（左上顶栏、左侧 mod 列表、右侧规则面板）。
/// 窗口开关由 RoxyLib 自己的 OpenSettings 规则值驱动；语言切换刷新通过订阅 Language 规则的 ValueChanged。
/// 每帧由 RoxyLib.Tick 调用 OnUpdate：处理快捷键捕获、ESC 关闭、脏标记刷新。
/// </summary>
public sealed class RoxyGui {
	// ---- 关键 UI 引用（构建时保存，供刷新/开关用）----
	private RectTransform? Root;           // Canvas 根（下拉弹层挂这里）
	private RectTransform? Window;         // 主窗口
	private RectTransform? Modal;          // 全屏遮罩
	private RectTransform? ModListContent; // 左栏 mod 列表的滚动 content
	private RectTransform? RuleContent;    // 右栏规则面板的滚动 content
	private Text? RightTitle;              // 右栏标题（当前选中 mod 名）
	private Text? TopTitle;                // 顶栏标题（跟随语言）
	private Text? LeftTitle;               // 左栏 "MODS"（跟随语言）
	private InputField? SearchField;       // 搜索框（占位文本跟随语言）

	// ---- 状态 ----
	private string SelectedModId = RoxyLib.MOD_ID; // 当前选中的 mod（默认选中 RoxyLib 自身）
	private string SearchText = "";                // 搜索关键词
	private bool Ready;                            // 是否已初始化（防重复）
	private bool Dirty;                            // 需要重建界面（注册/语言/值变更后置位）
	private bool CapturingKeybind;                 // 是否处于快捷键捕获状态
	private RuleInfo? CaptureRule;                 // 正在捕获快捷键的规则
	private readonly List<RoxyDropdown> Dropdowns = new List<RoxyDropdown>(); // 已创建的下拉（重建时统一收起）

	public bool IsOpen { get; private set; }

	/// <summary>外部入口（UMM 设置按钮等）打开设置界面。</summary>
	public void OpenFromExternal() => SetOpenSettingsValue(true);

	/// <summary>首次创建 Canvas 与窗口 UI，并订阅 RoxyLib 的注册/变更事件。</summary>
	public void Initialize() {
		if (Ready) {
			return;
		}
		Ready = true;
		CreateRootUi();
		// 订阅事件：
		//  - 某个 mod 注册完成 → 若为 RoxyLib 自身则订阅 OpenSettings/Language 规则
		//  - 反注册/版本变更 → 标记脏，重建界面
		// 注：RoxyLib 自身注册发生在 EnsureReady（本方法）之后，所以这里用 ModRegistered 事件触发订阅。
		RoxyLib.ModRegistered += OnModRegistered;
		RoxyLib.ModUnregistered += _ => Dirty = true;
		RoxyLib.RevisionChanged += _ => Dirty = true;
	}

	/// <summary>某 mod 注册完成：若为 RoxyLib 自身，订阅其 OpenSettings（开关窗口）与 Language（切语言刷新）。</summary>
	private void OnModRegistered(RoxyHost host) {
		if (host.ModId == RoxyLib.MOD_ID) {
			SubscribeOpenSettings();
			SubscribeLanguage();
		}
		Dirty = true;
	}

	/// <summary>每帧驱动（由 RoxyLib.Tick 调用）。</summary>
	public void OnUpdate(float dt) {
		if (CapturingKeybind) {
			UpdateKeybindCapture(); // 捕获快捷键中：监听按键
		}
		if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape) && !CapturingKeybind) {
			SetOpenSettingsValue(false); // 窗口开着且非捕获态时，ESC 关闭窗口
		}
		if (Dirty && IsOpen) {
			Dirty = false;
			RefreshAll(); // 有变更且窗口可见时才重建（窗口关闭时无需重建）
		}
	}

	// ---------------- 开关 / 语言驱动 ----------------

	/// <summary>订阅 OpenSettings 规则：值变更 → 开/关窗口；并同步当前值。</summary>
	private void SubscribeOpenSettings() {
		foreach (RuleInfo rule in RoxyRules.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "OpenSettings") {
				rule.ValueChanged += (sender, args) => SetOpen((bool)args);
				SetOpen((bool)rule.GetValue());
				break;
			}
		}
	}

	/// <summary>订阅 Language 规则：值变更 → 标记刷新（重读翻译）。RoxyLang 无广播事件，故直接订阅规则。</summary>
	private void SubscribeLanguage() {
		foreach (RuleInfo rule in RoxyRules.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "Language") {
				rule.ValueChanged += (sender, args) => Dirty = true;
				break;
			}
		}
	}

	/// <summary>通过给 OpenSettings 规则赋值来开关窗口（赋值会触发 ValueChanged → SetOpen）。</summary>
	private void SetOpenSettingsValue(bool value) {
		foreach (RuleInfo rule in RoxyRules.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "OpenSettings") {
				rule.SetValue(value, true);
				return;
			}
		}
	}

	/// <summary>直接设置窗口显示状态（由 OpenSettings 规则值驱动）。</summary>
	private void SetOpen(bool open) {
		IsOpen = open;
		if (Window != null) {
			Window.gameObject.SetActive(open);
		}
		if (Modal != null) {
			Modal.gameObject.SetActive(open);
		}
		if (open) {
			RefreshAll(); // 打开时立即构建一次内容
		}
	}

	// ---------------- UI 构建 ----------------

	/// <summary>创建 Canvas（ScreenSpaceOverlay）、全屏遮罩、主窗口，并构建顶栏与主体。</summary>
	private void CreateRootUi() {
		EnsureEventSystem();
		GameObject canvas_go = new GameObject("RoxyLibCanvas");
		UnityEngine.Canvas canvas = canvas_go.AddComponent<UnityEngine.Canvas>();
		canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30000; // 置顶显示
		canvas_go.AddComponent<GraphicRaycaster>();
		UnityEngine.Object.DontDestroyOnLoad(canvas_go); // 跨场景存活
		Root = canvas_go.GetComponent<RectTransform>();

		// 全屏遮罩：打开时挡住底层，拦截鼠标点击穿透（默认隐藏）
		Modal = UiFactory.CreatePanel("Modal", Root, new Color(0f, 0f, 0f, 0.35f));
		UiFactory.Stretch(Modal);
		Modal.gameObject.SetActive(false);

		// 主窗口：居中固定尺寸（无 CanvasScaler，固定像素）
		Window = UiFactory.CreatePanel("RoxyWindow", Root, UiFactory.Theme.Background);
		UiFactory.SetRect(Window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UiFactory.Theme.WindowWidth, UiFactory.Theme.WindowHeight));
		BuildTopBar(Window);
		BuildBody(Window);
		Window.gameObject.SetActive(false);
	}

	/// <summary>确保存在 EventSystem，否则 UGUI 按钮点击无效（复用已有的，不重复创建）。</summary>
	private void EnsureEventSystem() {
		if (EventSystem.current != null) {
			return;
		}
		GameObject es_go = new GameObject("RoxyLibEventSystem");
		es_go.AddComponent<EventSystem>();
		es_go.AddComponent<StandaloneInputModule>();
		UnityEngine.Object.DontDestroyOnLoad(es_go);
	}

	/// <summary>顶栏：标题（可翻译）+ 搜索框 + 关闭按钮。</summary>
	private void BuildTopBar(RectTransform window) {
		RectTransform bar = UiFactory.CreatePanel("TopBar", window, UiFactory.Theme.Panel);
		UiFactory.SetRect(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, UiFactory.Theme.TopBarHeight));

		TopTitle = UiFactory.CreateText("Title", bar, T("RoxyLib.Window.Title", "RoxyLib Settings"), UiFactory.Theme.FontSize, UiFactory.Theme.Text, TextAnchor.MiddleLeft);
		TopTitle.rectTransform.SetRectAt(12, 0, 200, UiFactory.Theme.TopBarHeight);

		RectTransform search_holder = UiFactory.CreateRect("SearchHolder", bar);
		UiFactory.SetRect(search_holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(300, 30));
		SearchField = UiFactory.CreateInputField("Search", search_holder, "", value => OnSearchChanged(value ?? ""));
		UiFactory.Stretch(SearchField.GetComponent<RectTransform>());
		UpdateSearchPlaceholder();

		Button close = UiFactory.CreateButton("Close", bar, "✕", () => SetOpenSettingsValue(false), 30, 30);
		UiFactory.SetRect(close.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(30, 30));
	}

	/// <summary>主体：左栏（mod 列表）+ 右栏（规则面板），各自带滚动。</summary>
	private void BuildBody(RectTransform window) {
		RectTransform body = UiFactory.CreateRect("Body", window);
		body.anchorMin = Vector2.zero;
		body.anchorMax = Vector2.one;
		body.offsetMin = Vector2.zero;
		body.offsetMax = new Vector2(0, -UiFactory.Theme.TopBarHeight); // 让出顶栏

		// ---- 左栏 ----
		RectTransform left = UiFactory.CreatePanel("Left", body, UiFactory.Theme.Panel);
		UiFactory.SetRect(left, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(UiFactory.Theme.ModListWidth, 0));

		RectTransform left_header = UiFactory.CreatePanel("LeftHeader", left, UiFactory.Theme.PanelLight);
		UiFactory.SetRect(left_header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 28));
		LeftTitle = UiFactory.CreateText("Title", left_header, T("RoxyLib.Left.Title", "MODS"), UiFactory.Theme.FontSizeSmall, UiFactory.Theme.TextDim, TextAnchor.MiddleLeft);
		UiFactory.Stretch(LeftTitle.rectTransform);
		LeftTitle.rectTransform.offsetMin = new Vector2(10, 0);

		RectTransform left_body = UiFactory.CreateRect("LeftBody", left);
		left_body.anchorMin = Vector2.zero;
		left_body.anchorMax = Vector2.one;
		left_body.offsetMin = Vector2.zero;
		left_body.offsetMax = new Vector2(0, -28); // 让出左栏标题
		(_, ModListContent) = UiFactory.CreateScrollView("Scroll", left_body, UiFactory.Theme.Panel);

		// ---- 右栏 ----
		RectTransform right = UiFactory.CreateRect("Right", body);
		right.anchorMin = new Vector2(0, 0);
		right.anchorMax = Vector2.one;
		right.offsetMin = new Vector2(UiFactory.Theme.ModListWidth, 0); // 从左栏右侧开始
		right.offsetMax = Vector2.zero;

		RectTransform right_header = UiFactory.CreatePanel("RightHeader", right, UiFactory.Theme.PanelLight);
		UiFactory.SetRect(right_header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 28));
		RightTitle = UiFactory.CreateText("Title", right_header, "", UiFactory.Theme.FontSizeSmall, UiFactory.Theme.TextDim, TextAnchor.MiddleLeft);
		UiFactory.Stretch(RightTitle.rectTransform);
		RightTitle.rectTransform.offsetMin = new Vector2(12, 0);

		RectTransform right_body = UiFactory.CreateRect("RightBody", right);
		right_body.anchorMin = Vector2.zero;
		right_body.anchorMax = Vector2.one;
		right_body.offsetMin = Vector2.zero;
		right_body.offsetMax = new Vector2(0, -28); // 让出右栏标题
		(_, RuleContent) = UiFactory.CreateScrollView("Scroll", right_body, UiFactory.Theme.Background);
	}

	// ---------------- 刷新 ----------------

	/// <summary>重建整个界面：先刷顶栏静态文案（跟随语言），再重建左栏与右栏。</summary>
	private void RefreshAll() {
		RefreshTopTexts();
		BuildModList();
		BuildRulePanel();
	}

	/// <summary>刷新顶栏/左栏标题与搜索占位（语言切换时需要重读翻译）。</summary>
	private void RefreshTopTexts() {
		if (TopTitle != null) {
			TopTitle.text = T("RoxyLib.Window.Title", "RoxyLib Settings");
		}
		if (LeftTitle != null) {
			LeftTitle.text = T("RoxyLib.Left.Title", "MODS");
		}
		UpdateSearchPlaceholder();
	}

	/// <summary>刷新搜索框占位文本（语言切换时）。</summary>
	private void UpdateSearchPlaceholder() {
		if (SearchField != null && SearchField.placeholder is Text placeholder) {
			placeholder.text = T("RoxyLib.Search.Placeholder", "Search...");
		}
	}

	/// <summary>搜索框输入变化：更新关键词并重建左右栏。</summary>
	private void OnSearchChanged(string value) {
		SearchText = value;
		BuildModList();
		BuildRulePanel();
	}

	/// <summary>翻译简写。</summary>
	private string T(string key, string fallback) => RoxyLang.Translate(key, fallback);

	// ---------------- 左栏 ----------------

	/// <summary>重建左栏 mod 列表（RoxyLib 置顶 + 其余按 DisplayName 字典序，可被搜索过滤）。</summary>
	private void BuildModList() {
		if (ModListContent == null) {
			return;
		}
		Clear(ModListContent);
		List<RoxyHost> hosts = RoxyLib.GetHosts().ToList();
		hosts.Sort((a, b) => {
			bool a_self = a.ModId == RoxyLib.MOD_ID;
			bool b_self = b.ModId == RoxyLib.MOD_ID;
			if (a_self != b_self) {
				return a_self ? -1 : 1; // RoxyLib 自身排最前
			}
			return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
		});
		float y = 0;
		foreach (RoxyHost host in hosts) {
			if (SearchText.Length > 0 && host.DisplayName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0) {
				continue; // 搜索过滤
			}
			y += AddModEntry(host, y);
		}
		UiFactory.SetContentHeight(ModListContent, y);
		// 若当前选中 mod 已被反注册，回落到 RoxyLib 自身
		if (!RoxyLib.GetHosts().Any(host => host.ModId == SelectedModId)) {
			SelectedModId = RoxyLib.MOD_ID;
		}
	}

	/// <summary>添加一个 mod 行（点击选中，高亮当前选中项）。</summary>
	private float AddModEntry(RoxyHost host, float y) {
		float height = UiFactory.Theme.RowHeight;
		bool selected = host.ModId == SelectedModId;
		RectTransform row = UiFactory.CreatePanel("Mod_" + host.ModId, ModListContent!, selected ? UiFactory.Theme.AccentDim : UiFactory.Theme.PanelLight);
		UiFactory.PlaceRow(row, ModListContent!, y, height);
		Button button = row.gameObject.AddComponent<Button>();
		button.targetGraphic = row.GetComponent<Image>();
		Text label = UiFactory.CreateText("Label", row, host.DisplayName, UiFactory.Theme.FontSize, UiFactory.Theme.Text);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(10, 0);
		string mod_id = host.ModId;
		button.onClick.AddListener(() => {
			SelectedModId = mod_id;
			BuildModList();
			BuildRulePanel();
		});
		return height;
	}

	// ---------------- 右栏规则面板 ----------------

	/// <summary>重建右栏规则面板：按 Category 分组，组内逐行渲染对应控件。</summary>
	private void BuildRulePanel() {
		if (RuleContent == null) {
			return;
		}
		Clear(RuleContent);
		// 先收起所有下拉弹层（它们挂在 Canvas 根上，Clear 清不到），避免残留
		foreach (RoxyDropdown dropdown in Dropdowns) {
			dropdown.Close();
		}
		Dropdowns.Clear();
		RoxyHost? host = RoxyLib.GetHosts().FirstOrDefault(candidate => candidate.ModId == SelectedModId);
		if (host == null) {
			AddEmptyHint(0);
			return;
		}
		if (RightTitle != null) {
			RightTitle.text = host.DisplayName;
		}
		List<RuleInfo> rules = RoxyRules.GetRules(SelectedModId).ToList();
		List<RuleInfo> filtered = rules.Where(rule => SearchText.Length == 0 || MatchesSearch(rule)).ToList();
		if (filtered.Count == 0) {
			AddEmptyHint(0);
			return;
		}
		// 分组（保持首次出现顺序）
		List<string> category_order = new List<string>();
		Dictionary<string, List<RuleInfo>> groups = new Dictionary<string, List<RuleInfo>>();
		foreach (RuleInfo rule in filtered) {
			if (!groups.TryGetValue(rule.Category, out var list)) {
				list = new List<RuleInfo>();
				groups[rule.Category] = list;
				category_order.Add(rule.Category);
			}
			list.Add(rule);
		}
		float y = 0;
		foreach (string category in category_order) {
			y += AddSectionHeader(category, y);
			foreach (RuleInfo rule in groups[category]) {
				y += AddRuleRow(rule, y);
			}
		}
		UiFactory.SetContentHeight(RuleContent, y);
	}

	/// <summary>搜索匹配：规则名或分类名包含关键词。</summary>
	private bool MatchesSearch(RuleInfo rule) {
		return rule.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
			|| rule.Category.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>无规则时的占位提示。</summary>
	private void AddEmptyHint(float y) {
		Text hint = UiFactory.CreateText("Empty", RuleContent!, T("RoxyLib.Rule.Empty", "No rules"), UiFactory.Theme.FontSizeSmall, UiFactory.Theme.TextDim, TextAnchor.MiddleCenter);
		UiFactory.PlaceRow(hint.rectTransform, RuleContent!, y, UiFactory.Theme.RowHeight);
	}

	/// <summary>分类标题行（可翻译）。</summary>
	private float AddSectionHeader(string category, float y) {
		float height = UiFactory.Theme.SectionHeight;
		RectTransform row = UiFactory.CreatePanel("Section_" + category, RuleContent!, UiFactory.Theme.PanelLight);
		UiFactory.PlaceRow(row, RuleContent!, y, height);
		Button button = row.gameObject.AddComponent<Button>();
		button.targetGraphic = row.GetComponent<Image>();
		Text label = UiFactory.CreateText("Label", row, T($"{SelectedModId}.Category.{category}", category), UiFactory.Theme.FontSize, UiFactory.Theme.Text, TextAnchor.MiddleLeft);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(10, 0);
		return height;
	}

	// ---------------- 规则行 ----------------

	/// <summary>规则行布局：左侧名称标签（可翻译）+ 中部控件区 + 右侧重置按钮（值为默认值）。</summary>
	private float AddRuleRow(RuleInfo rule, float y) {
		float height = rule.RuleType == RoxyRuleType.Color ? 120 : UiFactory.Theme.RowHeight; // 颜色规则需要更多高度（4 条滑条）
		RectTransform row = UiFactory.CreatePanel("Rule_" + rule.Name, RuleContent!, UiFactory.Theme.Background);
		UiFactory.PlaceRow(row, RuleContent!, y, height);

		Text label = UiFactory.CreateText("Label", row, T($"{SelectedModId}.{rule.Category}.{rule.Name}", rule.Name), UiFactory.Theme.FontSize, UiFactory.Theme.Text);
		UiFactory.SetRect(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(200, height));

		RectTransform control = UiFactory.CreateRect("Control", row);
		control.anchorMin = new Vector2(0, 0);
		control.anchorMax = new Vector2(1, 1);
		control.offsetMin = new Vector2(215, 0); // 让出左侧标签
		control.offsetMax = new Vector2(-34, 0); // 让出右侧重置按钮

		switch (rule.RuleType) {
		case RoxyRuleType.Switch: BuildSwitchControl(rule, control); break;
		case RoxyRuleType.SliderInt: BuildNumericControl(rule, control, true); break;
		case RoxyRuleType.SliderFloat: BuildNumericControl(rule, control, false); break;
		case RoxyRuleType.Options: BuildOptionsControl(rule, control); break;
		case RoxyRuleType.Color: BuildColorControl(rule, control); break;
		case RoxyRuleType.String: BuildStringControl(rule, control); break;
		}

		// 值重置按钮：把规则值恢复为字段初始默认值（↺）
		Button reset = UiFactory.CreateButton("Reset", row, "↺", () => { rule.SetValue(rule.DefaultValue, true); BuildRulePanel(); }, 26, 26);
		UiFactory.SetRect(reset.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(26, 26));
		return height;
	}

	/// <summary>Switch 规则控件：开关 + 快捷键绑定按钮 + 快捷键清除按钮（✕ 表示设为空 None）。</summary>
	private void BuildSwitchControl(RuleInfo rule, RectTransform control) {
		RectTransform holder = UiFactory.CreateRect("SwitchHolder", control);
		holder.anchorMin = Vector2.zero;
		holder.anchorMax = Vector2.one;
		holder.offsetMin = Vector2.zero;
		holder.offsetMax = Vector2.zero;

		RectTransform toggle_holder = UiFactory.CreateRect("ToggleHolder", holder);
		UiFactory.SetRect(toggle_holder, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 30));
		Toggle toggle = UiFactory.CreateToggle("Toggle", toggle_holder, (bool)rule.GetValue(), v => rule.SetValue(v, true), 24);
		UiFactory.Stretch(toggle.GetComponent<RectTransform>());

		if (rule.Keybind != null) {
			RoxyKeybind keybind = rule.Keybind;
			// 快捷键绑定按钮：点击进入捕获态（显示"请按键"，按 Esc 取消）
			// 注：闭包引用了 key_btn，须在创建 lambda 前定值（null! 先初始化，点击时必已赋值）
			Button key_btn = null!;
			key_btn = UiFactory.CreateButton("Keybind", holder, keybind.Combination.ToString(), () => {
				CapturingKeybind = true;
				CaptureRule = rule;
				// 立即反馈"请按键"提示
				if (key_btn.transform.Find("Label") is Transform label_transform
					&& label_transform.GetComponent<Text>() is Text label) {
					label.text = T("RoxyLib.Keybind.Press", "Press Key...");
				}
			}, 150, 26);
			UiFactory.SetRect(key_btn.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(150, 26));
			// 快捷键清除按钮：✕ 表示"设为空（None）"。库不规定任何默认快捷键，
			// 因此清除 = 设为 None（组合为空），而不是恢复默认键。
			Button key_clear = UiFactory.CreateButton("KeyClear", holder, "✕", () => { keybind.Combination = KeyCombination.None; RoxyLib.Dirty = true; BuildRulePanel(); }, 26, 26);
			UiFactory.SetRect(key_clear.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(228, 0), new Vector2(26, 26));
		}
	}

	/// <summary>数值规则控件：滑条 + 精确输入框（is_int 决定整型/浮点）。</summary>
	private void BuildNumericControl(RuleInfo rule, RectTransform control, bool is_int) {
		float min = Convert.ToSingle(rule.Min);
		float max = Convert.ToSingle(rule.Max);
		if (max <= min) {
			max = min + 1; // 防呆：Min==Max 时保证可拖动
		}
		float initial = Convert.ToSingle(rule.GetValue());
		RectTransform holder = UiFactory.CreateRect("NumericHolder", control);
		holder.anchorMin = Vector2.zero;
		holder.anchorMax = Vector2.one;
		holder.offsetMin = Vector2.zero;
		holder.offsetMax = Vector2.zero;

		RectTransform field_holder = UiFactory.CreateRect("FieldHolder", holder);
		field_holder.anchorMin = new Vector2(1, 0.5f);
		field_holder.anchorMax = new Vector2(1, 0.5f);
		field_holder.pivot = new Vector2(1, 0.5f);
		field_holder.sizeDelta = new Vector2(70, 24);
		InputField field = UiFactory.CreateInputField("ValueField", field_holder, FormatValue(rule.GetValue()), value => {
			if (TryParseNumber(value, is_int, out object parsed)) {
				rule.SetValue(parsed, true);
			}
		});
		UiFactory.Stretch(field.GetComponent<RectTransform>());

		RectTransform slider_holder = UiFactory.CreateRect("SliderHolder", holder);
		slider_holder.anchorMin = new Vector2(0, 0.5f);
		slider_holder.anchorMax = new Vector2(1, 0.5f);
		slider_holder.pivot = new Vector2(0.5f, 0.5f);
		slider_holder.anchoredPosition = new Vector2(-35, 0);
		slider_holder.sizeDelta = new Vector2(-70, 24);
		Slider slider = UiFactory.CreateSlider("Slider", slider_holder, min, max, initial, v => {
			object new_value = is_int ? (object)(int)Math.Round(v) : v;
			if (rule.SetValue(new_value, true)) {
				field.text = FormatValue(new_value); // 拖滑条时同步输入框
			}
		});
		UiFactory.Stretch(slider.GetComponent<RectTransform>());
	}

	/// <summary>Options（枚举）规则控件：下拉框（RoxyDropdown，弹层挂 Canvas 根）。</summary>
	private void BuildOptionsControl(RuleInfo rule, RectTransform control) {
		string[] options = rule.EnumNames ?? Array.Empty<string>();
		int current_index = Math.Max(0, Array.IndexOf(options, rule.GetValue()?.ToString() ?? ""));
		RoxyDropdown dropdown = new RoxyDropdown(control, Root!, options, current_index, index => {
			if (index >= 0 && index < options.Length) {
				rule.SetValue(options[index], true);
			}
		});
		Dropdowns.Add(dropdown);
	}

	/// <summary>Color 规则控件：色块预览 + RGBA 四条滑条/输入框。</summary>
	private void BuildColorControl(RuleInfo rule, RectTransform control) {
		Color color = (Color)rule.GetValue();
		RectTransform holder = UiFactory.CreateRect("ColorHolder", control);
		holder.anchorMin = Vector2.zero;
		holder.anchorMax = Vector2.one;
		holder.offsetMin = Vector2.zero;
		holder.offsetMax = Vector2.zero;

		RectTransform swatch = UiFactory.CreatePanel("Swatch", holder, color);
		UiFactory.SetRect(swatch, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 30), new Vector2(36, 26));

		float[] channels = { color.r, color.g, color.b, color.a };
		string[] names = { "R", "G", "B", "A" };
		for (int i = 0; i < 4; i++) {
			int idx = i;
			RectTransform row = UiFactory.CreateRect("ColorRow" + i, holder);
			UiFactory.SetRect(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(44, -4 - i * 26), new Vector2(0, 22));
			UiFactory.CreateText("Name", row, names[i], UiFactory.Theme.FontSizeSmall, UiFactory.Theme.TextDim, TextAnchor.MiddleLeft)
				.rectTransform.SetRectAt(0, 0, 20, 22);

			RectTransform slider_holder = UiFactory.CreateRect("SliderHolder", row);
			slider_holder.anchorMin = new Vector2(0, 0.5f);
			slider_holder.anchorMax = new Vector2(1, 0.5f);
			slider_holder.pivot = new Vector2(0.5f, 0.5f);
			slider_holder.anchoredPosition = new Vector2(-40, 0);
			slider_holder.sizeDelta = new Vector2(-80, 20);
			Slider slider = UiFactory.CreateSlider("Slider", slider_holder, 0f, 1f, channels[i], v => {
				Color c = (Color)rule.GetValue();
				Color nc = ApplyChannel(c, idx, v);
				if (rule.SetValue(nc, true)) {
					swatch.GetComponent<Image>().color = nc; // 实时更新色块
				}
			});
			UiFactory.Stretch(slider.GetComponent<RectTransform>());

			RectTransform field_holder = UiFactory.CreateRect("FieldHolder", row);
			field_holder.anchorMin = new Vector2(1, 0.5f);
			field_holder.anchorMax = new Vector2(1, 0.5f);
			field_holder.pivot = new Vector2(1, 0.5f);
			field_holder.sizeDelta = new Vector2(36, 20);
			InputField field = UiFactory.CreateInputField("Field", field_holder, ((int)(channels[i] * 255)).ToString(), value => {
				if (int.TryParse(value, out int byte_val) && byte_val >= 0 && byte_val <= 255) {
					Color c = (Color)rule.GetValue();
					Color nc = ApplyChannel(c, idx, byte_val / 255f);
					if (rule.SetValue(nc, true)) {
						swatch.GetComponent<Image>().color = nc;
					}
				}
			});
			UiFactory.Stretch(field.GetComponent<RectTransform>());
		}
	}

	/// <summary>替换颜色某个通道（0=R,1=G,2=B,3=A）。</summary>
	private static Color ApplyChannel(Color c, int idx, float value) {
		switch (idx) {
		case 0: c.r = value; break;
		case 1: c.g = value; break;
		case 2: c.b = value; break;
		default: c.a = value; break;
		}
		return c;
	}

	/// <summary>String 规则控件：输入框（失焦提交）。</summary>
	private void BuildStringControl(RuleInfo rule, RectTransform control) {
		RectTransform holder = UiFactory.CreateRect("StringHolder", control);
		holder.anchorMin = Vector2.zero;
		holder.anchorMax = Vector2.one;
		holder.offsetMin = Vector2.zero;
		holder.offsetMax = Vector2.zero;
		InputField field = UiFactory.CreateInputField("Field", holder, rule.GetValue()?.ToString() ?? "", value => rule.SetValue(value, true));
		UiFactory.Stretch(field.GetComponent<RectTransform>());
	}

	// ---------------- 快捷键捕获 ----------------

	/// <summary>捕获态每帧：ESC 取消捕获（不改动当前快捷键），或按下其他键绑定为新组合。</summary>
	private void UpdateKeybindCapture() {
		if (CaptureRule == null || CaptureRule.Keybind == null) {
			return;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
			// ESC 仅取消本次捕获，保持原快捷键不变。
			// 清除快捷键请用行尾的 ✕ 按钮（设为 None），这里不承担"设为空"职责。
			CapturingKeybind = false;
			CaptureRule = null;
			BuildRulePanel();
			return;
		}
		foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
			// 排除修饰键/ESC/鼠标键（它们不作为主键单独绑定）
			if (key == KeyCode.LeftControl || key == KeyCode.RightControl || key == KeyCode.LeftShift
				|| key == KeyCode.RightShift || key == KeyCode.LeftAlt || key == KeyCode.RightAlt
				|| key == KeyCode.Escape || key == KeyCode.Mouse0 || key == KeyCode.Mouse1) {
				continue;
			}
			if (UnityEngine.Input.GetKeyDown(key)) {
				string combo = key.ToString();
				// 按住修饰键时组合成 Ctrl/Shift/Alt + 主键
				if (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)) combo = "LeftControl+" + combo;
				if (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift)) combo = "LeftShift+" + combo;
				if (UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt)) combo = "LeftAlt+" + combo;
				CaptureRule.Keybind.Combination = KeyCombination.Parse(combo);
				CapturingKeybind = false;
				CaptureRule = null;
				RoxyLib.Dirty = true;
				BuildRulePanel();
				return;
			}
		}
	}

	// ---------------- 工具 ----------------

	private static string FormatValue(object value) => value is float f ? f.ToString("0.##") : value?.ToString() ?? "";

	private static bool TryParseNumber(string text, bool is_int, out object value) {
		value = null!;
		if (string.IsNullOrWhiteSpace(text)) {
			return false;
		}
		try {
			value = is_int ? int.Parse(text.Trim()) : float.Parse(text.Trim());
			return true;
		} catch {
			return false;
		}
	}

	/// <summary>立即销毁父节点的全部子物体（重建面板用）。</summary>
	private static void Clear(RectTransform parent) {
		for (int i = parent.childCount - 1; i >= 0; i--) {
			UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
		}
	}
}

/// <summary>RectTransform 便捷布局扩展：以"左上角为基准、x 向右 y 向下"放置并给定尺寸。</summary>
internal static class RectTransformExtensions {
	public static void SetRectAt(this RectTransform rt, float x, float y, float width, float height) {
		rt.anchorMin = new Vector2(0, 1);
		rt.anchorMax = new Vector2(0, 1);
		rt.pivot = new Vector2(0, 1);
		rt.anchoredPosition = new Vector2(x, -y);
		rt.sizeDelta = new Vector2(width, height);
	}
}
