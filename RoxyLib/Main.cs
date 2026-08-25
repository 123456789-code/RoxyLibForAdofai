using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using RoxyLib.Utils;

namespace RoxyLib;

// RoxyLib 作为独立 UMM mod 的入口
public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		RoxyLib.Register(mod_entry);
		mod_entry.OnUpdate = (entry, dt) => RoxyLib.Tick(dt);
		mod_entry.OnGUI = entry => DrawOpenSettingsButton();
		var harmony = new Harmony("RoxyLib.InputBlock");
		harmony.PatchAll(typeof(Setting.InputBlockPatches).Assembly);
	}

	private static void DrawOpenSettingsButton() {
		// UMM 的 OnGUI 在 IMGUI 上下文中调用（GUILayout 可用）
		GUILayout.BeginVertical();
		GUILayout.Space(8);
		if (GUILayout.Button(LanguageManager.Translate("RoxyLib.UMM.OpenConfig", "Open Config Screen"), GUILayout.Width(180)))
			RoxyLib.OpenSettings();
		GUILayout.EndVertical();
	}
}
