using System;
using System.Reflection;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>A setting instance. GUI, configuration and code share the same validated mutation path.</summary>
public sealed class RuleInfo {
	private readonly FieldInfo? Field;
	private object Value;
	private readonly string DefaultText;
	public string ModId { get; }
	public string Name { get; }
	public string Category { get; }
	public string Key => Category + "." + Name;
	public string Id => ModId + "." + Key;
	public RuleType Type { get; }
	public Type ValueType => Type.ValueType;
	public object? Min { get; }
	public object? Max { get; }
	public bool Persistent { get; }
	public Keybind? Keybind { get; }
	public string? Error { get; private set; }
	public event Action<RuleInfo>? ValueChanged;
	internal event Action<RuleInfo>? Changed;

	public RuleInfo(string mod_id, string name, string category, RuleType type, object default_value,
		object? min = null, object? max = null, bool persistent = true)
		: this(mod_id, name, category, type, default_value, min, max, persistent, null) { }

	internal RuleInfo(string mod_id, string name, string category, RuleType type, object default_value,
		object? min, object? max, bool persistent, FieldInfo? field) {
		if (string.IsNullOrWhiteSpace(mod_id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
			throw new ArgumentException("Mod, category and rule names must not be empty.");
		if (category.Contains(".") || name.Contains("."))
			throw new ArgumentException("Category and rule names must not contain '.'.");
		ModId = mod_id;
		Name = name;
		Category = category;
		Type = type;
		Min = min;
		Max = max;
		Persistent = persistent;
		Field = field;
		Value = default_value;
		if (!Validate(default_value, out string? error))
			throw new ArgumentException(Id + ": " + error);
		DefaultText = Type.Serialize(default_value);
		if (!Type.TryParse(DefaultText, out object? restored, out error) || !Validate(restored!, out error))
			throw new ArgumentException(Id + ": default cannot round-trip: " + error);
		if (ValueType == typeof(bool)) {
			Keybind = new Keybind(KeyCombination.None);
			Keybind.Changed += () => Changed?.Invoke(this);
		}
	}

	public object GetValue() {
		return Field?.GetValue(null) ?? Value;
	}

	public T GetValue<T>() {
		return (T)GetValue();
	}

	public string Serialize() {
		return Type.Serialize(GetValue());
	}

	public object DefaultValue {
		get {
			if (!Type.TryParse(DefaultText, out object? result, out string? error))
				throw new InvalidOperationException(error);
			return result!;
		}
	}
	public bool TrySetValue(object candidate, out string? error) {
		return Apply(candidate, true, out error);
	}

	public bool TrySetText(string text, out string? error) {
		if (!Type.TryParse(text, out object? parsed, out error)) { Error = error; return false; }
		return Apply(parsed!, true, out error);
	}
	public bool Reset() {
		return TrySetValue(DefaultValue, out _);
	}

	internal bool Load(string text, out string? error) {
		if (!Type.TryParse(text, out object? parsed, out error))
			return false;
		return Apply(parsed!, false, out error);
	}

	private bool Apply(object candidate, bool notify, out string? error) {
		if (!Validate(candidate, out error)) { Error = error; return false; }
		Error = null;
		if (Equals(GetValue(), candidate))
			return true;
		if (Field != null)
			Field.SetValue(null, candidate);
		else
			Value = candidate;
		if (notify) {
			Changed?.Invoke(this);
			if (ValueChanged != null)
				foreach (Action<RuleInfo> handler in ValueChanged.GetInvocationList())
					try { handler(this); }
					catch (Exception ex) { RoxyLog.Error(Id + " callback", ex); }
		}
		return true;
	}
	private bool Validate(object candidate, out string? error) {
		if (!Type.Validate(candidate, out error))
			return false;
		if (!Type.IsNumeric)
			return true;
		try {
			if (Min == null || Max == null) { error = "Numeric rules require Min and Max."; return false; }
			NumericRange range = new(ValueType, Min, Max);
			if (!range.Contains(candidate)) { error = $"Value must be between {Min} and {Max}."; return false; }
			return true;
		}
		catch (Exception ex) { error = "Invalid numeric value or bounds: " + ex.Message; return false; }
	}
}
