using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>
/// RLA 设置主窗口。
///
/// 结构：全屏 Canvas（ScreenSpaceOverlay）→ 遮罩 + 阴影 + 圆角窗口
/// （顶栏 + 左侧 mod 列表卡片 + 右侧规则卡片面板）。
/// 窗口开关由 RoxyLib 自己的 OpenSettings 规则值驱动；语言切换刷新通过订阅 Language 规则的 ValueChanged。
/// 每帧由 RoxyLib.Tick 调用 OnUpdate：处理快捷键捕获、ESC 关闭、脏标记刷新。
/// </summary>
public sealed class RoxyGui {
	// ---- 关键 UI 引用（构建时保存，供刷新/开关用）----
	private RectTransform? Root;           // Canvas 根（下拉弹层挂这里）
	private RectTransform? Window;         // 主窗口
	private RectTransform? Modal;          // 全屏遮罩
	private RectTransform? WindowShadow;   // 窗口阴影（需随窗口一起开关）
	private RectTransform? ModListContent; // 左栏 mod 列表的滚动 content
	private RectTransform? RuleContent;    // 右栏规则面板的滚动 content
	private Text? RightTitle;              // 右栏标题（当前选中 mod 名）
	private Text? TopTitle;                // 顶栏标题（跟随语言）
	private Text? LeftTitle;               // 左栏 "MODS"（跟随语言）
	private InputField? SearchField;       // 搜索框（占位文本跟随语言）

	// ---- 状态 ----
	private string SelectedModId = RoxyLib.MOD_ID; // 当前选中的 mod（默认选中 RoxyLib 自身）
	private string SearchText = "";                // 搜索关键词
	private bool Dirty;                            // 需要重建界面（注册/语言/值变更后置位）
	private bool CapturingKeybind;                 // 是否处于快捷键捕获状态
	private RuleInfo? CaptureRule;                 // 正在捕获快捷键的规则
	private readonly List<RoxyDropdown> Dropdowns = new List<RoxyDropdown>(); // 已创建的下拉（重建时统一收起）

	public bool IsOpen { get; private set; }

	public RoxyGui() {
		// 构造函数只订阅事件，不建 UI：此时语言表可能尚未加载（LoadLangDir 在注册后才跑），
		// 建 UI 会触发翻译读取，所以 UI 延迟到首次打开窗口时构建。
		RoxyLib.ModRegistered += OnModRegistered;
		RoxyLib.ModUnregistered += _ => Dirty = true;
		RoxyLib.RevisionChanged += _ => Dirty = true;
	}

	/// <summary>外部入口（UMM 设置按钮等）打开设置界面。</summary>
	public void OpenFromExternal() {
		SetOpenSettingsValue(true);
	}

	/// <summary>UI 未构建时惰性构建（首次打开窗口时，此时语言已加载）。Root 为 null 即未构建。</summary>
	private void EnsureRoot() {
		if (Root == null) {
			CreateRootUi();
		}
	}

	/// <summary>某 mod 注册完成：若为 RoxyLib 自身，订阅其 OpenSettings（开关窗口）与 Language（切语言刷新）。</summary>
	private void OnModRegistered(ModHost host) {
		if (host.ModId == RoxyLib.MOD_ID) {
			SubscribeOpenSettings();
			SubscribeLanguage();
		}
		Dirty = true;
	}

