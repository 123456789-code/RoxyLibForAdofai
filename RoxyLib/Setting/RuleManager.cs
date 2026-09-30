using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RoxyLib.Attribute;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>规则全局管理器：注册、按 mod 查询、扫描 [RoxyMod] 规则类。</summary>
public static class RuleManager {
	private static readonly Dictionary<string, List<RuleInfo>> Rules = [ ];

	public static IReadOnlyList<RuleInfo> GetRules(string? mod_id = null) {
		return (mod_id is null)
			? Rules.Values.SelectMany(list => list).ToList().AsReadOnly()
			: Rules.TryGetValue(mod_id, out var list) ? list.AsReadOnly() : Array.Empty<RuleInfo>();
	}

	/// <summary>扫描程序集，注册所有 [RoxyMod] 规则类（字段 → RuleFactory.Create）。</summary>
	internal static void ScanAssembly(Assembly assembly) {
		foreach (Type type in SafeGetTypes(assembly)) {
			var mod_attribute = type.GetCustomAttribute<RoxyModAttribute>();
			if (mod_attribute is null)
				continue;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public)) {
				var rule_attribute = field.GetCustomAttribute<RoxyRuleAttribute>();
				if (rule_attribute is null)
					continue;
				Register(RuleFactory.Create(mod_attribute.ModId, field, rule_attribute));
			}
		}
	}

	public static void RemoveMod(string mod_id) {
		Rules.Remove(mod_id);
	}

	/// <summary>注册一条规则（编程式通道的入口；扫描通道也汇入此处）。</summary>
	public static void Register(RuleInfo info) {
		if (info.IsNumericSlider && (info.Min is null || info.Max is null))
			throw new InvalidOperationException($"RoxyRule '{info.ModId}.{info.Name}' (Slider) requires Min and Max");
		if (info is SwitchRule)
			info.Keybind ??= new Keybind(KeyCombination.None);
		if (!Rules.TryGetValue(info.ModId, out var list)) {
			list = [];
			Rules[info.ModId] = list;
		}
		list.Add(info);
	}

	/// <summary>安全遍历程序集类型（跳过无法加载的类型，避免拖垮整个扫描）。</summary>
	private static Type[] SafeGetTypes(Assembly assembly) {
		try {
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException e) {
			Debug.LogWarning($"[RoxyLib] failed to load some types from {assembly.FullName}: {e.Message}");
			return e.Types.Where(t => t != null).Cast<Type>().ToArray();
		}
	}
}
