using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using RoxyLib.Gui;
using RoxyLib.Utils;
using RoxyLib.Setting;
using RoxyLib.Overlay;

namespace RoxyLib;

/// <summary>
/// RLA 核心入口，业务 mod 一行注册：RoxyLib.Register(mod_entry)，生命周期全权接管
/// </summary>
public static class RoxyLib {
	public const string MOD_ID = "RoxyLib";
	public const string CONFIG_FILE = "config.json";
	public const string LANG_DIR = "lang";

	private static readonly List<ModHost> Hosts = [ ];
	private static readonly RoxyGui Gui = new();
	private static readonly OverlayGUI OverlayGUI = new();
	private static float LastSaveTime; // 最后一次存储数据的时间
	public static bool Dirty { get; internal set; } // 是否有未存储的数据
	public static long Revision { get; private set; } // mod 列表变化的计数器

	public static event Action<ModHost>? ModRegistered;
	public static event Action<string>? ModUnregistered;
	public static event Action<long>? RevisionChanged;

	public static void Register(UnityModManager.ModEntry mod_entry) {
		mod_entry.OnToggle = ToggleHandler;
		mod_entry.OnSaveGUI = entry => SaveAll();
	}

	public static void Unregister(string mod_id) {
		for (int i = Hosts.Count - 1; i >= 0; i--) {
			if (Hosts[i].ModId == mod_id) {
				RuleManager.RemoveMod(mod_id);
				OverlayManager.RemoveMod(mod_id);
				Hosts.RemoveAt(i);
				KeybindManager.UpdateKeybindings();
				ModUnregistered?.Invoke(mod_id);
				RevisionChanged?.Invoke(++Revision);
				return;
			}
		}
	}

	public static void Tick(float dt) {
		KeybindManager.Update(dt);
		Gui.OnUpdate(dt);
		if (Dirty && Time.realtimeSinceStartup - LastSaveTime > 0.5f) {
			Dirty = false;
			LastSaveTime = Time.realtimeSinceStartup;
			SaveAll();
		}
	}

	public static void SaveAll() {
		foreach (var host in Hosts) {
			Storage.SaveAll(RuleManager.GetRules(host.ModId), host.ConfigPath);
		}
		Dirty = false;
	}

	public static void OpenSettings() {
		Gui.OpenFromExternal();
	}

	public static IReadOnlyList<ModHost> GetHosts(){
		return Hosts.AsReadOnly();
	}

	private static bool ToggleHandler(UnityModManager.ModEntry mod_entry, bool value) {
		if (value) {
			var host = new ModHost(mod_entry);
			if (Hosts.Any(existing => existing.ModId == host.ModId))
				return true;
			Hosts.Add(host);

			RuleManager.ScanAssembly(mod_entry.Assembly);
			OverlayManager.ScanAssembly(mod_entry.Assembly);
			OverlayGUI.Initialize();
			LanguageManager.LoadLangDir(host.LangDir);
			IReadOnlyList<RuleInfo> rules = RuleManager.GetRules(host.ModId);
			Storage.LoadAll(rules, host.ConfigPath);
			foreach (RuleInfo rule in rules) {
				rule.ValueChanged += (sender, args) => Dirty = true;
				if (rule.RuleType == RuleType.Switch) {
					RuleInfo captured = rule; // 防御性写法，防止lambda问题
					rule.Keybind!.Activated += () => captured.SetValue(!(bool)captured.GetValue(), true);
				}
			}
			KeybindManager.UpdateKeybindings();
			ModRegistered?.Invoke(host);
			RevisionChanged?.Invoke(++Revision);
		}
		else {
			Unregister(mod_entry.Info.Id);
		}
		return true;
	}
}
