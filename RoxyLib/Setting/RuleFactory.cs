using System;
using System.Reflection;
using UnityEngine;
using RoxyLib.Attribute;

namespace RoxyLib.Setting;

/// <summary>
/// 规则工厂：把「运行时类型」映射到具体 RuleInfo 子类。
///
/// 内建类型（bool / 数值 / 枚举 / Color / string）在此登记；
/// 自定义类型不走此工厂——直接 new 子类后 <see cref="RuleManager.Register"/>（编程式通道）。
/// </summary>
public static class RuleFactory {
	/// <summary>扫描用：由 [RoxyRule] 静态字段构造对应子类。</summary>
	public static RuleInfo Create(string mod_id, FieldInfo field, RoxyRuleAttribute attribute) {
		Type type = field.FieldType;
		if (type == typeof(bool))
			return new SwitchRule(mod_id, field, attribute);
		if (IsNumeric(type))
			return (RuleInfo)Activator.CreateInstance(typeof(NumericRule<>).MakeGenericType(type), mod_id, field, attribute)!;
		if (type.IsEnum)
			return (RuleInfo)Activator.CreateInstance(typeof(OptionsRule<>).MakeGenericType(type), mod_id, field, attribute)!;
		if (type == typeof(Color))
			return new ColorRule(mod_id, field, attribute);
		return new StringRule(mod_id, field, attribute);
	}

	/// <summary>编程式：按类型造子类（Overlay 等运行时注册用）。</summary>
	public static RuleInfo Create(string mod_id, string name, string category, Type field_type, object default_value, object? min, object? max) {
		if (field_type == typeof(bool))
			return new SwitchRule(mod_id, name, category, (bool)default_value);
		if (IsNumeric(field_type)) {
			// 统一默认值到字段类型（如 int → uint），否则泛型构造参数类型不匹配
			if (!field_type.IsInstanceOfType(default_value) && default_value is IConvertible)
				default_value = Convert.ChangeType(default_value, field_type);
			return (RuleInfo)Activator.CreateInstance(typeof(NumericRule<>).MakeGenericType(field_type), mod_id, name, category, default_value, min, max)!;
		}
		if (field_type.IsEnum)
			return (RuleInfo)Activator.CreateInstance(typeof(OptionsRule<>).MakeGenericType(field_type), mod_id, name, category, default_value)!;
		if (field_type == typeof(Color))
			return new ColorRule(mod_id, name, category, (Color)default_value);
		return new StringRule(mod_id, name, category, default_value as string ?? "");
	}

	private static bool IsNumeric(Type type) {
		return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
			|| type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte)
			|| type == typeof(float) || type == typeof(double) || type == typeof(decimal);
	}
}
