using System;
using System.Collections.Generic;
using System.Reflection;
using RoxyLib.Attribute;
using RoxyLib.Setting;

namespace RoxyLib.Overlay;

public enum OverlayType {
	LeftTop,
	RightTop,
	AnyPosition
}

public enum OverlayAlignment {
	TopLeft,    TopMiddle,    TopRight,
	MiddleLeft, MiddleMiddle, MiddleRight,
	BottomLeft, BottomMiddle, BottomRight,
}

public sealed class OverlayInfo {
	public OverlayType OverlayType { get; }
	public string ModId { get; internal set; } = string.Empty;
	public string Name { get; internal set; } = string.Empty;
	public RuleInfo? RuleB { get; internal set; }   // 显示/隐藏

	// LeftTop / RightTop
	public uint? Order { get; internal set; }       // 排列顺序

	// AnyPosition
	public RuleInfo? RuleX { get; internal set; }   // X坐标
	public RuleInfo? RuleY { get; internal set; }   // Y坐标
	public RuleInfo? RuleA { get; internal set; }   // 对齐方式
	public RuleInfo? RuleS { get; internal set; }   // 字体大小
	public RuleInfo? RuleC { get; internal set; }   // 颜色

	public event Action<OverlayInfo>? PositionChanged;

	public void NotifyChange() {
		PositionChanged?.Invoke(this);
	}

	public event Action<OverlayInfo, string>? TextChanged;

	public void SetText(string text) {
		TextChanged?.Invoke(this, text);
	}

	public OverlayInfo(OverlayType overlay_type) {
		OverlayType = overlay_type;
	}
}

public static class OverlayManager {
	private static readonly List<OverlayInfo> Overlays = [ ];

	public static uint LeftCount { get; private set; }
	public static uint RightCount { get; private set; }
	public static event Action? UpdateAll;

	public static void NotifyUpdate() {
		UpdateAll?.Invoke();
	}

	public static IReadOnlyList<OverlayInfo> GetOverlays() {
		return Overlays.AsReadOnly();
	}

	internal static void ScanAssembly(Assembly assembly) {
		foreach (Type type in assembly.GetTypes()) {
			var mod = type.GetCustomAttribute<RoxyModAttribute>();
			if (mod is null)
				continue;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public)) {
				if (field.FieldType != typeof(OverlayInfo))
					continue;
				var info = (OverlayInfo)field.GetValue(null)!;
				info.ModId = mod.ModId;
				info.Name = field.Name;
				Register(info);
			}
		}

		foreach (RuleInfo rule in RuleManager.GetRules(RoxyLib.MOD_ID)) {
			if (rule.Name is "LeftTopColor" or "RightTopColor")
				rule.ValueChanged += (_, _) => NotifyUpdate();
		}

		NotifyUpdate();
	}

	public static void RemoveMod(string mod_id) {
		Overlays.RemoveAll(o => o.ModId == mod_id);
		NotifyUpdate(); // 结构变化 → 通知全部重排
	}

	private static void Register(OverlayInfo info) {
		string name = info.Name;
		foreach (RuleInfo rule in RuleManager.GetRules(info.ModId)) {
			if (rule.Name == name + "_B")
				info.RuleB = rule;
		}

		switch (info.OverlayType) {
		case OverlayType.LeftTop:
			info.Order = LeftCount++; break;
		case OverlayType.RightTop:
			info.Order = RightCount++; break;
		case OverlayType.AnyPosition:
			foreach (RuleInfo rule in RuleManager.GetRules(info.ModId)) {
				if (rule.Name == name + "_X")
					info.RuleX = rule;
				else if (rule.Name == name + "_Y")
					info.RuleY = rule;
				else if (rule.Name == name + "_A")
					info.RuleA = rule;
				else if (rule.Name == name + "_S")
					info.RuleS = rule;
				else if (rule.Name == name + "_C")
					info.RuleC = rule;
			}
			break;
		}

		foreach (RuleInfo? rule in new[] { info.RuleX, info.RuleY, info.RuleA, info.RuleS, info.RuleC }) {
				rule?.ValueChanged += (_, _) => info.NotifyChange();
		}
		info.RuleB?.ValueChanged += (_, _) => NotifyUpdate(); // 结构变化 → 通知全部重排

		Overlays.Add(info);
	}
}
