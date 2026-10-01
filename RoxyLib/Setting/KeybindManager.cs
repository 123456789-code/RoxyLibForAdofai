using System;
using System.Collections.Generic;
using System.Linq;
using RoxyLib.Utils;
using UnityEngine;

namespace RoxyLib.Setting;

public static class KeybindManager {
	private static readonly Dictionary<RuleInfo, bool> Pressed = [];
	private static readonly KeyCode[] Keys = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Distinct().ToArray();
	private static int CaptureFrame;
	public static RuleInfo? Capturing { get; private set; }

	public static void BeginCapture(RuleInfo rule) {
		if (rule.Keybind == null)
			throw new ArgumentException("This rule has no keybind.");
		Capturing = rule;
		CaptureFrame = Time.frameCount;
	}
	public static void CancelCapture() { Capturing = null; }
	internal static KeyModifiers ReadModifiers() {
		KeyModifiers result = KeyModifiers.None;
		if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
			result |= KeyModifiers.Control;
		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			result |= KeyModifiers.Shift;
		if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
			result |= KeyModifiers.Alt;
		return result;
	}
	internal static void Update(bool settings_open) {
		RuleInfo[] rules = RuleManager.GetRules().Where(x => x.Keybind != null).ToArray();
		foreach (RuleInfo stale in Pressed.Keys.Where(x => !rules.Contains(x)).ToArray())
			Pressed.Remove(stale);
		bool capturing = Capturing != null;
		if (capturing && !rules.Contains(Capturing!))
			CancelCapture();
		if (Capturing != null && Time.frameCount > CaptureFrame) {
			if (Input.GetKeyDown(KeyCode.Escape))
				CancelCapture();
			else {
				foreach (KeyCode key in Keys) {
					if (key == KeyCode.None || KeyCombination.IsModifier(key) || key == KeyCode.Escape || key >= KeyCode.Mouse0)
						continue;
					if (!Input.GetKeyDown(key))
						continue;
					Capturing.Keybind!.Combination = new KeyCombination(key, ReadModifiers());
					CancelCapture();
					break;
				}
			}
		}
		// Snapshot the open state: a toggle in this batch cannot suppress other bindings to the same chord.
		Poll(rules, Input.GetKey, ReadModifiers(), capturing || settings_open);
		if (settings_open && !capturing && Input.GetKeyDown(KeyCode.Escape))
			RoxyLib.SetSettingsOpen(false);
	}
	internal static void Poll(IReadOnlyList<RuleInfo> rules, Func<KeyCode, bool> is_down, KeyModifiers modifiers, bool suppressed) {
		foreach (RuleInfo rule in rules) {
			KeyCombination combo = rule.Keybind!.Combination;
			bool down = !combo.IsEmpty && is_down(combo.Key) && modifiers == combo.Modifiers;
			bool previous = Pressed.TryGetValue(rule, out bool was_down) && was_down;
			Pressed[rule] = down;
			if (down && !previous && !suppressed)
				rule.TrySetValue(!rule.GetValue<bool>(), out _);
		}
	}
}
