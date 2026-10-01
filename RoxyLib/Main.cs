using RoxyLib.Setting;
using RoxyLib.Utils;
using UnityEngine;
using UnityModManagerNet;

namespace RoxyLib;

public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		InputBlock.Install();
		RoxyLib.Register(mod_entry);
		mod_entry.OnUpdate += (_, dt) => RoxyLib.Tick(dt);
		mod_entry.OnGUI += _ => {
			if (GUILayout.Button(LanguageManager.Translate("RoxyLib.UMM.OpenConfig", "Open settings"), GUILayout.Width(180)))
				RoxyLib.OpenSettings();
		};
	}
}
