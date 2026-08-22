using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoxyLib.Input;
using RoxyLib.Rules;

namespace RoxyLib.Storage;

using UnityEngine;

/// <summary>
/// 结构：
/// {
///   "ModId": "RoxyExample",
///   "Rules": { "Gameplay.Speed": "1.25", ... },
///   "Keybinds": { "Input.OpenMenu": "Ctrl+F11", ... },
/// }
/// </summary>
public static class RoxyStorage {
	/// <summary>
	/// 读取配置并回填规则与快捷键
	/// 文件不存在则用默认值落盘
	/// </summary>
	public static void LoadAll(IReadOnlyList<RuleInfo> rules, string config_path) {
		EnsureDirectory(config_path);
		if (!File.Exists(config_path)) {
			SaveAll(rules, config_path);
			return;
		}
		try {
			JObject root = JObject.Parse(File.ReadAllText(config_path));
			if (root["Rules"] is JObject rules_read && root["Keybinds"] is JObject keybinds_read)
				foreach (var rule in rules) {
					string name = $"{rule.Category}.{rule.Name}";
					if (rules_read[name] is JToken rule_token)
						DeserializeValue(rule, rule_token.Value<string>()
							?? throw new ArgumentNullException(nameof(rule_token), "数据不能为空"));
					if (rule.RuleType == RoxyRuleType.Switch
						&& keybinds_read[name] is JToken key_token)
						rule.Keybind = new RoxyKeybind(
							KeyCombination.Parse(key_token.Value<string>()
							?? throw new ArgumentNullException(nameof(key_token), "数据不能为空"))
						);
				}
		}
		catch (Exception e) {
			Debug.LogError($"[RoxyLib] config load failed: {config_path}: {e}");
		}
	}

	/// <summary>
	/// 保存规则与快捷键到 config.json
	/// </summary>
	public static void SaveAll(IReadOnlyList<RuleInfo> rules, string config_path) {
		if (rules.Count == 0)
			return;
		EnsureDirectory(config_path);

		// 填充内容
		var root = new JObject();
		root["ModId"] = rules[0].ModId;
		var rules_save = new JObject();
		var keybinds_save = new JObject();
		foreach (var rule in rules) {
			string name = $"{rule.Category}.{rule.Name}";
			rules_save[name] = SerializeValue(rule);
			if (rule.RuleType == RoxyRuleType.Switch
				&& rule.Keybind is RoxyKeybind k)
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

	/// <summary>
	/// 规则当前值 → 字符串。
	/// </summary>
	private static string SerializeValue(RuleInfo rule) {
		object value = rule.GetValue();
		switch (rule.RuleType) {
		case RoxyRuleType.Switch:
		case RoxyRuleType.SliderInt:
		case RoxyRuleType.SliderFloat:
		case RoxyRuleType.Options:
			return value.ToString();
		case RoxyRuleType.Color:
			Color color = (Color)value;
			return "#" + ColorUtility.ToHtmlStringRGBA(color);
		case RoxyRuleType.String:
			return (string)value;
		default:
			throw new Exception("Impossible");
		}
	}

	/// <summary>
	/// 字符串 → 规则值（经 SetValueDirect 静默回填，不触发事件）。
	/// </summary>
	private static void DeserializeValue(RuleInfo rule, string raw) {
		try {
			switch (rule.RuleType) {
			case RoxyRuleType.Switch:
				rule.SetValue(bool.Parse(raw), false);
				break;
			case RoxyRuleType.SliderInt:
				rule.SetValue(int.Parse(raw), false);
				break;
			case RoxyRuleType.SliderFloat:
				rule.SetValue(float.Parse(raw), false);
				break;
			case RoxyRuleType.Options:
				// 枚举名 → 枚举（ConvertValue 内部处理 Enum.Parse）
				rule.SetValue(raw, false);
				break;
			case RoxyRuleType.Color:
				if (ColorUtility.TryParseHtmlString(raw, out Color color)) {
					rule.SetValue(color, false);
				}
				break;
			case RoxyRuleType.String:
				rule.SetValue(raw, false);
				break;
			}
		}
		catch (Exception e) {
			Debug.LogError($"[RoxyLib] deserialize failed for '{rule.Category}.{rule.Name}' value '{raw}': {e}");
		}
	}

	private static void EnsureDirectory(string config_path) {
		string? directory = Path.GetDirectoryName(config_path);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
			Directory.CreateDirectory(directory);
		}
	}
}
