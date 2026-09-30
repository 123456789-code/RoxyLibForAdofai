using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace RoxyLib.Utils;

/// <summary>
/// 支持的语言种类，包含中文、英文、日文、韩文
/// </summary>
public enum LanguageEnum {
	en_us,
	zh_cn,
	ja_jp,
	ko_kr
}

/// <summary>
/// 当前语言直接派生自 RoxyLibRules.Language 静态规则
/// 切换语言 = 对该规则调用 SetValue（触发 ValueChanged → RaiseLanguageChanged）
/// </summary>
public static class LanguageManager {
	private static readonly ConcurrentDictionary<LanguageEnum, Dictionary<string, string>> Dict = new() {
		[LanguageEnum.en_us] = new(),
		[LanguageEnum.zh_cn] = new(),
		[LanguageEnum.ja_jp] = new(),
		[LanguageEnum.ko_kr] = new()
	};

	/// <summary>
	/// 加载指定目录下的所有 JSON 语言文件
	/// </summary>
	public static void LoadLangDir(string path) {
		string directory = Path.Combine(path, "lang");
		if (!Directory.Exists(directory))
			return;
		foreach (string file in Directory.GetFiles(directory, "*.json")) {
			try {
				LanguageEnum language = CodeToLanguage(Path.GetFileNameWithoutExtension(file).ToLowerInvariant());
				var root = JObject.Parse(File.ReadAllText(file));
				var dict = Dict[language];
				lock (dict) {
					foreach (var pair in root) {
						var token = pair.Value;
						if (token is null || token.Type != JTokenType.String)
							throw new InvalidDataException($"Value for key '{pair.Key}' must be a string, but found type {token?.Type}");
						dict[pair.Key] = token.Value<string>() ?? string.Empty;
					}
				}
			}
			catch (Exception e) {
				UnityEngine.Debug.LogError($"[RoxyLib] lang load failed: {file}: {e}");
			}
		}
	}

	public static void AddContent(string key, string content, LanguageEnum lang) {
		var dict = Dict[lang];
		lock (dict)
			dict[key] = content;
	}

	public static void AddContent(string key, string? en, string? zh, string? ja, string? ko) {
		if (en is not null)
			AddContent(key, en, LanguageEnum.en_us);
		if (zh is not null)
			AddContent(key, zh, LanguageEnum.zh_cn);
		if (ja is not null)
			AddContent(key, ja, LanguageEnum.ja_jp);
		if (ko is not null)
			AddContent(key, ko, LanguageEnum.ko_kr);
	}

	public static string Translate(string key, string fallback, LanguageEnum language) {
		var table = Dict[language];
		lock (table) {
			if (table.TryGetValue(key, out string? value))
				return value;
		}
		return fallback;
	}

	/// <summary>
	/// 翻译指定键，若未找到则返回备用文本。
	/// 回退链：当前语言 → "en-us" → 备用文本。
	/// </summary>
	public static string Translate(string key, string fallback) {
		var lang = RoxyLibRules.Language;
		if (Translate(key, fallback, lang) is string back)
			return back;
		if (lang != LanguageEnum.en_us && Translate(key, fallback, LanguageEnum.en_us) is string en)
			return en;
		return fallback;
	}

	public static string LanguageToCode(LanguageEnum language) {
		return language switch {
			LanguageEnum.en_us => "en-us",
			LanguageEnum.zh_cn => "zh-cn",
			LanguageEnum.ja_jp => "ja-jp",
			LanguageEnum.ko_kr => "ko-kr",
			_ => throw new Exception("Impossible")
		};
	}

	public static LanguageEnum CodeToLanguage(string language) {
		return language switch {
			"en-us" => LanguageEnum.en_us,
			"zh-cn" => LanguageEnum.zh_cn,
			"ja-jp" => LanguageEnum.ja_jp,
			"ko-kr" => LanguageEnum.ko_kr,
			_ => throw new InvalidDataException(language)
		};
	}
}
