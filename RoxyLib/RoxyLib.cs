using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RoxyLib.Gui;
using RoxyLib.Setting;
using RoxyLib.Utils;
using UnityModManagerNet;

namespace RoxyLib;

/// <summary>UMM integration. Rule discovery, session ownership and GUI hosting meet only here.</summary>
public static class RoxyLib {
	public const string MOD_ID = "RoxyLib";
	private static readonly Dictionary<string, ModHost> Hosts = new(StringComparer.Ordinal);
	private static readonly HashSet<UnityModManager.ModEntry> Entries = [];
	private static readonly Stopwatch Clock = Stopwatch.StartNew();
	private static ImGuiHost? Gui;
	internal static double Now => Clock.Elapsed.TotalSeconds;
	public static bool IsSettingsOpen => Gui != null && Gui.IsOpen;
	public static bool IsInputBlocked => InputBlock.Blocking;

	internal static void AttachRule(RuleInfo rule) {
		Hosts[rule.ModId].Attach(rule);
	}
	internal static void DetachRule(RuleInfo rule) {
		if (Hosts.TryGetValue(rule.ModId, out ModHost? host))
			host.Detach(rule);
	}
	public static bool IsRegistered(string mod_id) { return Hosts.ContainsKey(mod_id); }
	public static IReadOnlyList<ModHost> GetHosts() { return Hosts.Values.ToArray(); }

	/// <summary>Call once after installing your own UMM callbacks. Existing callbacks are preserved.</summary>
	public static void Register(UnityModManager.ModEntry mod_entry) {
		if (!Entries.Add(mod_entry))
			return;
		var previous_toggle = mod_entry.OnToggle;
		var previous_save = mod_entry.OnSaveGUI;
		mod_entry.OnToggle = (entry, enabled) => {
			if (enabled) {
				if (IsRegistered(entry.Info.Id))
					return true;
				bool callback_started = false;
				try {
					Activate(entry.Info.Id, entry.Info.DisplayName ?? entry.Info.Id, entry.Path, entry.Assembly);
					callback_started = true;
					if (previous_toggle != null && !previous_toggle(entry, true)) {
						Deactivate(entry.Info.Id);
						return false;
					}
					return true;
				}
				catch (Exception ex) {
					RoxyLog.Error("Enabling " + entry.Info.Id, ex);
					if (callback_started && previous_toggle != null)
						try { previous_toggle(entry, false); }
						catch (Exception rollback) { RoxyLog.Error("Rollback", rollback); }
					Deactivate(entry.Info.Id);
					return false;
				}
			}
			if (entry.Info.Id == MOD_ID && Hosts.Keys.Any(x => x != MOD_ID)) {
				RoxyLog.Warning("Disable dependent mods before disabling RoxyLib.");
				return false;
			}
			if (entry.Info.Id == MOD_ID) {
				SetSettingsOpen(false);
				if (IsSettingsOpen)
					return false;
			}
			if (Hosts.TryGetValue(entry.Info.Id, out ModHost? host) && host.HasUnsavedChanges && !host.Save())
				return false;
			if (previous_toggle != null && !previous_toggle(entry, false))
				return false;
			Deactivate(entry.Info.Id);
			return true;
		};
		mod_entry.OnSaveGUI = entry => {
			if (Hosts.TryGetValue(entry.Info.Id, out ModHost? host))
				host.Save();
			previous_save?.Invoke(entry);
		};
	}

	internal static void Activate(string mod_id, string display_name, string path, System.Reflection.Assembly assembly) {
		if (Hosts.ContainsKey(mod_id))
			throw new InvalidOperationException("Mod already registered: " + mod_id);
		IReadOnlyList<RuleInfo> rules = RuleManager.Scan(assembly, mod_id);
		ModHost host = new(mod_id, display_name, path, assembly);
		Hosts.Add(mod_id, host);
		try {
			LanguageManager.Load(mod_id, path);
			foreach (RuleInfo rule in rules) {
				// Restore the original default on re-enable, before applying saved config.
				rule.Load(rule.Type.Serialize(rule.DefaultValue), out _);
				RuleManager.Add(rule);
			}
			if (mod_id == MOD_ID) {
				Gui = ImGuiHost.Create(path);
				RuleInfo open_rule = RuleManager.Find(MOD_ID, "General.OpenSettings")!;
				open_rule.ValueChanged += OnOpenChanged;
				Gui.SetOpen(false);
			}
		}
		catch {
			Deactivate(mod_id);
			throw;
		}
	}
	private static void OnOpenChanged(RuleInfo rule) {
		if (Gui != null && !Gui.SetOpen(rule.GetValue<bool>()))
			rule.TrySetValue(true, out _);
	}
	internal static void Deactivate(string mod_id) {
		if (mod_id == MOD_ID) {
			if (Gui != null) {
				Gui.SetOpen(false, discard: true);
				UnityEngine.Object.Destroy(Gui.gameObject);
			}
			Gui = null;
			RoxyLibRules.OpenSettings = false;
		}
		RuleManager.RemoveMod(mod_id);
		LanguageManager.Remove(mod_id);
		Hosts.Remove(mod_id);
	}
	public static void Tick(float dt) {
		KeybindManager.Update(IsSettingsOpen);
		foreach (ModHost host in Hosts.Values.ToArray())
			host.Tick();
	}
	public static void SaveAll() {
		foreach (ModHost host in Hosts.Values.ToArray())
			host.Save();
	}
	public static void OpenSettings() { SetSettingsOpen(true); }
	internal static void SetSettingsOpen(bool open) {
		RuleInfo? rule = RuleManager.Find(MOD_ID, "General.OpenSettings");
		if (rule != null)
			rule.TrySetValue(open, out _);
	}
}
