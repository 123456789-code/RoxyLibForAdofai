using System;
using UnityModManagerNet;

namespace RoxyExample;

public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		RoxyLib.RoxyLib.Register(mod_entry);
		mod_entry.OnUpdate += (_, _) => ExampleOverlay.TimeOverlay.SetText(DateTime.Now.ToString("HH:mm:ss"));
	}
}
