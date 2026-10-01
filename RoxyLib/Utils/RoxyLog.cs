using System;
using UnityEngine;

namespace RoxyLib.Utils;

internal static class RoxyLog {
	internal static Action<string> Sink = message => Debug.LogWarning("[RoxyLib] " + message);
	internal static void Warning(string message) {
		Sink(message);
	}

	internal static void Error(string operation, Exception ex) {
		Sink(operation + ": " + ex);
	}
}
