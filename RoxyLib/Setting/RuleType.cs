using System;
using System.Collections.Generic;

namespace RoxyLib.Setting;

public delegate bool RuleParser<T>(string text, out T value);

/// <summary>Serialization and validation, independent of the GUI and mod lifecycle.</summary>
public abstract class RuleType {
	public string Id { get; }
	public Type ValueType { get; }
	public bool IsNumeric { get; }
	protected RuleType(string id, Type value_type, bool numeric = false) {
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("A stable type ID is required.", nameof(id));
		Id = id;
		ValueType = value_type;
		IsNumeric = numeric;
	}
	public abstract string Serialize(object value);
	public abstract bool TryParse(string text, out object? value, out string? error);
	public abstract bool Validate(object value, out string? error);
}

public sealed class RuleType<T> : RuleType {
	private readonly Func<T, string> Serializer;
	private readonly RuleParser<T> Parser;
	private readonly Func<T, string?>? Validator;
	public RuleType(string id, Func<T, string> serialize, RuleParser<T> parse,
		Func<T, string?>? validate = null, bool numeric = false) : base(id, typeof(T), numeric) {
		Serializer = serialize ?? throw new ArgumentNullException(nameof(serialize));
		Parser = parse ?? throw new ArgumentNullException(nameof(parse));
		Validator = validate;
	}
	public override string Serialize(object value) {
		return Serializer((T)value);
	}

	public override bool TryParse(string text, out object? value, out string? error) {
		value = null;
		try {
			if (!Parser(text, out T result)) { error = "Invalid " + Id + " value."; return false; }
			if (!Validate(result!, out error))
				return false;
			value = result;
			return true;
		}
		catch (Exception ex) { error = ex.Message; return false; }
	}
	public override bool Validate(object value, out string? error) {
		error = null;
		if (!(value is T typed)) { error = "Expected " + typeof(T).Name + "."; return false; }
		try { error = Validator?.Invoke(typed); }
		catch (Exception ex) { error = ex.Message; }
		return error == null;
	}
}

/// <summary>Exact type inference; explicit IDs allow alternative representations of the same CLR type.</summary>
public static class RuleTypeRegistry {
	private static readonly Dictionary<string, RuleType> Types = new(StringComparer.Ordinal);
	private static readonly Dictionary<Type, RuleType> Inferred = new();
	static RuleTypeRegistry() { BuiltinRuleTypes.Register(); }

	public static IDisposable Register(RuleType type, bool infer_for_fields = true) {
		if (Types.ContainsKey(type.Id))
			throw new InvalidOperationException("Duplicate rule type: " + type.Id);
		if (infer_for_fields && Inferred.ContainsKey(type.ValueType))
			throw new InvalidOperationException("An inferred type already exists for " + type.ValueType + "; use an explicit TypeId.");
		Types.Add(type.Id, type);
		if (infer_for_fields)
			Inferred.Add(type.ValueType, type);
		return new Registration(() => {
			if (Types.TryGetValue(type.Id, out var current) && ReferenceEquals(current, type))
				Types.Remove(type.Id);
			if (Inferred.TryGetValue(type.ValueType, out current) && ReferenceEquals(current, type))
				Inferred.Remove(type.ValueType);
		});
	}
	public static RuleType Resolve(Type value_type, string? id = null) {
		if (!string.IsNullOrEmpty(id)) {
			if (!Types.TryGetValue(id!, out var explicit_type) || explicit_type.ValueType != value_type)
				throw new InvalidOperationException("Unknown or mismatched rule type: " + id);
			return explicit_type;
		}
		if (Inferred.TryGetValue(value_type, out var type))
			return type;
		if (value_type.IsEnum)
			return BuiltinRuleTypes.RegisterEnum(value_type);
		throw new InvalidOperationException("No rule type registered for " + value_type.FullName);
	}
}

internal sealed class Registration : IDisposable {
	private Action? OnDispose;
	internal Registration(Action dispose) { OnDispose = dispose; }
	public void Dispose() { Action? action = OnDispose; OnDispose = null; action?.Invoke(); }
}
