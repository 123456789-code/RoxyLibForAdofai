using System;
using System.Reflection;
using UnityEngine;
using RoxyLib.Attribute;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>
/// 一条规则的抽象基类。
///
/// 承载两类职责：
/// 1. 元数据 + 值存储：支持「字段后备」（[RoxyRule] 静态字段，读写成静态字段）
///    与「编程式」（Overlay 等运行时注册，值存内部）两种模式；
/// 2. 序列化/反序列化契约：子类通过多态实现（替换旧的 RuleType 枚举 + 全局 switch）。
///
/// 拓展方式：内建类型由 <see cref="RuleFactory"/> 按字段类型映射到具体子类；
/// 自定义类型直接继承本类（或泛型子类），实现序列化契约后由 <see cref="RuleManager.Register"/> 注册。
/// 控件构建不属于本类职责——GUI 层通过「控件构建器注册表」按具体类型分发（见 SettingGUI）。
/// </summary>
public abstract class RuleInfo {
	public string ModId { get; }
	public string Name { get; }
	public string Category { get; }
	public Type FieldType { get; }
	public object DefaultValue { get; }
	public object? Min { get; }
	public object? Max { get; }
	public string[]? EnumNames { get; }

	/// <summary>Switch 规则专用：快捷键绑定（由 RuleManager.Register 初始化）。</summary>
	public Keybind? Keybind { get; internal set; }

	/// <summary>是否为数值滑块规则（GUI 据此要求必须提供 Min/Max）。</summary>
	public virtual bool IsNumericSlider => false;

	public event EventHandler<object>? ValueChanged;

	private readonly bool UsingField;
	private readonly FieldInfo? Field;
	private object? Value;

	/// <summary>字段后备模式（[RoxyRule] 静态字段）。</summary>
	protected RuleInfo(string mod_id, FieldInfo field, RoxyRuleAttribute attribute) {
		UsingField = true;
		ModId = mod_id;
		Name = field.Name;
		Category = attribute.Category;
		FieldType = field.FieldType;
		DefaultValue = field.GetValue(null) ?? string.Empty;
		Field = field;
		Min = attribute.Min;
		Max = attribute.Max;
		EnumNames = FieldType.IsEnum ? Enum.GetNames(FieldType) : null;
	}

	/// <summary>编程式模式（运行时注册，值存内部）。</summary>
	protected RuleInfo(string mod_id, string name, string category, Type field_type, object default_value, object? min, object? max) {
		UsingField = false;
		ModId = mod_id;
		Name = name;
		Category = category;
		FieldType = field_type;
		DefaultValue = default_value;
		Value = default_value;
		Min = min;
		Max = max;
		EnumNames = field_type.IsEnum ? Enum.GetNames(field_type) : null;
	}

	/// <summary>读取当前值（字段后备 → 静态字段；编程式 → 内部值）。</summary>
	public object GetValue() {
		return UsingField ? Field!.GetValue(null) : Value!;
	}

	/// <summary>赋值（可触发 ValueChanged）。值转换失败返回 false。</summary>
	public bool SetValue(object value, bool trigger) {
		object? converted = ConvertValue(value);
		if (converted == null)
			return false;
		if (UsingField)
			Field!.SetValue(null, converted);
		else
			Value = converted;
		if (trigger)
			RaiseChanged(converted);
		return true;
	}

	/// <summary>值转换钩子：子类可覆盖（如枚举按名、颜色按 #HEX）。</summary>
	protected virtual object? ConvertValue(object value) {
		if (value == null)
			return null;
		if (FieldType.IsEnum && value is string s)
			return Enum.Parse(FieldType, s);
		if (FieldType == typeof(Color) && value is string color_str)
			return ColorUtility.TryParseHtmlString(color_str, out Color c) ? c : null;
		return FieldType.IsAssignableFrom(value.GetType()) ? value : Convert.ChangeType(value, FieldType);
	}

	protected void RaiseChanged(object value) {
		ValueChanged?.Invoke(this, value);
	}

	// ---- 序列化契约（子类必须实现）----

	/// <summary>把当前值序列化为配置字符串（config.json 存储格式）。</summary>
	public abstract string Serialize();

	/// <summary>从配置字符串解析回填。解析失败返回 false（由调用方记录日志）。</summary>
	public abstract bool TryDeserialize(string raw);
}
