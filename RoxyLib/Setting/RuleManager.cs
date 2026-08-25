using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RoxyLib.Utils;
using RoxyLib.Attribute;

namespace RoxyLib.Setting;

public enum RuleType {
	Switch,      // bool，绑定快捷键
	SliderInt,   // 整型，需 Min/Max，滑块+输入框
	SliderFloat, // 浮点，需 Min/Max，滑块+输入框
	Options,     // 枚举，下拉框
	Color,       // UnityEngine.Color，RGBA 滑块+输入框
	String,      // string，输入框
}

/// <summary>一条规则：包装静态字段，提供读值、赋值（可选触发事件）、元数据。</summary>
public sealed class RuleInfo {
	public string ModId { get; }
	public string Name { get; }
	public string Category { get; }
	public RuleType RuleType { get; }
	public Type FieldType { get; }
	public object DefaultValue { get; }
	public object? Min { get; }
	public object? Max { get; }
	public string[]? EnumNames { get; }
	public Keybind? Keybind { get; internal set; }

	public event EventHandler<object>? ValueChanged;

	private readonly FieldInfo Field;

	internal RuleInfo(string mod_id, FieldInfo field, RoxyRuleAttribute attribute, object default_value) {
		ModId = mod_id;
		Field = field;
		Name = field.Name;
		Category = attribute.Category;
		FieldType = field.FieldType;
		DefaultValue = default_value;
		RuleType = DetectType(field.FieldType);
		if (RuleType == RuleType.Options) {
			EnumNames = Enum.GetNames(field.FieldType);
		}
		else if (RuleType is RuleType.SliderInt or RuleType.SliderFloat) {
			Min = attribute.Min;
			Max = attribute.Max;
		}
	}

	/// <summary>读取当前值</summary>
	public object GetValue() {
		return Field.GetValue(null);
	}

	/// <summary>转换为 string</summary>
	public string GetValueString() {
		object value = GetValue();
		switch (RuleType) {
		case RuleType.Switch:
		case RuleType.SliderInt:
		case RuleType.SliderFloat:
		case RuleType.Options:
			return value.ToString();
		case RuleType.Color:
			var color = (Color)value;
			return "#" + ColorUtility.ToHtmlStringRGBA(color);
		case RuleType.String:
			return (string)value;
		default:
			throw new Exception("Impossible");
		}
	}

	/// <summary>
	/// 赋值
	/// </summary>
	/// <param name="value">新值</param>
	/// <param name="trigger">是否触发事件</param>
	public bool SetValue(object value, bool trigger) {
		object? converted = ConvertValue(value);
		if (converted == null)
			return false;
		Field.SetValue(null, converted);
		if (trigger)
			ValueChanged?.Invoke(this, converted);
		return true;
	}

	/// <summary>从字符串解析数值</summary>
	public void LoadFromString(string raw) {
		try {
			SetValue(RuleType switch {
				RuleType.Switch => bool.Parse(raw),
				RuleType.SliderInt => int.Parse(raw),
				RuleType.SliderFloat => float.Parse(raw),
				RuleType.Options => raw,
				RuleType.Color => ColorUtility.TryParseHtmlString(raw, out Color color) ? color : throw new InvalidCastException("Can't Cast To Color"),
				RuleType.String => raw,
				_ => throw new Exception("Impossible")
			}, false);
		}
		catch (Exception e) {
			Debug.LogError($"[RoxyLib] deserialize failed for '{Category}.{Name}' value '{raw}': {e}");
		}
	}

	private object? ConvertValue(object value) {
		if (value == null)
			return null;
		if (FieldType.IsEnum && value is string s)
			return Enum.Parse(FieldType, s);
		if (FieldType == typeof(UnityEngine.Color) && value is string color_str)
			return UnityEngine.ColorUtility.TryParseHtmlString(color_str, out UnityEngine.Color c) ? c : null;
		return FieldType.IsAssignableFrom(value.GetType()) ? value : Convert.ChangeType(value, FieldType);
	}

	private static RuleType DetectType(Type type) {
		if (type == typeof(bool))
			return RuleType.Switch;
		if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
			|| type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
			return RuleType.SliderInt;
		if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
			return RuleType.SliderFloat;
		if (type.IsEnum)
			return RuleType.Options;
		if (type == typeof(UnityEngine.Color))
			return RuleType.Color;
		return RuleType.String;
	}
}

/// <summary>规则全局管理器</summary>
public static class RuleManager {
	private static readonly Dictionary<string, List<RuleInfo>> Rules = [];

	/// <summary>扫描程序集，注册所有 [RoxyMod] 规则类</summary>
	internal static void ScanAssembly(Assembly assembly) {
		foreach (Type type in assembly.GetTypes()) {
			var mod_attribute = type.GetCustomAttribute<RoxyModAttribute>();
			if (mod_attribute is null)
				continue;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public)) {
				var rule_attribute = field.GetCustomAttribute<RoxyRuleAttribute>();
				if (rule_attribute is null)
					continue;
				var info = new RuleInfo(mod_attribute.ModId, field, rule_attribute, field.GetValue(null));
				if ((info.RuleType == RuleType.SliderInt || info.RuleType == RuleType.SliderFloat)
					&& (rule_attribute.Min is null || rule_attribute.Max is null))
					throw new InvalidOperationException($"RoxyRule '{mod_attribute.ModId}.{field.Name}' (Slider) requires Min and Max");
				if (info.RuleType == RuleType.Switch)
					info.Keybind = new Keybind(KeyCombination.None);
				if (!Rules.TryGetValue(mod_attribute.ModId, out var list)) {
					list = [];
					Rules[mod_attribute.ModId] = list;
				}
				list.Add(info);
			}
		}
	}

	public static void RemoveMod(string mod_id) {
		Rules.Remove(mod_id);
	}

	public static IReadOnlyList<RuleInfo> GetRules(string? mod_id) {
		return (mod_id is null)
			? Rules.Values.SelectMany(list => list).ToList().AsReadOnly()
			: Rules.TryGetValue(mod_id, out var list) ? list.AsReadOnly() : Array.Empty<RuleInfo>();
	}
}
