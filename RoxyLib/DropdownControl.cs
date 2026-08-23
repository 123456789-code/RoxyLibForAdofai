using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoxyLib.Gui;

/// <summary>
/// 库级下拉组件：一个圆角触发按钮 + 点击后弹出的圆角菜单。
///
/// 弹出菜单不挂在触发按钮的父级下——那样会被 ScrollRect 的 Mask 裁剪掉，
/// 而是挂在 Canvas 根（popup_root，ScreenSpaceOverlay 全屏）上，
/// 用屏幕坐标定位在触发按钮正下方，保证在滚动区域内也能完整弹出、不被裁剪。
///
/// 生命周期：创建时构建触发按钮 → 点击展开/收起 → 选中选项回调上层。
/// </summary>
public sealed class RoxyDropdown {
	private readonly RectTransform PopupRoot;    // 弹出菜单的挂载根（Canvas 根，全屏、pivot 0.5,0.5）
	private readonly RectTransform TriggerButton; // 触发按钮（挂在规则行的 control 区域里）
	private readonly List<string> Options;        // 选项文本
	private readonly Action<int> OnChanged;       // 选中回调（参数为选中索引）
	private bool Open;                            // 菜单是否展开

	/// <summary>当前选中索引（-1 表示无选中）。</summary>
	public int SelectedIndex { get; private set; }

	// 弹出菜单布局参数
	private const float MenuWidth = 190f;
	private const float ItemHeight = 30f;
	private const float ItemGap = 2f;
	private const float Padding = 6f;

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
		bg.color = UiFactory.Theme.PanelLight;
		UiFactory.ApplyButtonStyle(button, bg);

		Text label = UiFactory.CreateText("Label", btn_rt, "", UiFactory.Theme.FontSize, UiFactory.Theme.Text, TextAnchor.MiddleLeft);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(12, 0);
		label.rectTransform.offsetMax = new Vector2(-30, 0);

		Text arrow = UiFactory.CreateText("Arrow", btn_rt, "▾", UiFactory.Theme.FontSizeSmall, UiFactory.Theme.TextDim, TextAnchor.MiddleRight);
		UiFactory.Stretch(arrow.rectTransform);
		arrow.rectTransform.offsetMin = new Vector2(-26, 0);
		arrow.rectTransform.offsetMax = new Vector2(-10, 0);

		button.onClick.AddListener(() => Toggle());
		return btn_rt;
	}

	/// <summary>把当前选中项刷新到触发按钮文案。</summary>
	private void UpdateLabel() {
		Text? label = TriggerButton.Find("Label")?.GetComponent<Text>();
		if (label != null) {
			label.text = SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex] : "";
		}
	}

	// ---------------- 展开 / 收起 ----------------

	private void Toggle() {
		Open = !Open;
		if (Open) {
			ShowPopup();
		} else {
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
		float menu_height = Padding * 2 + Options.Count * ItemHeight + (Options.Count - 1) * ItemGap;
		RectTransform panel = UiFactory.CreateRoundedPanel("RoxyDropdownPanel", PopupRoot, UiFactory.Theme.Panel);
		panel.anchorMin = new Vector2(0.5f, 0.5f);
		panel.anchorMax = new Vector2(0.5f, 0.5f);
		panel.pivot = new Vector2(0, 1); // 容器左上角对齐定位点
		panel.anchoredPosition = local + new Vector2(0, -20f);
		panel.sizeDelta = new Vector2(MenuWidth, menu_height);

		// 选项按钮
		for (int i = 0; i < Options.Count; i++) {
			int idx = i;
			bool selected = idx == SelectedIndex;
			string text = (selected ? "✓  " : "") + Options[idx];
			Button opt = UiFactory.CreateButton("RoxyDropdownOpt_" + idx, panel, text, () => Select(idx), MenuWidth - Padding * 2, ItemHeight);
			RectTransform opt_rt = opt.GetComponent<RectTransform>();
			opt_rt.anchorMin = new Vector2(0, 1);
			opt_rt.anchorMax = new Vector2(1, 1);
			opt_rt.pivot = new Vector2(0.5f, 1);
			opt_rt.anchoredPosition = new Vector2(0, -(Padding + idx * (ItemHeight + ItemGap)));
			// 只缩左右，保留 anchoredPosition 算好的 y 定位（offset 的 y 会覆盖 y 位置，不能置 0）
			opt_rt.offsetMin = new Vector2(Padding, opt_rt.offsetMin.y);
			opt_rt.offsetMax = new Vector2(-Padding, opt_rt.offsetMax.y);
			opt_rt.sizeDelta = new Vector2(0, ItemHeight);
			if (selected) {
				opt.GetComponent<Image>().color = UiFactory.Theme.Accent;
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
