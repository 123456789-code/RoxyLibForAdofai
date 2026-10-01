using UnityModManagerNet;

namespace RoxyExample;

public static class Main {
	public static void Setup(UnityModManager.ModEntry mod_entry) {
		CustomRuleTypes.Register();
		RoxyLib.RoxyLib.Register(mod_entry);
	}
}
