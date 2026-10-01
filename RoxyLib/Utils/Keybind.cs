using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoxyLib.Utils;

[Flags]
public enum KeyModifiers { None = 0, Control = 1, Shift = 2, Alt = 4 }

public readonly struct KeyCombination : IEquatable<KeyCombination> {
	public KeyCode Key { get; }
	public KeyModifiers Modifiers { get; }
	public bool IsEmpty => Key == KeyCode.None;
	public static KeyCombination None => default;
	public KeyCombination(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) {
		if (!Enum.IsDefined(typeof(KeyCode), key) || IsModifier(key) || (modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)) != 0)
			throw new ArgumentException("Invalid key combination.");
		Key = key;
		Modifiers = key == KeyCode.None ? KeyModifiers.None : modifiers;
	}
	public override string ToString() {
		if (IsEmpty)
			return "None";
		List<string> parts = [];
		if ((Modifiers & KeyModifiers.Control) != 0)
			parts.Add("Ctrl");
		if ((Modifiers & KeyModifiers.Shift) != 0)
			parts.Add("Shift");
		if ((Modifiers & KeyModifiers.Alt) != 0)
			parts.Add("Alt");
		parts.Add(Key.ToString());
		return string.Join("+", parts);
	}
	public static bool TryParse(string text, out KeyCombination combination) {
		combination = None;
		if (text == "None" || string.IsNullOrWhiteSpace(text))
			return true;
		KeyModifiers modifiers = KeyModifiers.None;
		string[] parts = text.Split('+');
		for (int i = 0; i < parts.Length - 1; i++) {
			switch (parts[i].Trim()) {
			case "Ctrl":
				modifiers |= KeyModifiers.Control;
				break;
			case "Shift":
				modifiers |= KeyModifiers.Shift;
				break;
			case "Alt":
				modifiers |= KeyModifiers.Alt;
				break;
			default:
				return false;
			}
		}
		if (!Enum.TryParse(parts[parts.Length - 1].Trim(), out KeyCode key) || key == KeyCode.None || IsModifier(key) || !Enum.IsDefined(typeof(KeyCode), key))
			return false;
		combination = new(key, modifiers);
		return true;
	}
	internal static bool IsModifier(KeyCode key) {
		return key is KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftAlt or KeyCode.RightAlt;
	}
	public bool Equals(KeyCombination other) { return Key == other.Key && Modifiers == other.Modifiers; }
	public override bool Equals(object? obj) { return obj is KeyCombination other && Equals(other); }
	public override int GetHashCode() { return ((int)Key * 397) ^ (int)Modifiers; }
}

public sealed class Keybind {
	private KeyCombination Current;
	public KeyCombination Combination {
		get => Current;
		set {
			if (Current.Equals(value))
				return;
			Current = value;
			Changed?.Invoke();
		}
	}
	internal event Action? Changed;
	public Keybind(KeyCombination combination) { Current = combination; }
}
