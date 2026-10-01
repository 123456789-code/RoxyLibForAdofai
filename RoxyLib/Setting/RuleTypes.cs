using System;
using System.Globalization;
using UnityEngine;

namespace RoxyLib.Setting;

internal static class BuiltinRuleTypes {
	internal static void Register() {
		RuleTypeRegistry.Register(new RuleType<bool>("core/bool", x => x ? "true" : "false", bool.TryParse));
		RuleTypeRegistry.Register(new RuleType<string>("core/string", x => x, (string s, out string v) => { v = s; return true; }));
		foreach (var t in new[] { typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
			typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }) {
			var rule_type = (RuleType)typeof(BuiltinRuleTypes).GetMethod(nameof(Number), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
				.MakeGenericMethod(t).Invoke(null, null)!;
			RuleTypeRegistry.Register(rule_type);
		}
		RuleTypeRegistry.Register(new RuleType<Color>("core/color", ColorText, ParseColor,
			c => Finite01(c.r) && Finite01(c.g) && Finite01(c.b) && Finite01(c.a) ? null : "RGBA channels must be between 0 and 1."));
	}
	private static bool Finite01(float x) {
		return !float.IsNaN(x) && x >= 0 && x <= 1;
	}

	private static RuleType Number<T>() {
		return new RuleType<T>("core/" + typeof(T).Name.ToLowerInvariant(),
			x => x is double d ? d.ToString("R", CultureInfo.InvariantCulture) : x is float f ? f.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(x, CultureInfo.InvariantCulture)!,
			(string s, out T v) => {
				try { v = (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture); return true; }
				catch { v = default!; return false; }
			}, x => {
				if (x is float f && (float.IsNaN(f) || float.IsInfinity(f)))
					return "Value must be finite.";
				if (x is double d && (double.IsNaN(d) || double.IsInfinity(d)))
					return "Value must be finite.";
				return null;
			}, numeric: true);
	}

	internal static RuleType RegisterEnum(Type type) {
		var rule_type = (RuleType)typeof(BuiltinRuleTypes).GetMethod(nameof(EnumType), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
			.MakeGenericMethod(type).Invoke(null, null)!;
		RuleTypeRegistry.Register(rule_type);
		return rule_type;
	}
	private static RuleType EnumType<T>() where T : struct, Enum {
		return new RuleType<T>("core/enum/" + typeof(T).Assembly.GetName().Name + "/" + typeof(T).FullName,
		x => x.ToString(), (string s, out T v) => Enum.TryParse(s, out v) && Enum.IsDefined(typeof(T), v),
		v => Enum.IsDefined(typeof(T), v) ? null : "Choose a defined enum value.");
	}

	private static string ColorText(Color color) {
		return "#" + ((byte)Math.Round(color.r * 255)).ToString("X2")
			+ ((byte)Math.Round(color.g * 255)).ToString("X2")
			+ ((byte)Math.Round(color.b * 255)).ToString("X2")
			+ ((byte)Math.Round(color.a * 255)).ToString("X2");
	}
	private static bool ParseColor(string text, out Color color) {
		color = default;
		if (!text.StartsWith("#", StringComparison.Ordinal) || (text.Length != 7 && text.Length != 9))
			return false;
		string digits = text.Substring(1) + (text.Length == 7 ? "FF" : "");
		if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgba))
			return false;
		color = new Color((rgba >> 24) / 255f, ((rgba >> 16) & 255) / 255f, ((rgba >> 8) & 255) / 255f, (rgba & 255) / 255f);
		return true;
	}
}
