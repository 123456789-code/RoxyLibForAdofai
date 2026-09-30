using System;
using System.Reflection;
using UnityEngine;
using RoxyLib.Attribute;

namespace RoxyLib.Setting;

/// <summary>布尔开关规则：bool，可绑定快捷键。</summary>
public sealed class SwitchRule : RuleInfo {
	public SwitchRule(string mod_id, FieldInfo field, RoxyRuleAttribute attribute)
		: base(mod_id, field, attribute) {
	}

	public SwitchRule(string mod_id, string name, string category, bool default_value)
		: base(mod_id, name, category, typeof(bool), default_value, null, null) {
	}

	public override string Serialize() {
		return GetValue().ToString() ?? "False";
	}

	public override bool TryDeserialize(string raw) {
		if (bool.TryParse(raw, out bool parsed)) {
			SetValue(parsed, false);
			return true;
		}
		return false;
	}
}

/// <summary>
/// 数值规则：整型/浮点（int、uint、long、float、double…），滑块 + 精确输入框。
/// 通过 <c>where T : struct, IConvertible</c> 一次覆盖整族数值类型。
/// </summary>
public sealed class NumericRule<T> : RuleInfo where T : struct, IConvertible {
	public override bool IsNumericSlider => true;

	public NumericRule(string mod_id, FieldInfo field, RoxyRuleAttribute attribute)
		: base(mod_id, field, attribute) {
	}

	public NumericRule(string mod_id, string name, string category, T default_value, object? min, object? max)
		: base(mod_id, name, category, typeof(T), default_value, min, max) {
	}

	public override string Serialize() {
		return GetValue().ToString() ?? "";
	}

	public override bool TryDeserialize(string raw) {
		try {
			SetValue(Convert.ChangeType(raw, typeof(T)), false);
			return true;
		}
		catch {
			return false;
		}
	}
}

/// <summary>枚举选项规则：下拉框（EnumNames 来自 FieldType）。</summary>
public sealed class OptionsRule<TEnum> : RuleInfo where TEnum : struct, Enum {
	public OptionsRule(string mod_id, FieldInfo field, RoxyRuleAttribute attribute)
		: base(mod_id, field, attribute) {
	}

	public OptionsRule(string mod_id, string name, string category, TEnum default_value)
		: base(mod_id, name, category, typeof(TEnum), default_value, null, null) {
	}

	public override string Serialize() {
		return GetValue().ToString() ?? "";
	}

	public override bool TryDeserialize(string raw) {
		if (Enum.TryParse<TEnum>(raw, out TEnum parsed)) {
			SetValue(parsed, false);
			return true;
		}
		return false;
	}
}

/// <summary>颜色规则：UnityEngine.Color，RGBA 四通道滑块。</summary>
public sealed class ColorRule : RuleInfo {
	public ColorRule(string mod_id, FieldInfo field, RoxyRuleAttribute attribute)
		: base(mod_id, field, attribute) {
	}

	public ColorRule(string mod_id, string name, string category, Color default_value)
		: base(mod_id, name, category, typeof(Color), default_value, null, null) {
	}

	public override string Serialize() {
		var color = (Color)GetValue();
		return "#" + ColorUtility.ToHtmlStringRGBA(color);
	}

	public override bool TryDeserialize(string raw) {
		if (ColorUtility.TryParseHtmlString(raw, out Color parsed)) {
			SetValue(parsed, false);
			return true;
		}
		return false;
	}
}

/// <summary>字符串规则：输入框。</summary>
public sealed class StringRule : RuleInfo {
	public StringRule(string mod_id, FieldInfo field, RoxyRuleAttribute attribute)
		: base(mod_id, field, attribute) {
	}

	public StringRule(string mod_id, string name, string category, string default_value)
		: base(mod_id, name, category, typeof(string), default_value, null, null) {
	}

	public override string Serialize() {
		return (string)GetValue();
	}

	public override bool TryDeserialize(string raw) {
		SetValue(raw, false);
		return true;
	}
}
