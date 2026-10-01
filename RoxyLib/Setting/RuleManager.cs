using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoxyLib.Attribute;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>Owns identities and notifications; session owners attach persistence when rules arrive.</summary>
public static class RuleManager {
	private static readonly Dictionary<string, List<RuleInfo>> Rules = new(StringComparer.Ordinal);
	private static readonly Dictionary<FieldInfo, string> Defaults = new();
	public static event Action<RuleInfo>? Registered;
	public static event Action<RuleInfo>? Removed;

	public static IReadOnlyList<RuleInfo> GetRules(string? mod_id = null) {
		return mod_id == null ? Rules.Values.SelectMany(x => x).ToArray()
			: Rules.TryGetValue(mod_id, out List<RuleInfo>? rules) ? rules.AsReadOnly() : Array.Empty<RuleInfo>();
	}
	public static RuleInfo? Find(string mod_id, string key) {
		return GetRules(mod_id).FirstOrDefault(x => x.Key == key);
	}
	public static RuleInfo Register(RuleInfo rule) {
		if (!RoxyLib.IsRegistered(rule.ModId))
			throw new InvalidOperationException("Register the mod before adding runtime rules.");
		return Add(rule);
	}
	internal static RuleInfo Add(RuleInfo rule) {
		if (!Rules.TryGetValue(rule.ModId, out List<RuleInfo>? rules))
			Rules[rule.ModId] = rules = [];
		if (rules.Any(x => x.Key == rule.Key))
			throw new InvalidOperationException("Duplicate rule: " + rule.Id);
		rules.Add(rule);
		try { RoxyLib.AttachRule(rule); Registered?.Invoke(rule); }
		catch { rules.Remove(rule); NotifyRemoved(rule); throw; }
		return rule;
	}
	public static void Remove(RuleInfo rule) {
		if (Rules.TryGetValue(rule.ModId, out List<RuleInfo>? rules) && rules.Remove(rule))
			NotifyRemoved(rule);
	}
	private static void NotifyRemoved(RuleInfo rule) {
		RoxyLib.DetachRule(rule);
		if (Removed == null)
			return;
		foreach (Action<RuleInfo> handler in Removed.GetInvocationList())
			try { handler(rule); }
			catch (Exception ex) { RoxyLog.Error("Removing " + rule.Id, ex); }
	}
	internal static void RemoveMod(string mod_id) {
		foreach (RuleInfo rule in GetRules(mod_id).ToArray())
			Remove(rule);
		Rules.Remove(mod_id);
	}
	internal static IReadOnlyList<RuleInfo> Scan(Assembly assembly, string mod_id) {
		List<RuleInfo> result = [];
		// Fail registration as one transaction if any declared rule is invalid.
		foreach (Type type in assembly.GetTypes().OrderBy(x => x.FullName, StringComparer.Ordinal)) {
			if (type.GetCustomAttribute<RoxyModAttribute>()?.ModId != mod_id)
				continue;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken)) {
				RoxyRuleAttribute? attribute = field.GetCustomAttribute<RoxyRuleAttribute>();
				if (attribute == null)
					continue;
				if (field.IsInitOnly || field.IsLiteral)
					throw new InvalidOperationException(field.Name + " must be a writable static field.");
				RuleType rule_type = RuleTypeRegistry.Resolve(field.FieldType, attribute.TypeId);
				object default_value = field.GetValue(null) ?? throw new InvalidOperationException(field.Name + " cannot be null.");
				if (Defaults.TryGetValue(field, out string? cached)) {
					if (!rule_type.TryParse(cached, out object? parsed, out string? error))
						throw new InvalidOperationException(error);
					default_value = parsed!;
				}
				RuleInfo rule = new(mod_id, field.Name, attribute.Category, rule_type, default_value,
					attribute.Min, attribute.Max, attribute.Persistent, field);
				Defaults[field] = rule_type.Serialize(default_value);
				if (result.Any(x => x.Key == rule.Key))
					throw new InvalidOperationException("Duplicate rule: " + rule.Id);
				result.Add(rule);
			}
		}
		return result;
	}
}
