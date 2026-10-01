using System;
using System.Globalization;

namespace RoxyLib.Setting;

/// <summary>Exact integral/decimal validation; floating-point ranges retain their full finite domain.</summary>
internal sealed class NumericRange {
	private readonly Type Type;
	private readonly bool Floating;
	private readonly object Minimum;
	private readonly object Maximum;
	internal NumericRange(Type type, object minimum, object maximum) {
		Type = type;
		Floating = type == typeof(float) || type == typeof(double);
		if (!Floating && type != typeof(decimal) && type != typeof(byte) && type != typeof(sbyte)
			&& type != typeof(short) && type != typeof(ushort) && type != typeof(int) && type != typeof(uint)
			&& type != typeof(long) && type != typeof(ulong))
			throw new ArgumentException("Numeric editors require a primitive numeric type or decimal.");
		Minimum = Convert.ChangeType(minimum, type, CultureInfo.InvariantCulture);
		Maximum = Convert.ChangeType(maximum, type, CultureInfo.InvariantCulture);
		if (Floating) {
			double lo = Convert.ToDouble(Minimum), hi = Convert.ToDouble(Maximum);
			if (!Finite(lo) || !Finite(hi) || lo >= hi)
				throw new ArgumentException("Bounds must be finite, representable and increasing.");
		}
		else if (Convert.ToDecimal(Minimum) != Convert.ToDecimal(minimum)
			|| Convert.ToDecimal(Maximum) != Convert.ToDecimal(maximum)
			|| Convert.ToDecimal(Minimum) >= Convert.ToDecimal(Maximum))
			throw new ArgumentException("Bounds must be representable and increasing.");
	}
	private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
	internal bool Contains(object value) {
		if (Floating) {
			double number = Convert.ToDouble(value);
			return Finite(number) && number >= Convert.ToDouble(Minimum) && number <= Convert.ToDouble(Maximum);
		}
		decimal exact = Convert.ToDecimal(value);
		return exact >= Convert.ToDecimal(Minimum) && exact <= Convert.ToDecimal(Maximum);
	}
	internal float Fraction(object value) {
		// Scaling before subtracting avoids overflow even for [-MaxValue, MaxValue].
		double lo = Convert.ToDouble(Minimum), hi = Convert.ToDouble(Maximum);
		double scale = Math.Max(Math.Abs(lo), Math.Abs(hi));
		if (hi == lo)
			return 0; // Adjacent large integers may coincide in a visual slider; text remains exact.
		return (float)Math.Max(0, Math.Min(1, (Convert.ToDouble(value) / scale - lo / scale) / (hi / scale - lo / scale)));
	}
	internal object Interpolate(float fraction) {
		if (fraction <= 0)
			return Minimum;
		if (fraction >= 1)
			return Maximum;
		if (Floating) {
			double lo = Convert.ToDouble(Minimum), hi = Convert.ToDouble(Maximum);
			double value = lo * (1 - (double)fraction) + hi * fraction;
			return Convert.ChangeType(Math.Max(lo, Math.Min(hi, value)), Type, CultureInfo.InvariantCulture);
		}
		decimal left = Convert.ToDecimal(Minimum), right = Convert.ToDecimal(Maximum);
		decimal weight = (decimal)fraction;
		decimal next = left * (1 - weight) + right * weight;
		if (Type != typeof(decimal))
			next = decimal.Round(next);
		return Convert.ChangeType(Math.Max(left, Math.Min(right, next)), Type, CultureInfo.InvariantCulture);
	}
}
