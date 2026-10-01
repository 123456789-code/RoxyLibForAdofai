using System;
using System.Collections.Generic;

namespace RoxyLib.Setting;

/// <summary>Small data-only descriptions of editable components; setters return a complete new value.</summary>
public enum EditorKind { Text, Number, Toggle, Options }

public sealed class EditorField {
	public string Label { get; }
	public EditorKind Kind { get; }
	public Type ComponentType { get; }
	public object? Min { get; }
	public object? Max { get; }
	public IReadOnlyList<string>? Options { get; }
	internal Func<object, object> Read { get; }
	internal Func<object, object, object> Write { get; }

	private EditorField(string label, EditorKind kind, Type component_type, Func<object, object> read,
		Func<object, object, object> write, object? min = null, object? max = null, string[]? options = null) {
		Label = label;
		Kind = kind;
		ComponentType = component_type;
		Read = read;
		Write = write;
		Min = min;
		Max = max;
		Options = options == null ? null : Array.AsReadOnly((string[])options.Clone());
	}
	public static EditorField Text<T>(string label, Func<T, string> read, Func<T, string, T> write) {
		return new(label, EditorKind.Text, typeof(string), x => read((T)x), (x, v) => write((T)x, (string)v)!);
	}
	public static EditorField Toggle<T>(string label, Func<T, bool> read, Func<T, bool, T> write) {
		return new(label, EditorKind.Toggle, typeof(bool), x => read((T)x), (x, v) => write((T)x, (bool)v)!);
	}
	public static EditorField Number<T, TNumber>(string label, Func<T, TNumber> read, Func<T, TNumber, T> write,
		object min, object max) where TNumber : struct, IConvertible {
		_ = new NumericRange(typeof(TNumber), min, max);
		return new(label, EditorKind.Number, typeof(TNumber), x => read((T)x), (x, v) => write((T)x, (TNumber)v)!, min, max);
	}
	public static EditorField OptionsList<T>(string label, string[] options, Func<T, string> read, Func<T, string, T> write) {
		if (options.Length == 0)
			throw new ArgumentException("At least one option is required.");
		return new(label, EditorKind.Options, typeof(string), x => read((T)x), (x, v) => write((T)x, (string)v)!, options: options);
	}
}
public sealed class RuleEditorSchema {
	public IReadOnlyList<EditorField> Fields { get; }
	public RuleEditorSchema(params EditorField[] fields) {
		if (fields.Length == 0)
			throw new ArgumentException("At least one editor field is required.");
		Fields = Array.AsReadOnly((EditorField[])fields.Clone());
	}
}
