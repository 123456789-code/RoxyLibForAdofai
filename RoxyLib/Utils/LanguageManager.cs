using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace RoxyLib.Utils;

public enum LanguageEnum { EnUs, ZhCn, JaJp, KoKr }

public static class LanguageManager {
	private static readonly Dictionary<string, Dictionary<LanguageEnum, Dictionary<string, string>>> Mods = new(StringComparer.Ordinal);
	internal static void Load(string mod_id, string path) {
		Dictionary<LanguageEnum, Dictionary<string, string>> tables = [];
		string directory = Path.Combine(path, "lang");
		if (Directory.Exists(directory))
			foreach (string file in Directory.GetFiles(directory, "*.json")) {
				try {
					if (!TryLanguage(Path.GetFileNameWithoutExtension(file), out LanguageEnum language))
						continue;
					Dictionary<string, string> table = new(StringComparer.Ordinal);
					foreach (KeyValuePair<string, JToken?> item in JObject.Parse(File.ReadAllText(file))) {
						if (item.Value?.Type != JTokenType.String)
							throw new InvalidDataException(item.Key + " must be a string.");
						// A mod may translate its own namespace only.
						if (item.Key.StartsWith(mod_id + ".", StringComparison.Ordinal))
							table.Add(item.Key, item.Value.Value<string>()!);
					}
					tables[language] = table;
				}
				catch (Exception ex) { RoxyLog.Error("Loading language file " + file, ex); }
			}
		Mods[mod_id] = tables;
	}
	internal static void Remove(string mod_id) { Mods.Remove(mod_id); }
	public static string Translate(string key, string fallback) {
		return Translate(key, fallback, RoxyLibRules.Language);
	}
	public static string Translate(string key, string fallback, LanguageEnum language) {
		foreach (var mod in Mods) {
			if (!key.StartsWith(mod.Key + ".", StringComparison.Ordinal))
				continue;
			if (mod.Value.TryGetValue(language, out Dictionary<string, string>? current) && current.TryGetValue(key, out string? value))
				return value;
			if (mod.Value.TryGetValue(LanguageEnum.EnUs, out Dictionary<string, string>? english) && english.TryGetValue(key, out value))
				return value;
		}
		return fallback;
	}
	private static bool TryLanguage(string code, out LanguageEnum language) {
		switch (code.ToLowerInvariant()) {
		case "en-us":
			language = LanguageEnum.EnUs;
			return true;
		case "zh-cn":
			language = LanguageEnum.ZhCn;
			return true;
		case "ja-jp":
			language = LanguageEnum.JaJp;
			return true;
		case "ko-kr":
			language = LanguageEnum.KoKr;
			return true;
		default:
			language = default;
			return false;
		}
	}
}
