using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoxyLib.Rules;

public enum RoxyRuleType {
	Switch,      // bool，绑定快捷键
	SliderInt,   // 整型，需 Min/Max，滑块+输入框
	SliderFloat, // 浮点，需 Min/Max，滑块+输入框
	Options,     // 枚举，下拉框
	Color,       // UnityEngine.Color，RGBA 滑块+输入框
	String,      // string，输入框
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class RoxyModAttribute : Attribute {
	public string ModId { get; set; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class RoxyRuleAttribute : Attribute {
	public string Category { get; set; } = "General";
	public object? Min { get; set; }  // Slider 必填
	public object? Max { get; set; }  // Slider 必填
}

/// <summary>一条规则：包装静态字段，提供读值、赋值（可选触发事件）、元数据。</summary>
public sealed class RuleInfo {
	public string ModId { get; }
	public string Name { get; }
	public string Category { get; }
	public RoxyRuleType RuleType { get; }
	public Type FieldType { get; }
	public object DefaultValue { get; }
	public object? Min { get; }
	public object? Max { get; }
	public string[]? EnumNames { get; }
	public Input.RoxyKeybind? Keybind { get; internal set; }

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
		if (RuleType == RoxyRuleType.Options) {
			EnumNames = Enum.GetNames(field.FieldType);
		}
		else if (RuleType == RoxyRuleType.SliderInt || RuleType == RoxyRuleType.SliderFloat) {
			Min = attribute.Min;
			Max = attribute.Max;
		}
	}

	/// <summary>读取当前值</summary>
	public object GetValue() => Field.GetValue(null);

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

	private object? ConvertValue(object value) {
		if (value == null) {
			return null;
		}
		if (FieldType.IsEnum && value is string s) {
			return Enum.Parse(FieldType, s);
		}
		if (FieldType == typeof(UnityEngine.Color) && value is string color_str) {
			return UnityEngine.ColorUtility.TryParseHtmlString(color_str, out UnityEngine.Color c) ? c : null;
		}
		return FieldType.IsAssignableFrom(value.GetType()) ? value : Convert.ChangeType(value, FieldType);
	}

	private static RoxyRuleType DetectType(Type type) {
		if (type == typeof(bool)) return RoxyRuleType.Switch;
		if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
			|| type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
			return RoxyRuleType.SliderInt;
		if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return RoxyRuleType.SliderFloat;
		if (type.IsEnum) return RoxyRuleType.Options;
		if (type == typeof(UnityEngine.Color)) return RoxyRuleType.Color;
		return RoxyRuleType.String;
	}
}

/// <summary>规则全局管理器</summary>
public static class RoxyRules {
	private static readonly Dictionary<string, List<RuleInfo>> Rules = new Dictionary<string, List<RuleInfo>>();

	/// <summary>扫描程序集，注册所有 [RoxyMod] 规则类</summary>
	internal static void ScanAssembly(Assembly assembly) {
		foreach (Type type in assembly.GetTypes()) {
			var mod_attribute = type.GetCustomAttribute<RoxyModAttribute>();
			if (mod_attribute == null)
				continue;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public)) {
				var rule_attribute = field.GetCustomAttribute<RoxyRuleAttribute>();
				if (rule_attribute == null)
					continue;
				var info = new RuleInfo(mod_attribute.ModId, field, rule_attribute, field.GetValue(null));
				if ((info.RuleType == RoxyRuleType.SliderInt || info.RuleType == RoxyRuleType.SliderFloat)
					&& (rule_attribute.Min == null || rule_attribute.Max == null))
					throw new InvalidOperationException($"RoxyRule '{mod_attribute.ModId}.{field.Name}' (Slider) requires Min and Max");
				if (info.RuleType == RoxyRuleType.Switch)
					info.Keybind = new Input.RoxyKeybind(Input.KeyCombination.None);
				if (!Rules.TryGetValue(mod_attribute.ModId, out var list)) {
					list = new List<RuleInfo>();
					Rules[mod_attribute.ModId] = list;
				}
				list.Add(info);
			}
		}
	}

	public static void RemoveMod(string mod_id) => Rules.Remove(mod_id);

	public static IReadOnlyList<RuleInfo> GetRules(string? mod_id) {
		if (mod_id == null) {
			return Rules.Values.SelectMany(list => list).ToList().AsReadOnly();
		}
		return Rules.TryGetValue(mod_id, out var list) ? list.AsReadOnly() : Array.Empty<RuleInfo>();
	}
}
