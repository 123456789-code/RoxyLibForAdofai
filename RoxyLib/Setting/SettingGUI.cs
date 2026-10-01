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
	internal string SelectedMod { get; private set; } = RoxyLib.MOD_ID;
	private string? Category;
	private string Search = "";
	private string? PendingMod;
	private string? PendingCategory;
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
		// Apply navigation once, before either pane is drawn. A click cannot change this frame's expansion halfway through the loop.
		if (PendingMod != null) {
			SelectedMod = PendingMod;
			Category = PendingCategory;
			PendingMod = null;
			ResetTransientState();
		}
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
			ImGui.AlignTextToFramePadding();
			ImGui.TextColored(Theme.Accent, "ROXY");
			ImGui.SameLine();
			ImGui.TextDisabled("/ " + T("RoxyLib.Window.Title", "Settings"));
			string close_label = T("RoxyLib.Close", "Close") + "  Esc";
			float close_width = ImGui.CalcTextSize(close_label).X + ImGui.GetStyle().FramePadding.X * 2;
			ImGui.SameLine(io.DisplaySize.X - ImGui.GetStyle().WindowPadding.X - close_width);
			if (ImGui.Button(close_label, new Vector2(close_width, ImGui.GetFrameHeight())))
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
			float footer = ImGui.GetFrameHeight();
			ImGui.BeginChild("Navigation", new Vector2(sidebar, -footer), ImGuiChildFlags.Borders);
			try {
				ImGui.TextDisabled(T("RoxyLib.Left.Title", "MODS"));
				foreach (ModHost host in hosts.OrderBy(x => x.ModId != RoxyLib.MOD_ID).ThenBy(x => x.DisplayName)) {
					int count = all.Count(x => x.ModId == host.ModId && Matches(x, host));
					if (Search.Length > 0 && count == 0)
						continue;
					ImGui.PushID(host.ModId);
					if (ImGui.Selectable(host.DisplayName + "  (" + count + ")", SelectedMod == host.ModId, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetFrameHeight())))
						RequestNavigation(host.ModId, null);
					if (SelectedMod == host.ModId) {
						ImGui.Indent(12);
						if (ImGui.Selectable(T("RoxyLib.AllCategories", "All categories"), Category == null))
							RequestNavigation(SelectedMod, null);
						foreach (string category in all.Where(x => x.ModId == host.ModId).Select(x => x.Category).Distinct()) {
							if (ImGui.Selectable(LanguageManager.Translate(host.ModId + ".Category." + category, category) + "##" + category, Category == category))
								RequestNavigation(SelectedMod, category);
						}
						ImGui.Unindent(12);
					}
					ImGui.PopID();
				}
			}
			finally { ImGui.EndChild(); }
			ImGui.SameLine();
			ImGui.BeginChild("Content", new Vector2(0, -footer));
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
	internal void RequestNavigation(string mod_id, string? category) {
		if (!CommitPending())
			return;
		PendingMod = mod_id;
		PendingCategory = category;
	}
	private void DrawRule(RuleInfo rule) {
		if (!States.TryGetValue(rule, out EditorState? state))
			States[rule] = state = new EditorState();
		ImGui.PushID(rule.Id);
		try {
			if (!ImGui.BeginTable("rule", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
				return;
			try {
				ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, 0.38f);
				ImGui.TableSetupColumn("value", ImGuiTableColumnFlags.WidthStretch, 0.62f);
				float reset_width = Math.Max(60, ImGui.CalcTextSize(T("RoxyLib.Rule.Reset", "Reset")).X + ImGui.GetStyle().FramePadding.X * 2);
				ImGui.TableSetupColumn("reset", ImGuiTableColumnFlags.WidthFixed, reset_width);
				ImGui.TableNextRow();
				ImGui.TableSetColumnIndex(0);
				float row_y = ImGui.GetCursorPosY();
				float label_width = ImGui.GetContentRegionAvail().X;
				string name = LanguageManager.Translate(rule.Id, rule.Name);
				string description = LanguageManager.Translate(rule.Id + ".Desc", "");
				float label_height = ImGui.CalcTextSize(name, false, label_width).Y;
				if (description.Length > 0)
					label_height += 4 + ImGui.CalcTextSize(description, false, label_width).Y;
				float control_height = ImGui.GetFrameHeight();
				ImGui.TableSetColumnIndex(1);
				ImGui.SetCursorPosY(row_y + Math.Max(0, (label_height - control_height) / 2));
				ImGui.BeginGroup();
				try {
					if (rule.Keybind != null)
						DrawBoolean(rule, state);
					else
						RuleEditorRegistry.Draw(rule, state);
				}
				catch (Exception ex) { state.Error = ex.Message; }
				ImGui.EndGroup();
				float row_height = Math.Max(label_height, Math.Max(0, (label_height - control_height) / 2) + ImGui.GetItemRectSize().Y);
				ImGui.TableSetColumnIndex(0);
				// Clear the frame-control baseline inherited from the editor column before positioning plain text.
				ImGui.Dummy(Vector2.Zero);
				ImGui.SetCursorPosY(row_y + (row_height - label_height) / 2);
				ImGui.BeginGroup();
				ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 4));
				ImGui.TextWrapped(name);
				if (description.Length > 0) {
					ImGui.PushStyleColor(ImGuiCol.Text, Theme.Muted);
					ImGui.TextWrapped(description);
					ImGui.PopStyleColor();
				}
				ImGui.PopStyleVar();
				ImGui.EndGroup();
				ImGui.TableSetColumnIndex(2);
				ImGui.SetCursorPosY(row_y + (row_height - control_height) / 2);
				if (ImGui.Button(T("RoxyLib.Rule.Reset", "Reset"), new Vector2(reset_width, control_height))) { rule.Reset(); state.Clear(); }
				string? error = state.Error ?? rule.Error;
				if (error != null) {
					ImGui.TableNextRow();
					ImGui.TableSetColumnIndex(1);
					ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
					ImGui.TextWrapped(error);
					ImGui.PopStyleColor();
				}
			}
			finally { ImGui.EndTable(); }
			ImGui.Separator();
		}
		finally { ImGui.PopID(); }
	}
	private static void DrawBoolean(RuleInfo rule, EditorState state) {
		float available = ImGui.GetContentRegionAvail().X;
		float gap = ImGui.GetStyle().ItemSpacing.X;
		float height = ImGui.GetFrameHeight();
		string clear_label = T("RoxyLib.Keybind.Clear", "Clear");
		float clear_width = Math.Max(60, ImGui.CalcTextSize(clear_label).X + ImGui.GetStyle().FramePadding.X * 2);
		// Keep all four controls in one row, including the disabled empty-binding reset.
		ImGui.BeginGroup();
		RuleEditorRegistry.Draw(rule, state);
		ImGui.EndGroup();
		float toggle_width = ImGui.GetItemRectSize().X;
		ImGui.SameLine(0, gap);
		bool capture = ReferenceEquals(KeybindManager.Capturing, rule);
		string label = capture ? T("RoxyLib.Keybind.Capturing", "Press a key...") : rule.Keybind!.Combination.ToString();
		if (ImGui.Button(label + "##key", new Vector2(Math.Max(1, available - toggle_width - clear_width - gap * 2), height)))
			KeybindManager.BeginCapture(rule);
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(capture ? T("RoxyLib.Keybind.Press", "Press a key; Esc cancels") : label);
		ImGui.SameLine(0, gap);
		ImGui.BeginDisabled(rule.Keybind!.Combination.IsEmpty && !capture);
		if (ImGui.Button(clear_label, new Vector2(clear_width, height))) {
			rule.Keybind.Combination = KeyCombination.None;
			KeybindManager.CancelCapture();
		}
		ImGui.EndDisabled();
	}
	private static string T(string key, string fallback) { return LanguageManager.Translate(key, fallback); }
}
