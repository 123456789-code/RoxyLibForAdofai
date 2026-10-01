using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>One session per mod. Unknown rules survive saves, including late registrations.</summary>
internal sealed class ConfigStore {
	private readonly string FilePath;
	private JObject Root = new();
	private bool Writable = true;
	internal string? LastError { get; private set; }
	internal ConfigStore(string path, string mod_id) {
		FilePath = Path.Combine(path, "config.json");
		if (File.Exists(FilePath)) {
			try {
				Root = JObject.Parse(File.ReadAllText(FilePath));
				if (Root.Value<int?>("Version") != 1)
					throw new InvalidDataException("Unsupported config version. Move the old config aside before enabling this mod.");
				if (Root.Value<string>("ModId") != mod_id)
					throw new InvalidDataException("Config ModId does not match.");
				if (Root["Rules"] is not JObject || Root["Keybinds"] is not JObject)
					throw new InvalidDataException("Rules and Keybinds must be objects.");
			}
			catch (Exception ex) {
				Writable = false;
				LastError = ex.Message;
				RoxyLog.Error("Loading " + FilePath + " (original preserved)", ex);
				Root = new();
			}
		}
		Root["Version"] = 1;
		Root["ModId"] = mod_id;
		Root["Rules"] ??= new JObject();
		Root["Keybinds"] ??= new JObject();
	}
	internal void Load(RuleInfo rule) {
		try {
			if (rule.Persistent && Root["Rules"]![rule.Key] is JToken token) {
				if (token.Type != JTokenType.String || !rule.Load(token.Value<string>()!, out _))
					RoxyLog.Warning("Invalid saved value for " + rule.Id + "; using default.");
			}
			if (rule.Keybind != null && Root["Keybinds"]![rule.Key] is JToken key) {
				if (key.Type == JTokenType.String && KeyCombination.TryParse(key.Value<string>()!, out KeyCombination combination))
					rule.Keybind.Combination = combination;
				else
					RoxyLog.Warning("Invalid saved keybind for " + rule.Id + "; using None.");
			}
		}
		catch (Exception ex) { RoxyLog.Error("Loading " + rule.Id, ex); }
	}
	internal void Remember(RuleInfo rule) {
		if (rule.Persistent)
			Root["Rules"]![rule.Key] = rule.Serialize();
		else
			((JObject)Root["Rules"]!).Remove(rule.Key);
		if (rule.Keybind != null)
			Root["Keybinds"]![rule.Key] = rule.Keybind.Combination.ToString();
	}
	internal bool Save(IReadOnlyList<RuleInfo> rules) {
		if (!Writable) {
			// A user can move an unreadable config aside and retry without losing this session's edits.
			if (File.Exists(FilePath))
				return false;
			Writable = true;
		}
		try {
			// Serialize everything before touching the original file.
			foreach (RuleInfo rule in rules)
				Remember(rule);
			string contents = Root.ToString(Formatting.Indented);
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			string temp = FilePath + ".tmp";
			File.WriteAllText(temp, contents);
			if (File.Exists(FilePath))
				File.Replace(temp, FilePath, null);
			else
				File.Move(temp, FilePath);
			LastError = null;
			return true;
		}
		catch (Exception ex) {
			LastError = ex.Message;
			RoxyLog.Error("Saving " + FilePath, ex);
			return false;
		}
	}
}
