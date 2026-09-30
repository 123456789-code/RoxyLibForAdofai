using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

public static class Storage {
	/// <summary>
	/// 读取配置并回填规则与快捷键
	/// 文件不存在则用默认值落盘
	/// </summary>
	public static void LoadAll(IReadOnlyList<RuleInfo> rules, string path) {
		string config_path = Path.Combine(path, "config.json");
		EnsureDirectory(config_path);
		if (!File.Exists(config_path)) {
			SaveAll(rules, config_path);
			return;
		}
		try {
			var root = JObject.Parse(File.ReadAllText(config_path));
			if (root["Rules"] is JObject rules_read && root["Keybinds"] is JObject keybinds_read)
				foreach (var rule in rules) {
					string name = $"{rule.Category}.{rule.Name}";
					if (rules_read[name] is JToken rule_token) {
						string raw = rule_token.Value<string>()
							?? throw new ArgumentNullException(nameof(rule_token), "数据不能为空");
						if (!rule.TryDeserialize(raw))
							UnityEngine.Debug.LogWarning($"[RoxyLib] deserialize failed for '{name}' value '{raw}'");
					}
					if (rule is SwitchRule
						&& keybinds_read[name] is JToken key_token)
						rule.Keybind = new Keybind(
							KeyCombination.Parse(key_token.Value<string>()
								?? throw new ArgumentNullException(nameof(key_token), "数据不能为空"))
						);
				}
		}
		catch (Exception e) {
			UnityEngine.Debug.LogError($"[RoxyLib] config load failed: {config_path}: {e}");
		}
	}

	/// <summary>
	/// 保存规则与快捷键到 config.json
	/// </summary>
	public static void SaveAll(IReadOnlyList<RuleInfo> rules, string path) {
		if (rules.Count == 0)
			return;
		string config_path = Path.Combine(path, "config.json");
		EnsureDirectory(config_path);

		// 填充内容
		var root = new JObject { { "ModId", rules[0].ModId } };
		var rules_save = new JObject();
		var keybinds_save = new JObject();
		foreach (var rule in rules) {
			string name = $"{rule.Category}.{rule.Name}";
			rules_save[name] = rule.Serialize();
			if (rule is SwitchRule
				&& rule.Keybind is Keybind k)
				keybinds_save[name] = k.Combination.ToString();
		}
		root["Rules"] = rules_save;
		root["Keybinds"] = keybinds_save;

		// 原子写
		string tmp_path = config_path + ".tmp";
		File.WriteAllText(tmp_path, root.ToString(Formatting.Indented));
		if (File.Exists(config_path))
			File.Delete(config_path);
		File.Move(tmp_path, config_path);
	}

	private static void EnsureDirectory(string config_path) {
		string? directory = Path.GetDirectoryName(config_path);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
			Directory.CreateDirectory(directory);
		}
	}
}
