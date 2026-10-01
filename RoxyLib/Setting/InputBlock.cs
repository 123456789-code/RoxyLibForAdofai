using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoxyLib.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoxyLib.Setting;

/// <summary>Gate the game's managed input API and lower UGUI processing, without patching native Unity Input.</summary>
internal static class InputBlock {
	private static bool Installed;
	private static int ReleaseFrame = -1;
	internal static bool Blocking => RoxyLib.IsSettingsOpen || Time.frameCount <= ReleaseFrame;
	internal static void HoldClosingFrame() { ReleaseFrame = Time.frameCount + 1; }
	internal static void Install() {
		if (Installed)
			return;
		Harmony harmony = new("RoxyLib.SettingsInput");
		// Native ImGui input uses Unity Input directly, so the game's EventSystems can be suspended.
		MethodInfo? update = AccessTools.Method(typeof(EventSystem), "Update");
		if (update != null)
			harmony.Patch(update, prefix: new HarmonyMethod(typeof(InputBlock), nameof(Allow)));
		MethodInfo? manager_gui = AccessTools.Method(AccessTools.TypeByName("UnityModManagerNet.UnityModManager+UI"), "OnGUI");
		if (manager_gui != null)
			harmony.Patch(manager_gui, prefix: new HarmonyMethod(typeof(InputBlock), nameof(Allow)));
		foreach (string name in new[] { "RDInput", "RDInputType_Keyboard", "RDInputType_AsyncKeyboard", "RDInputType_Mouse" }) {
			Type? type = AccessTools.TypeByName(name);
			if (type == null) { RoxyLog.Warning("Input blocker target unavailable: " + name); continue; }
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
				bool target = name == "RDInput"
					? method.Name != "get_useKeyLimiter"
					: method.Name is "CheckKeyState" or "CheckAnyKeyState" or "Main" or "MainIgnoreActive";
				if (!target || method.IsAbstract || method.ContainsGenericParameters)
					continue;
				string? prefix = method.ReturnType == typeof(bool) ? nameof(Bool)
					: method.ReturnType == typeof(int) ? nameof(Int)
					: method.Name == "get_mouseScrollDelta" && method.ReturnType == typeof(Vector2) ? nameof(Vector) : null;
				if (prefix != null)
					harmony.Patch(method, prefix: new HarmonyMethod(typeof(InputBlock), prefix));
			}
		}
		Installed = true;
	}
	private static bool Allow() { return !Blocking; }
#pragma warning disable IDE1006 // Harmony requires the exact injected parameter name __result.
	private static bool Bool(ref bool __result) {
		if (!Blocking)
			return true;
		__result = false;
		return false;
	}
	private static bool Int(ref int __result) {
		if (!Blocking)
			return true;
		__result = 0;
		return false;
	}
	private static bool Vector(ref Vector2 __result) {
		if (!Blocking)
			return true;
		__result = Vector2.zero;
		return false;
	}
#pragma warning restore IDE1006
}
