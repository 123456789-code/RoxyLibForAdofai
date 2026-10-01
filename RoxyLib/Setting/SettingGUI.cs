using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using RoxyLib.Gui;
using RoxyLib.Utils;

namespace RoxyLib.Setting;

/// <summary>Fullscreen navigation and settings. Owns no rule values or persistence logic.</summary>
internal sealed class SettingsPage {
	private string SelectedMod = RoxyLib.MOD_ID;
	private string? Category;
	private string Search = "";
	private readonly Dictionary<RuleInfo, EditorState> States = new();

	internal void ResetTransientState() {
		foreach (EditorState state in States.Values)
			state.Clear();
		KeybindManager.CancelCapture();
	}
	internal bool CommitPending() {
		bool success = true;
		foreach (RuleInfo rule in RuleManager.GetRules())
			if (States.TryGetValue(rule, out EditorState? state))
				success &= state.CommitPending();
		return success;
	}
	internal void Draw() {
		IReadOnlyList<ModHost> hosts = RoxyLib.GetHosts();
		RuleInfo[] all = RuleManager.GetRules().ToArray();
		foreach (RuleInfo stale in States.Keys.Where(x => !all.Contains(x)).ToArray())
			States.Remove(stale);
		if (!hosts.Any(x => x.ModId == SelectedMod)) {
			SelectedMod = hosts.FirstOrDefault()?.ModId ?? "";
			Category = null;
		}
		ImGuiIOPtr io = ImGui.GetIO();
		ImGui.SetNextWindowPos(Vector2.Zero);
		ImGui.SetNextWindowSize(io.DisplaySize);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(20, 16));
		ImGui.Begin("RoxyLib##Fullscreen", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
			| ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
		try {
			ImGui.TextColored(Theme.Accent, "ROXY");
			ImGui.SameLine();
			ImGui.TextDisabled("/ " + T("RoxyLib.Window.Title", "Settings"));
			ImGui.SameLine(Math.Max(180, io.DisplaySize.X - 120));
			if (ImGui.Button(T("RoxyLib.Close", "Close") + "  Esc", new Vector2(80, 30)))
				RoxyLib.SetSettingsOpen(false);
			ImGui.Separator();
			ImGui.SetNextItemWidth(-1);
			string previous_search = Search;
			if (ImGui.InputTextWithHint("##search", T("RoxyLib.Search.Placeholder", "Search mods, rules and descriptions"), ref Search, 256)) {
				if (CommitPending())
					ResetTransientState();
				else
					Search = previous_search;
			}
			ImGui.Spacing();
			float sidebar = Math.Max(185, Math.Min(290, io.DisplaySize.X * 0.21f));
			ImGui.BeginChild("Navigation", new Vector2(sidebar, -28), ImGuiChildFlags.Borders);
			try {
				ImGui.TextDisabled(T("RoxyLib.Left.Title", "MODS"));
				foreach (ModHost host in hosts.OrderBy(x => x.ModId != RoxyLib.MOD_ID).ThenBy(x => x.DisplayName)) {
					int count = all.Count(x => x.ModId == host.ModId && Matches(x, host));
					if (Search.Length > 0 && count == 0)
						continue;
					ImGui.PushID(host.ModId);
					if (ImGui.Selectable(host.DisplayName + "  (" + count + ")", SelectedMod == host.ModId, ImGuiSelectableFlags.None, new Vector2(0, 32)) && CommitPending()) {
						SelectedMod = host.ModId;
						Category = null;
						ResetTransientState();
					}
					if (SelectedMod == host.ModId) {
						ImGui.Indent(12);
						if (ImGui.Selectable(T("RoxyLib.AllCategories", "All categories"), Category == null) && CommitPending()) {
							Category = null;
							ResetTransientState();
						}
						foreach (string category in all.Where(x => x.ModId == host.ModId).Select(x => x.Category).Distinct()) {
							if (ImGui.Selectable(LanguageManager.Translate(host.ModId + ".Category." + category, category), Category == category) && CommitPending()) {
								Category = category;
								ResetTransientState();
							}
						}
						ImGui.Unindent(12);
					}
					ImGui.PopID();
				}
			}
			finally { ImGui.EndChild(); }
			ImGui.SameLine();
			ImGui.BeginChild("Content", new Vector2(0, -28));
			try {
				ModHost? selected = hosts.FirstOrDefault(x => x.ModId == SelectedMod);
				if (selected == null)
					ImGui.TextDisabled(T("RoxyLib.Rule.Empty", "No rules"));
				else {
					ImGui.TextColored(Theme.Accent, selected.DisplayName);
					ImGui.TextDisabled(selected.ModId + (Category == null ? "" : " / " + Category));
					ImGui.Separator();
					var rules = all.Where(x => x.ModId == SelectedMod && (Category == null || x.Category == Category) && Matches(x, selected)).ToList();
					if (rules.Count == 0)
						ImGui.TextDisabled(T("RoxyLib.Rule.Empty", "No matching rules"));
					foreach (var group in rules.GroupBy(x => x.Category)) {
						ImGui.Spacing();
						ImGui.TextColored(Theme.Accent, LanguageManager.Translate(SelectedMod + ".Category." + group.Key, group.Key));
						ImGui.Separator();
						foreach (RuleInfo rule in group)
							DrawRule(rule);
					}
					if (selected.SaveError != null) {
						ImGui.Spacing();
						ImGui.TextColored(Theme.Error, T("RoxyLib.Save.Failed", "Configuration could not be saved."));
						ImGui.TextWrapped(selected.SaveError);
						if (ImGui.Button(T("RoxyLib.Save.Retry", "Retry save")))
							selected.Save();
					}
				}
			}
			finally { ImGui.EndChild(); }
			ImGui.Separator();
			bool dirty = hosts.Any(x => x.HasUnsavedChanges);
			if (hosts.Any(x => x.SaveError != null))
				ImGui.TextColored(Theme.Error, T("RoxyLib.Save.Failed", "Configuration could not be saved."));
			else
				ImGui.TextDisabled(T(dirty ? "RoxyLib.Save.Pending" : "RoxyLib.Save.Saved", dirty ? "Saving changes..." : "Changes saved automatically"));
		}
		finally {
			ImGui.End();
			ImGui.PopStyleVar(2);
		}
	}
	private bool Matches(RuleInfo rule, ModHost host) {
		if (string.IsNullOrWhiteSpace(Search))
			return true;
		string text = host.DisplayName + " " + rule.Id + " " + LanguageManager.Translate(rule.Id, rule.Name)
			+ " " + LanguageManager.Translate(rule.Id + ".Desc", "") + " " + LanguageManager.Translate(rule.ModId + ".Category." + rule.Category, rule.Category);
		return text.IndexOf(Search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
	}
	private void DrawRule(RuleInfo rule) {
		if (!States.TryGetValue(rule, out EditorState? state))
			States[rule] = state = new EditorState();
		ImGui.PushID(rule.Id);
		try {
			if (!ImGui.BeginTable("rule", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
				return;
			try {
				ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, 0.40f);
				ImGui.TableSetupColumn("value", ImGuiTableColumnFlags.WidthStretch, 0.60f);
				ImGui.TableSetupColumn("reset", ImGuiTableColumnFlags.WidthFixed, 60);
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				ImGui.TextWrapped(LanguageManager.Translate(rule.Id, rule.Name));
				string description = LanguageManager.Translate(rule.Id + ".Desc", "");
				if (description.Length > 0) {
					ImGui.PushStyleColor(ImGuiCol.Text, Theme.Muted);
					ImGui.TextWrapped(description);
					ImGui.PopStyleColor();
				}
				ImGui.TableNextColumn();
				try { RuleEditorRegistry.Draw(rule, state); }
				catch (Exception ex) { state.Error = ex.Message; }
				if (rule.Keybind != null) {
					bool capture = ReferenceEquals(KeybindManager.Capturing, rule);
					string label = capture ? T("RoxyLib.Keybind.Press", "Press a key; Esc cancels") : rule.Keybind.Combination.ToString();
					if (ImGui.Button(label + "##key", new Vector2(Math.Max(120, ImGui.GetContentRegionAvail().X - 62), 26)))
						KeybindManager.BeginCapture(rule);
					if (!rule.Keybind.Combination.IsEmpty) {
						ImGui.SameLine();
						if (ImGui.Button(T("RoxyLib.Keybind.Clear", "Clear"))) {
							rule.Keybind.Combination = KeyCombination.None;
							KeybindManager.CancelCapture();
						}
					}
				}
				string? error = state.Error ?? rule.Error;
				if (error != null) {
					ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
					ImGui.TextWrapped(error);
					ImGui.PopStyleColor();
				}
				ImGui.TableNextColumn();
				if (ImGui.Button(T("RoxyLib.Rule.Reset", "Reset"))) { rule.Reset(); state.Clear(); }
			}
			finally { ImGui.EndTable(); }
			ImGui.Spacing();
			ImGui.Separator();
		}
		finally { ImGui.PopID(); }
	}
	private static string T(string key, string fallback) { return LanguageManager.Translate(key, fallback); }
}
