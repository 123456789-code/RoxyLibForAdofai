using System;
using System.Globalization;
using System.Numerics;
using ImGuiNET;
using RoxyLib.Gui;
using RoxyLib.Setting;

namespace RoxyExample;

public readonly struct RangeValue {
	public double Minimum { get; }
	public double Maximum { get; }
	public RangeValue(double minimum, double maximum) { Minimum = minimum; Maximum = maximum; }
}
public readonly struct Percentage {
	public double Value { get; }
	public Percentage(double value) { Value = value; }
}
public static class CustomRuleTypes {
	private static bool Registered;
	public static void Register() {
		if (Registered)
			return;
		RuleTypeRegistry.Register(new RuleType<RangeValue>("example/range",
			x => x.Minimum.ToString("R", CultureInfo.InvariantCulture) + ";" + x.Maximum.ToString("R", CultureInfo.InvariantCulture),
			ParseRange, x => x.Minimum >= 0 && x.Maximum <= 100 && x.Minimum <= x.Maximum ? null : "Require 0 <= min <= max <= 100."));
		RuleEditorRegistry.RegisterSchema("example/range", new RuleEditorSchema(
			EditorField.Number<RangeValue, double>("Minimum", x => x.Minimum, (x, v) => new RangeValue(v, x.Maximum), 0, 100),
			EditorField.Number<RangeValue, double>("Maximum", x => x.Maximum, (x, v) => new RangeValue(x.Minimum, v), 0, 100)));

		RuleTypeRegistry.Register(new RuleType<Percentage>("example/percentage",
			x => x.Value.ToString("R", CultureInfo.InvariantCulture), ParsePercentage,
			x => x.Value >= 0 && x.Value <= 1 ? null : "Percentage must be between 0 and 1."));
		RuleEditorRegistry.RegisterCustom("example/percentage", context => {
			float value = (float)context.GetValue<Percentage>().Value;
			ImGui.SetNextItemWidth(-1);
			if (ImGui.SliderFloat("##percentage", ref value, 0, 1, "%.2f"))
				context.SetValue(new Percentage(value));
			ImGui.ProgressBar(value, new Vector2(-1, 14), (value * 100).ToString("0") + "%");
		});
		Registered = true;
	}
	private static bool ParseRange(string text, out RangeValue value) {
		value = default;
		string[] parts = text.Split(';');
		if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double min)
			|| !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
			return false;
		value = new(min, max);
		return true;
	}
	private static bool ParsePercentage(string text, out Percentage value) {
		bool success = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
		value = new(number);
		return success;
	}
}
