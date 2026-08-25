using System;
using System.Linq;
using UnityEngine;

namespace RoxyLib.Utils;

/// <summary>
/// 表示一个按键组合，由多个 <see cref="KeyCode"/> 组成（如 Ctrl+Shift+A）
/// </summary>
public readonly struct KeyCombination {
	public KeyCode[] Keys { get; }

	public KeyCombination(params KeyCode[] keys) {
		Keys = keys;
	}

	public static KeyCombination None => new();

	public override string ToString() {
		return (Keys is null || Keys.Length == 0)
			? "None"
			: string.Join("+", Keys.Select(key => key.ToString()));
	}

	public static KeyCombination Parse(string text) {
		if (string.IsNullOrEmpty(text) || text == "None")
			return None;
		string[] parts = text.Split('+');
		KeyCode[] keys = [.. parts.Select(p => (KeyCode)Enum.Parse(typeof(KeyCode), p.Trim()))];
		return new KeyCombination(keys);
	}
}

/// <summary>
/// 单个按键绑定，包含所属 Mod、名称、当前组合，并提供状态查询和事件
/// </summary>
public sealed class Keybind {
	public KeyCombination Combination { get; set {
		field = value;
		WasDown = IsDown();
	} }
	private bool WasDown { get; set; }
	public event Action? Activated;

	public Keybind(KeyCombination combination) {
		Combination = combination;
	}

	/// <summary>
	/// 内部轮询
	/// </summary>
	internal void Poll() {
		bool is_down = IsDown();
		bool just_pressed = is_down && !WasDown;
		WasDown = is_down;
		if (just_pressed) {
			Activated?.Invoke();
		}
	}

	public bool IsDown() {
		KeyCode[] keys = Combination.Keys;
		return !(keys is null || keys.Length == 0) && keys.All(UnityEngine.Input.GetKey);
	}
}
