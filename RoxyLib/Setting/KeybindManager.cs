using System.Collections.Generic;
using HarmonyLib;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>
/// 按键绑定管理器
/// </summary>
public static class KeybindManager {
	static readonly List<Keybind> Keybinds = [];

	public static void UpdateKeybindings() {
		Keybinds.Clear();
		foreach (var rule in RuleManager.GetRules())
			if (rule.RuleType == RuleType.Switch && rule.Keybind is Keybind k)
				Keybinds.Add(k);
	}

	public static void Update(float dt) {
		foreach (var keybind in Keybinds)
			keybind.Poll();
	}
}

// 设置界面屏蔽输入
[HarmonyPatch(typeof(RDInputType_Keyboard), "CheckKeyState")]
internal static class InputBlockPatches {
	static bool Prefix(ref bool __result) {
		if (RoxyLibRules.OpenSettings) {
			__result = false; // 游戏以为这个键没按
			return false;     // 跳过原方法
		}
		return true;
	}
}
