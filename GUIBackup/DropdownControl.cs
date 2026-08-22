using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoxyLib.Gui;

/// <summary>
/// 库级下拉组件：一个触发按钮 + 点击后弹出的选项列表。
///
/// 弹出列表不挂在触发按钮的父级下——那样会被 ScrollRect 的 Mask 裁剪掉，
/// 而是挂在 Canvas 根（popup_root，ScreenSpaceOverlay 全屏）上，
/// 用屏幕坐标定位在触发按钮正下方，保证在滚动区域内也能完整弹出、不被裁剪。
///
/// 生命周期：创建时构建触发按钮 → 点击触发按钮展开/收起 → 选中选项回调上层。
/// </summary>
public sealed class RoxyDropdown {
	private readonly RectTransform PopupRoot;    // 弹出列表的挂载根（Canvas 根，全屏、pivot 0.5,0.5）
	private readonly RectTransform TriggerButton; // 触发按钮（挂在规则行的 control 区域里）
	private readonly List<string> Options;        // 选项文本
	private readonly Action<int> OnChanged;       // 选中回调（参数为选中索引）
	private bool Open;                            // 弹层是否展开

	/// <summary>当前选中索引（-1 表示无选中）。</summary>
	public int SelectedIndex { get; private set; }

	/// <param name="parent">触发按钮的父节点（规则行的 control 区域）</param>
	/// <param name="popup_root">弹出列表挂载根（Canvas 根，ScreenSpaceOverlay 下用屏幕坐标）</param>
	/// <param name="options">选项文本</param>
	/// <param name="initial_index">当前选中索引</param>
	/// <param name="on_changed">选择回调（选中索引）</param>
	public RoxyDropdown(RectTransform parent, RectTransform popup_root, IList<string> options, int initial_index, Action<int> on_changed) {
		PopupRoot = popup_root;
		Options = new List<string>(options);
		OnChanged = on_changed;
		SelectedIndex = Mathf.Clamp(initial_index, 0, Mathf.Max(0, Options.Count - 1));

		TriggerButton = CreateTrigger(parent);
		// 必须在 TriggerButton 赋值后再刷新文案：
		// CreateTrigger 执行期间字段还没赋值（右侧表达式未算完），此时 UpdateLabel 会 null.Find 抛异常。
		UpdateLabel();
	}

	// ---------------- 触发按钮 ----------------

	private RectTransform CreateTrigger(Transform parent) {
		RectTransform btn_rt = UiFactory.CreateRect("RoxyDropdown", parent);
		btn_rt.anchorMin = new Vector2(0, 0.5f);
		btn_rt.anchorMax = new Vector2(1, 0.5f);
		btn_rt.pivot = new Vector2(0.5f, 0.5f);
		btn_rt.anchoredPosition = Vector2.zero;
		btn_rt.sizeDelta = new Vector2(-8, 26);

		Button button = btn_rt.gameObject.AddComponent<Button>();
		Image bg = btn_rt.gameObject.AddComponent<Image>();
		bg.color = UiFactory.Theme.PanelLight;
		button.targetGraphic = bg;

		// 左对齐的当前选项文本（内容由 UpdateLabel 填充）
		Text label = UiFactory.CreateText("Label", btn_rt, "", UiFactory.Theme.FontSize, UiFactory.Theme.Text, TextAnchor.MiddleLeft);
		UiFactory.Stretch(label.rectTransform);
		label.rectTransform.offsetMin = new Vector2(10, 0);
		label.rectTransform.offsetMax = new Vector2(-30, 0);

		// 右侧下拉箭头
		Text arrow = UiFactory.CreateText("Arrow", btn_rt, "▼", UiFactory.Theme.FontSize, UiFactory.Theme.TextDim, TextAnchor.MiddleRight);
		UiFactory.Stretch(arrow.rectTransform);
		arrow.rectTransform.offsetMin = new Vector2(-28, 0);
		arrow.rectTransform.offsetMax = new Vector2(-8, 0);

		button.onClick.AddListener(() => Toggle());
		return btn_rt;
	}

	/// <summary>把当前选中项刷新到触发按钮的文案上。</summary>
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
	/// 在触发按钮正下方展开选项列表。
	/// 定位采用 Unity 标准的「屏幕坐标 → 父本地坐标」换算，避免手算 pivot/anchor 出错：
	/// 1) WorldToScreenPoint 把触发按钮中心转成屏幕坐标（ScreenSpaceOverlay 传 null 相机，原点在左下）；
	/// 2) ScreenPointToLocalPointInRectangle 把屏幕点换算到 PopupRoot 的本地坐标系
	///    （自动处理 PopupRoot 的 pivot 与缩放），返回的 local 可直接作为
	///    anchor(0.5,0.5) 子节点的 anchoredPosition——即相对屏幕中心的偏移。
	/// </summary>
	private void ShowPopup() {
		HidePopup(); // 先清理上次的弹出项，避免重复展开叠加
		if (PopupRoot == null) {
			return;
		}

		// 触发按钮中心在屏幕上的位置
		Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, TriggerButton.position);

		// 换算到 PopupRoot 本地坐标（其 pivot 是 0.5,0.5，即屏幕中心）
		RectTransformUtility.ScreenPointToLocalPointInRectangle(PopupRoot, screen, null, out Vector2 local);

		float y_offset = 0f;
		for (int i = 0; i < Options.Count; i++) {
			int idx = i;
			bool selected = idx == SelectedIndex;
			Button opt = UiFactory.CreateButton("RoxyDropdownOpt_" + idx, PopupRoot, Options[idx] + (selected ? " ✓" : ""), () => Select(idx), 180, 28);
			RectTransform opt_rt = opt.GetComponent<RectTransform>();
			// 单点 anchor (0.5,0.5)：anchoredPosition 即相对 PopupRoot 中心（= 屏幕中心）的偏移
			opt_rt.anchorMin = new Vector2(0.5f, 0.5f);
			opt_rt.anchorMax = new Vector2(0.5f, 0.5f);
			opt_rt.pivot = new Vector2(0, 1); // 选项的左上角对齐到定位点
			// 从触发按钮中心下方 20px 起，逐项向下排列
			opt_rt.anchoredPosition = local + new Vector2(0, -20f - y_offset);
			opt_rt.sizeDelta = new Vector2(180, 28);
			if (selected) {
				opt.GetComponent<Image>().color = UiFactory.Theme.Accent; // 当前选中项高亮
			}
			y_offset += 30f;
		}
	}

	/// <summary>移除所有弹出选项（按名字前缀识别，避免误删 Canvas 根上的其他 UI）。</summary>
	private void HidePopup() {
		if (PopupRoot == null) {
			return;
		}
		for (int i = PopupRoot.childCount - 1; i >= 0; i--) {
			if (PopupRoot.GetChild(i).name.StartsWith("RoxyDropdownOpt_")) {
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
		UpdateLabel();   // 触发按钮文案跟随
		Open = false;
		HidePopup();     // 收起弹层
		OnChanged?.Invoke(index); // 通知上层（规则面板据此 SetValue 并触发防抖保存）
	}

	/// <summary>外部收起（如面板重建时调用，避免弹层残留）。</summary>
	public void Close() {
		Open = false;
		HidePopup();
	}
}
