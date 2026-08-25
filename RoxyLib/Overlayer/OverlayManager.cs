using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RoxyLib.Attribute;
using RoxyLib.Setting;
using RoxyLib.Utils;

namespace RoxyLib.Overlay;

public enum OverlayType {
	LeftTop, RightTop, AnyPosition
}

public enum OverlayAlignment {
	Left, Middle, Right,
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
	public RuleInfo? RuleS { get; internal set; }   // 字体大小
	public RuleInfo? RuleA { get; internal set; }   // 对齐方式
	public RuleInfo? RuleC { get; internal set; }   // 字体颜色

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
	private static readonly Dictionary<string, List<OverlayInfo>> Overlays = [ ];

	public static uint LeftCount { get; private set; }
	public static uint RightCount { get; private set; }
	public static event Action? UpdateAll;

	public static void NotifyUpdate() {
		UpdateAll?.Invoke();
	}

	public static IReadOnlyList<OverlayInfo> GetOverlays(string? mod_id = null) {
		return (mod_id is null)
			? Overlays.Values.SelectMany(list => list).ToList().AsReadOnly()
			: Overlays.TryGetValue(mod_id, out var list) ? list.AsReadOnly() : Array.Empty<OverlayInfo>();
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

		NotifyUpdate();
	}

	public static void RemoveMod(string mod_id) {
		Overlays.Remove(mod_id);
		NotifyUpdate(); // 结构变化 → 通知全部重排
	}

	internal static void Register(OverlayInfo info) {
		string mod_id = info.ModId;
		string name = info.Name;
		string desplay_en = LanguageManager.Translate($"Overlay.{info.ModId}.{name}", name, LanguageEnum.en_us);
		string desplay_zh = LanguageManager.Translate($"Overlay.{info.ModId}.{name}", name, LanguageEnum.zh_cn);

		var rule = new RuleInfo(mod_id, $"{name}.Switch", "Overlay", typeof(bool), false, null, null);
		RuleManager.Register(rule);
		LanguageManager.AddContent($"{mod_id}.Overlay.{name}.Switch",
			$"{desplay_en} - Switch", $"{desplay_zh} - 开关", null, null
		);
		rule.ValueChanged += (_, _) => NotifyUpdate(); // 结构变化 → 通知全部重排
		info.RuleB = rule;
		
		switch (info.OverlayType) {
		case OverlayType.LeftTop:
			info.Order = LeftCount++; break;
		case OverlayType.RightTop:
			info.Order = RightCount++; break;
		case OverlayType.AnyPosition:
			rule = new RuleInfo(mod_id, $"{name}.X", "Overlay", typeof(int), 0, 0, 65535);
			RuleManager.Register(rule);
			LanguageManager.AddContent($"{mod_id}.Overlay.{name}.X",
				$"{desplay_en} - X", $"{desplay_zh} - X", null, null
			);
			rule.ValueChanged += (_, _) => info.NotifyChange();
			info.RuleX = rule;

			rule = new RuleInfo(mod_id, $"{name}.Y", "Overlay", typeof(int), 0, 0, 65535);
			RuleManager.Register(rule);
			LanguageManager.AddContent($"{mod_id}.Overlay.{name}.Y",
				$"{desplay_en} - Y", $"{desplay_zh} - Y", null, null
			);
			rule.ValueChanged += (_, _) => info.NotifyChange();
			info.RuleY = rule;

			rule = new RuleInfo(mod_id, $"{name}.Size", "Overlay", typeof(uint), 10, 0, 65535);
			RuleManager.Register(rule);
			LanguageManager.AddContent($"{mod_id}.Overlay.{name}.Size",
				$"{desplay_en} - Size", $"{desplay_zh} - 字体大小", null, null
			);
			rule.ValueChanged += (_, _) => info.NotifyChange();
			info.RuleS = rule;

			rule = new RuleInfo(mod_id, $"{name}.Alignment", "Overlay", typeof(OverlayAlignment), OverlayAlignment.Middle, null, null);
			RuleManager.Register(rule);
			LanguageManager.AddContent($"{mod_id}.Overlay.{name}.Alignment",
				$"{desplay_en} - Alignment", $"{desplay_zh} - 对齐方式", null, null
			);
			rule.ValueChanged += (_, _) => info.NotifyChange();
			info.RuleA = rule;

			rule = new RuleInfo(mod_id, $"{name}.Color", "Overlay", typeof(Color), new Color(1, 1, 1, 1), null, null);
			RuleManager.Register(rule);
			LanguageManager.AddContent($"{mod_id}.Overlay.{name}.Color",
				$"{desplay_en} - Color", $"{desplay_zh} - 字体颜色", null, null
			);
			rule.ValueChanged += (_, _) => info.NotifyChange();
			info.RuleC = rule;

			break;
		}

		if (!Overlays.TryGetValue(mod_id, out var list)) {
			list = [];
			Overlays[mod_id] = list;
		}
		list.Add(info);
	}
}
