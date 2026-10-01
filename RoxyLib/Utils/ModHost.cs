using System;
using System.Reflection;
using RoxyLib.Setting;

namespace RoxyLib.Utils;

/// <summary>One active mod and its configuration session.</summary>
public sealed class ModHost {
	public string ModId { get; }
	public string Path { get; }
	public Assembly ModAssembly { get; }
	public string DisplayName { get; }
	public bool HasUnsavedChanges { get; private set; }
	public string? SaveError => Store.LastError;
	private readonly ConfigStore Store;
	private double LastChange;
	private double LastAttempt = double.NegativeInfinity;
	internal ModHost(string mod_id, string display_name, string path, Assembly assembly) {
		ModId = mod_id;
		DisplayName = display_name;
		Path = path;
		ModAssembly = assembly;
		Store = new ConfigStore(path, mod_id);
	}
	internal void Attach(RuleInfo rule) {
		Store.Load(rule);
		rule.Changed += OnChanged;
	}
	internal void Detach(RuleInfo rule) {
		try { Store.Remember(rule); }
		catch (Exception ex) { RoxyLog.Error("Remembering removed rule " + rule.Id, ex); }
		rule.Changed -= OnChanged;
	}
	private void OnChanged(RuleInfo rule) {
		// The store decides which values persist; bindings persist even on transient rules.
		HasUnsavedChanges = true;
		LastChange = RoxyLib.Now;
	}
	internal void Tick() {
		double now = RoxyLib.Now;
		if (HasUnsavedChanges && (now - LastChange >= 0.5) && (now - LastAttempt >= 1))
			Save();
	}
	public bool Save() {
		LastAttempt = RoxyLib.Now;
		bool success = Store.Save(RuleManager.GetRules(ModId));
		if (success)
			HasUnsavedChanges = false;
		return success;
	}
}
