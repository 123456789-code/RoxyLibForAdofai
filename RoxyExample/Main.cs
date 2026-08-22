using UnityModManagerNet;

namespace RoxyExample;

public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		RoxyLib.RoxyLib.Register(mod_entry);
	}
}