	/// <summary>每帧驱动（由 RoxyLib.Tick 调用）。</summary>
	public void OnUpdate(float dt) {
		if (CapturingKeybind) {
			UpdateKeybindCapture();
		}
		if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape) && !CapturingKeybind) {
			SetOpenSettingsValue(false);
		}
		if (Dirty && IsOpen) {
			Dirty = false;
			RefreshAll();
		}
	}

	// ---------------- 开关 / 语言驱动 ----------------

	private void SubscribeOpenSettings() {
		foreach (RuleInfo rule in RuleManager.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "OpenSettings") {
				rule.ValueChanged += (sender, args) => SetOpen((bool)args);
				SetOpen((bool)rule.GetValue());
				break;
			}
		}
	}

	/// <summary>订阅 Language 规则：值变更 → 标记刷新（重读翻译）。</summary>
	private void SubscribeLanguage() {
		foreach (RuleInfo rule in RuleManager.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "Language") {
				rule.ValueChanged += (sender, args) => Dirty = true;
				break;
			}
		}
	}

	private void SetOpenSettingsValue(bool value) {
		foreach (RuleInfo rule in RuleManager.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name == "OpenSettings") {
				rule.SetValue(value, true);
				return;
			}
		}
	}

	/// <summary>直接设置窗口显示状态（由 OpenSettings 规则值驱动）。</summary>
	private void SetOpen(bool open) {
		IsOpen = open;
		if (open) {
			EnsureRoot(); // 打开前确保 UI 已构建（首次打开时创建，此时语言已加载）
		}
		if (Window != null) {
			Window.gameObject.SetActive(open);
		}
		if (Modal != null) {
			Modal.gameObject.SetActive(open);
		}
		if (WindowShadow != null) {
			WindowShadow.gameObject.SetActive(open); // 阴影必须跟随窗口开关，否则关窗后残留
		}
		if (open) {
			RefreshAll();
		}
	}

	// ---------------- UI 构建 ----------------

	/// <summary>创建 Canvas、遮罩、窗口阴影与圆角主窗口，构建顶栏与主体。</summary>
	private void CreateRootUi() {
		EnsureEventSystem();
		GameObject canvas_go = new GameObject("RoxyLibCanvas");
		UnityEngine.Canvas canvas = canvas_go.AddComponent<UnityEngine.Canvas>();
		canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30000;
		canvas_go.AddComponent<GraphicRaycaster>();
		UnityEngine.Object.DontDestroyOnLoad(canvas_go);
		Root = canvas_go.GetComponent<RectTransform>();

		// 全屏遮罩（默认隐藏）
		Modal = UiFactory.CreatePanel("Modal", Root, new Color(0f, 0f, 0f, 0.45f));
		UiFactory.Stretch(Modal);
		Modal.gameObject.SetActive(false);

		// 窗口阴影（比窗口大一圈、略向下偏移，先创建 → 在窗口下层）
		WindowShadow = UiFactory.CreateRoundedPanel("WindowShadow", Root, new Color(0f, 0f, 0f, 0.55f));
		UiFactory.SetRect(WindowShadow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(UiFactory.WindowWidth + 18, UiFactory.WindowHeight + 18));

		// 主窗口：居中固定尺寸，圆角背景
		Window = UiFactory.CreateRoundedPanel("RoxyWindow", Root, UiFactory.Background);
		UiFactory.SetRect(Window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UiFactory.WindowWidth, UiFactory.WindowHeight));
		BuildTopBar(Window);
		BuildBody(Window);
		Window.gameObject.SetActive(false);
	}

	private void EnsureEventSystem() {
		if (EventSystem.current != null) {
			return;
		}
		GameObject es_go = new GameObject("RoxyLibEventSystem");
		es_go.AddComponent<EventSystem>();
		es_go.AddComponent<StandaloneInputModule>();
		UnityEngine.Object.DontDestroyOnLoad(es_go);
	}

	/// <summary>顶栏：标题（可翻译）+ 搜索框 + 关闭按钮，底部一条 Accent 强调线。</summary>
	private void BuildTopBar(RectTransform window) {
		RectTransform bar = UiFactory.CreateRect("TopBar", window);
		bar.anchorMin = new Vector2(0, 1);
		bar.anchorMax = new Vector2(1, 1);
		bar.pivot = new Vector2(0.5f, 1);
		bar.anchoredPosition = Vector2.zero;
		bar.sizeDelta = new Vector2(0, UiFactory.TopBarHeight);

		// 标题（大号、亮白、带小 Accent 竖条装饰）
		RectTransform title_deco = UiFactory.CreatePanel("TitleDeco", bar, UiFactory.Accent);
		title_deco.anchorMin = new Vector2(0, 0.5f);
		title_deco.anchorMax = new Vector2(0, 0.5f);
		title_deco.pivot = new Vector2(0.5f, 0.5f);
		title_deco.anchoredPosition = new Vector2(22, 0);
		title_deco.sizeDelta = new Vector2(4, 22);

		TopTitle = UiFactory.CreateText("Title", bar, T("RoxyLib.Window.Title", "RoxyLib Settings"), UiFactory.FontSizeTitle, UiFactory.Text, TextAnchor.MiddleLeft);
		TopTitle.rectTransform.SetRectAt(36, 0, 280, UiFactory.TopBarHeight);

		// 搜索框
		RectTransform search_holder = UiFactory.CreateRect("SearchHolder", bar);
		UiFactory.SetRect(search_holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(300, 32));
		SearchField = UiFactory.CreateInputField("Search", search_holder, "", value => OnSearchChanged(value ?? ""));
		UiFactory.Stretch(SearchField.GetComponent<RectTransform>());
		UpdateSearchPlaceholder();

		// 关闭按钮（圆角，悬停时呈 Accent）
		RectTransform close_rt = UiFactory.CreateRect("CloseHolder", bar);
		close_rt.anchorMin = new Vector2(1, 0.5f);
		close_rt.anchorMax = new Vector2(1, 0.5f);
		close_rt.pivot = new Vector2(1, 0.5f);
		close_rt.anchoredPosition = new Vector2(-14, 0);
		close_rt.sizeDelta = new Vector2(32, 32);
		Button close = UiFactory.CreateButton("Close", close_rt, "✕", () => SetOpenSettingsValue(false), 32, 32);
		UiFactory.Stretch(close.GetComponent<RectTransform>());

		// 顶栏底部强调线
		RectTransform divider = UiFactory.CreatePanel("Divider", window, new Color(UiFactory.Accent.r, UiFactory.Accent.g, UiFactory.Accent.b, 0.5f));
		divider.anchorMin = new Vector2(0, 1);
		divider.anchorMax = new Vector2(1, 1);
		divider.pivot = new Vector2(0.5f, 1);
		divider.anchoredPosition = new Vector2(0, -UiFactory.TopBarHeight);
		divider.sizeDelta = new Vector2(0, 1);
	}

	/// <summary>主体：左侧 mod 列表卡片 + 右侧规则卡片面板（各自带滚动），四周留边距。</summary>
	private void BuildBody(RectTransform window) {
		RectTransform body = UiFactory.CreateRect("Body", window);
		body.anchorMin = Vector2.zero;
		body.anchorMax = Vector2.one;
		body.offsetMin = new Vector2(12, 12);
		body.offsetMax = new Vector2(-12, -(UiFactory.TopBarHeight + 1));

		// ---- 左栏：圆角卡片 ----
		RectTransform left = UiFactory.CreateRoundedPanel("Left", body, UiFactory.Panel);
		left.anchorMin = new Vector2(0, 0);
		left.anchorMax = new Vector2(0, 1);
		left.pivot = new Vector2(0, 0.5f);
		left.anchoredPosition = Vector2.zero;
		left.sizeDelta = new Vector2(UiFactory.ModListWidth, 0);

		BuildColumnHeader(left, "LeftHeader", out LeftTitle, T("RoxyLib.Left.Title", "MODS"));
		RectTransform left_body = UiFactory.CreateRect("LeftBody", left);
		left_body.anchorMin = Vector2.zero;
		left_body.anchorMax = Vector2.one;
		left_body.offsetMin = new Vector2(6, 6);
		left_body.offsetMax = new Vector2(0, -40);
		(_, ModListContent) = UiFactory.CreateScrollView("Scroll", left_body, UiFactory.Panel);

		// ---- 右栏：圆角卡片 ----
		RectTransform right = UiFactory.CreateRoundedPanel("Right", body, new Color(UiFactory.Background.r, UiFactory.Background.g, UiFactory.Background.b, 1f));
		right.anchorMin = Vector2.zero;
		right.anchorMax = Vector2.one;
		right.offsetMin = new Vector2(UiFactory.ModListWidth + 12, 0);
		right.offsetMax = Vector2.zero;

		BuildColumnHeader(right, "RightHeader", out RightTitle, "");
		RectTransform right_body = UiFactory.CreateRect("RightBody", right);
		right_body.anchorMin = Vector2.zero;
		right_body.anchorMax = Vector2.one;
		right_body.offsetMin = new Vector2(8, 8);
		right_body.offsetMax = new Vector2(0, -40);
		(_, RuleContent) = UiFactory.CreateScrollView("Scroll", right_body, new Color(UiFactory.Background.r, UiFactory.Background.g, UiFactory.Background.b, 1f));
	}

	/// <summary>构建卡片顶部的栏目标题（小号 Accent 文字 + 底部细分隔线）。</summary>
	private void BuildColumnHeader(RectTransform column, string name, out Text? title, string text) {
		RectTransform header = UiFactory.CreateRect(name, column);
		header.anchorMin = new Vector2(0, 1);
		header.anchorMax = new Vector2(1, 1);
		header.pivot = new Vector2(0.5f, 1);
		header.anchoredPosition = new Vector2(0, 0);
		header.sizeDelta = new Vector2(0, 36);

		title = UiFactory.CreateText("Title", header, text, UiFactory.FontSizeSmall, UiFactory.Accent, TextAnchor.MiddleLeft);
		UiFactory.Stretch(title.rectTransform);
		title.rectTransform.offsetMin = new Vector2(14, 0);
		title.rectTransform.offsetMax = new Vector2(-14, -2);

		RectTransform line = UiFactory.CreatePanel("Line", header, new Color(1f, 1f, 1f, 0.06f));
		line.anchorMin = new Vector2(0, 0);
		line.anchorMax = new Vector2(1, 0);
		line.pivot = new Vector2(0.5f, 0);
		line.anchoredPosition = new Vector2(0, 1);
		line.sizeDelta = new Vector2(-24, 1);
	}

	// ---------------- 刷新 ----------------

	private void RefreshAll() {
		RefreshTopTexts();
		BuildModList();
		BuildRulePanel();
	}

	private void RefreshTopTexts() {
		if (TopTitle != null) {
			TopTitle.text = T("RoxyLib.Window.Title", "RoxyLib Settings");
		}
		if (LeftTitle != null) {
			LeftTitle.text = T("RoxyLib.Left.Title", "MODS");
		}
		UpdateSearchPlaceholder();
	}

	private void UpdateSearchPlaceholder() {
		if (SearchField != null && SearchField.placeholder is Text placeholder) {
			placeholder.text = T("RoxyLib.Search.Placeholder", "Search...");
		}
	}

	private void OnSearchChanged(string value) {
		SearchText = value;
		BuildModList();
		BuildRulePanel();
	}

	private string T(string key, string fallback) {
		return LanguageManager.Translate(key, fallback);
	}

	// ---------------- 左栏 ----------------

	/// <summary>重建左栏 mod 列表（RoxyLib 置顶 + 其余按 DisplayName 字典序，可被搜索过滤）。</summary>
	private void BuildModList() {
		if (ModListContent == null) {
			return;
		}
		Clear(ModListContent);
		List<ModHost> hosts = RoxyLib.GetHosts().ToList();
		hosts.Sort((a, b) => {
			bool a_self = a.ModId == RoxyLib.MOD_ID;
			bool b_self = b.ModId == RoxyLib.MOD_ID;
			if (a_self != b_self) {
				return a_self ? -1 : 1;
			}
			return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
		});
		float y = 4;
		foreach (ModHost host in hosts) {
			if (SearchText.Length > 0 && host.DisplayName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0) {
				continue;
			}
			y += AddModEntry(host, y) + 4;
		}
		UiFactory.SetContentHeight(ModListContent, y);
		if (!RoxyLib.GetHosts().Any(host => host.ModId == SelectedModId)) {
			SelectedModId = RoxyLib.MOD_ID;
		}
	}

	/// <summary>添加一个 mod 卡片行（选中高亮 + 左侧 Accent 条 + 加粗文字）。</summary>
	private float AddModEntry(ModHost host, float y) {
		float height = UiFactory.RowHeight - 4;
		bool selected = host.ModId == SelectedModId;
		RectTransform row = UiFactory.CreateRoundedPanel("Mod_" + host.ModId, ModListContent!, selected ? UiFactory.AccentDim : UiFactory.PanelLight);
		UiFactory.PlaceRow(row, ModListContent!, y, height);
		// 只缩左右，保留 PlaceRow 用 sizeDelta 算好的高度（offset 的 y 会覆盖高度，不能置 0）
		row.offsetMin = new Vector2(6, row.offsetMin.y);
		row.offsetMax = new Vector2(-6, row.offsetMax.y);

		Button button = row.gameObject.AddComponent<Button>();
		UiFactory.ApplyButtonStyle(button, row.GetComponent<Image>());

		// 选中时左侧 Accent 条
		if (selected) {
			RectTransform bar = UiFactory.CreatePanel("SelectedBar", row, UiFactory.Accent);
			bar.anchorMin = new Vector2(0, 0.18f);
			bar.anchorMax = new Vector2(0, 0.82f);
			bar.pivot = new Vector2(0, 0.5f);
			bar.anchoredPosition = new Vector2(2, 0);
			bar.sizeDelta = new Vector2(3, 0);
		}

		Text label = UiFactory.CreateText("Label", row, host.DisplayName, UiFactory.FontSize, selected ? UiFactory.Text : UiFactory.TextDim);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(12, 0);
		label.rectTransform.offsetMax = new Vector2(-8, 0);
		label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;

		string mod_id = host.ModId;
		button.onClick.AddListener(() => {
			SelectedModId = mod_id;
			BuildModList();
			BuildRulePanel();
		});
		return height;
	}

	// ---------------- 右栏规则面板 ----------------

	/// <summary>重建右栏规则面板：按 Category 分组，组内逐行渲染对应控件（卡片式）。</summary>
	private void BuildRulePanel() {
		if (RuleContent == null) {
			return;
		}
		Clear(RuleContent);
		foreach (RoxyDropdown dropdown in Dropdowns) {
			dropdown.Close();
		}
		Dropdowns.Clear();
		ModHost? host = RoxyLib.GetHosts().FirstOrDefault(candidate => candidate.ModId == SelectedModId);
		if (host == null) {
			AddEmptyHint(6);
			return;
		}
		if (RightTitle != null) {
			RightTitle.text = host.DisplayName;
		}
		List<RuleInfo> rules = RuleManager.GetRules(SelectedModId).ToList();
		List<RuleInfo> filtered = rules.Where(rule => SearchText.Length == 0 || MatchesSearch(rule)).ToList();
		if (filtered.Count == 0) {
			AddEmptyHint(6);
			return;
		}
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
		float y = 6;
		foreach (string category in category_order) {
			y += AddSectionHeader(category, y) + 4;
			foreach (RuleInfo rule in groups[category]) {
				y += AddRuleRow(rule, y) + 4;
			}
		}
		UiFactory.SetContentHeight(RuleContent, y);
	}

	private bool MatchesSearch(RuleInfo rule) {
		return rule.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
			|| rule.Category.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void AddEmptyHint(float y) {
		Text hint = UiFactory.CreateText("Empty", RuleContent!, T("RoxyLib.Rule.Empty", "No rules"), UiFactory.FontSize, UiFactory.TextDim, TextAnchor.MiddleCenter);
		UiFactory.PlaceRow(hint.rectTransform, RuleContent!, y, 40);
	}

	/// <summary>分类标题：Accent 小字胶囊，圆角浅底。</summary>
	private float AddSectionHeader(string category, float y) {
		float height = UiFactory.SectionHeight;
		RectTransform row = UiFactory.CreateRoundedPanel("Section_" + category, RuleContent!, new Color(1f, 1f, 1f, 0.045f));
		UiFactory.PlaceRow(row, RuleContent!, y, height);
		row.offsetMin = new Vector2(6, row.offsetMin.y);
		row.offsetMax = new Vector2(-6, row.offsetMax.y);
		Text label = UiFactory.CreateText("Label", row, T($"{SelectedModId}.Category.{category}", category), UiFactory.FontSizeSmall, UiFactory.Accent, TextAnchor.MiddleLeft);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(14, 0);
		return height;
	}

	// ---------------- 规则行 ----------------

	/// <summary>规则卡片行：名称标签（可翻译）+ 中部控件区 + 右侧重置按钮。</summary>
	private float AddRuleRow(RuleInfo rule, float y) {
		float height = rule.RuleType == RuleType.Color ? 152 : UiFactory.RowHeight;
		RectTransform row = UiFactory.CreateRoundedPanel("Rule_" + rule.Name, RuleContent!, UiFactory.PanelLight);
		UiFactory.PlaceRow(row, RuleContent!, y, height);
		row.offsetMin = new Vector2(6, row.offsetMin.y);
		row.offsetMax = new Vector2(-6, row.offsetMax.y);

		Text label = UiFactory.CreateText("Label", row, T($"{SelectedModId}.{rule.Category}.{rule.Name}", rule.Name), UiFactory.FontSize, UiFactory.Text);
		UiFactory.SetRect(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(190, height));

		RectTransform control = UiFactory.CreateRect("Control", row);
		control.anchorMin = new Vector2(0, 0);
		control.anchorMax = new Vector2(1, 1);
		control.offsetMin = new Vector2(220, 0);
		control.offsetMax = new Vector2(-44, 0);

		switch (rule.RuleType) {
		case RuleType.Switch: BuildSwitchControl(rule, control); break;
		case RuleType.SliderInt: BuildNumericControl(rule, control, true); break;
		case RuleType.SliderFloat: BuildNumericControl(rule, control, false); break;
		case RuleType.Options: BuildOptionsControl(rule, control); break;
		case RuleType.Color: BuildColorControl(rule, control); break;
		case RuleType.String: BuildStringControl(rule, control); break;
		}

		Button reset = UiFactory.CreateButton("Reset", row, "↺", () => { rule.SetValue(rule.DefaultValue, true); BuildRulePanel(); }, 28, 28);
		UiFactory.SetRect(reset.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(28, 28));
		return height;
	}

	/// <summary>Switch 规则控件：开关 + 快捷键绑定按钮 + 快捷键清除按钮（✕ = 设为空 None）。</summary>
	private void BuildSwitchControl(RuleInfo rule, RectTransform control) {
		RectTransform holder = UiFactory.CreateRect("SwitchHolder", control);
		holder.anchorMin = Vector2.zero;
		holder.anchorMax = Vector2.one;
		holder.offsetMin = Vector2.zero;
		holder.offsetMax = Vector2.zero;

		RectTransform toggle_holder = UiFactory.CreateRect("ToggleHolder", holder);
		UiFactory.SetRect(toggle_holder, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(56, 30));
		Toggle toggle = UiFactory.CreateToggle("Toggle", toggle_holder, (bool)rule.GetValue(), v => rule.SetValue(v, true), 22);
		UiFactory.Stretch(toggle.GetComponent<RectTransform>());

		if (rule.Keybind != null) {
			Keybind keybind = rule.Keybind;
			Button key_btn = null!;
			key_btn = UiFactory.CreateButton("Keybind", holder, keybind.Combination.ToString(), () => {
				CapturingKeybind = true;
				CaptureRule = rule;
				if (key_btn.transform.Find("Label") is Transform label_transform
					&& label_transform.GetComponent<Text>() is Text label) {
					label.text = T("RoxyLib.Keybind.Press", "Press Key...");
				}
			}, 160, 30);
			UiFactory.SetRect(key_btn.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(68, 0), new Vector2(160, 30));
			// 快捷键为 None 时不显示清除按钮（没有可清除的内容）
			if (keybind.Combination.Keys != null && keybind.Combination.Keys.Length > 0) {
				Button key_clear = UiFactory.CreateButton("KeyClear", holder, "✕", () => { keybind.Combination = KeyCombination.None; RoxyLib.Dirty = true; BuildRulePanel(); }, 30, 30);
				UiFactory.SetRect(key_clear.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(236, 0), new Vector2(30, 30));
			}
		}
	}

	/// <summary>数值规则控件：滑条 + 精确输入框（is_int 决定整型/浮点）。</summary>
	private void BuildNumericControl(RuleInfo rule, RectTransform control, bool is_int) {
		float min = Convert.ToSingle(rule.Min);
		float max = Convert.ToSingle(rule.Max);
		if (max <= min) {
			max = min + 1;
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
		field_holder.sizeDelta = new Vector2(70, 28);
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
		slider_holder.sizeDelta = new Vector2(-70, 28);
		Slider slider = UiFactory.CreateSlider("Slider", slider_holder, min, max, initial, v => {
			object new_value = is_int ? (object)(int)Math.Round(v) : v;
			if (rule.SetValue(new_value, true)) {
				field.text = FormatValue(new_value);
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

	/// <summary>Color 规则控件：右上角色块预览 + RGBA 四条「标签-滑块-输入框」行。</summary>
	/// <remarks>布局要点：行横贯控件区不偏移（不溢出到重置按钮）；滑块精确夹在标签与输入框之间。</remarks>
	private void BuildColorControl(RuleInfo rule, RectTransform control) {
		Color color = (Color)rule.GetValue();
		RectTransform holder = UiFactory.CreateRect("ColorHolder", control);
		UiFactory.Stretch(holder);

		// 色块预览：右上角，行从 y=44 开始，互不遮挡
		RectTransform swatch = UiFactory.CreateRoundedPanel("Swatch", holder, color);
		UiFactory.SetRect(swatch, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(48, 32));

		const float label_w = 24f;   // 左侧 RGBA 标签宽
		const float field_w = 46f;   // 右侧输入框宽
		const float gap = 8f;        // 控件间距
		const float row_h = 24f;     // 行高
		const float row_gap = 4f;    // 行间距
		const float top = 44f;       // 首行距顶部（给右上角色块留空间）

		float[] channels = { color.r, color.g, color.b, color.a };
		string[] names = { "R", "G", "B", "A" };

		for (int i = 0; i < 4; i++) {
			int idx = i;

			// 每行横贯整个控件区（anchoredPosition.x = 0，不偏移、不溢出）
			RectTransform row = UiFactory.CreateRect("ColorRow" + i, holder);
			row.anchorMin = new Vector2(0, 1);
			row.anchorMax = new Vector2(1, 1);
			row.pivot = new Vector2(0.5f, 1);
			row.anchoredPosition = new Vector2(0, -(top + i * (row_h + row_gap)));
			row.sizeDelta = new Vector2(0, row_h);

			// 左侧标签
			UiFactory.CreateText("Name", row, names[i], UiFactory.FontSizeSmall, UiFactory.TextDim, TextAnchor.MiddleLeft)
				.rectTransform.SetRectAt(0, 0, label_w, row_h);

			// 右侧输入框（右对齐，始终在控件区内 → 不再被重置按钮遮住）
			RectTransform field_holder = UiFactory.CreateRect("FieldHolder", row);
			UiFactory.SetRect(field_holder, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(field_w, row_h));
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

			// 中间滑块：精确填充「标签右缘」到「输入框左缘」之间
			RectTransform slider_holder = UiFactory.CreateRect("SliderHolder", row);
			slider_holder.anchorMin = new Vector2(0, 0.5f);
			slider_holder.anchorMax = new Vector2(1, 0.5f);
			slider_holder.pivot = new Vector2(0.5f, 0.5f);
			slider_holder.anchoredPosition = new Vector2((label_w - field_w) * 0.5f, 0);
			slider_holder.sizeDelta = new Vector2(-(label_w + field_w + gap * 2f), row_h - 2);
			Slider slider = UiFactory.CreateSlider("Slider", slider_holder, 0f, 1f, channels[i], v => {
				Color c = (Color)rule.GetValue();
				Color nc = ApplyChannel(c, idx, v);
				if (rule.SetValue(nc, true)) {
					swatch.GetComponent<Image>().color = nc;
				}
			});
			UiFactory.Stretch(slider.GetComponent<RectTransform>());
		}
	}

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

	private void UpdateKeybindCapture() {
		if (CaptureRule == null || CaptureRule.Keybind == null) {
			return;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
			// ESC 仅取消本次捕获，保持原快捷键不变；清除请用 ✕ 按钮（设为 None）
			CapturingKeybind = false;
			CaptureRule = null;
			BuildRulePanel();
			return;
		}
		foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
			if (key == KeyCode.LeftControl || key == KeyCode.RightControl || key == KeyCode.LeftShift
				|| key == KeyCode.RightShift || key == KeyCode.LeftAlt || key == KeyCode.RightAlt
				|| key == KeyCode.Escape || key == KeyCode.Mouse0 || key == KeyCode.Mouse1) {
				continue;
			}
			if (UnityEngine.Input.GetKeyDown(key)) {
				string combo = key.ToString();
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

	private static string FormatValue(object value) {
		return value is float f ? f.ToString("0.##") : value?.ToString() ?? "";
	}

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

	private static void Clear(RectTransform parent) {
		for (int i = parent.childCount - 1; i >= 0; i--) {
			UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
		}
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
