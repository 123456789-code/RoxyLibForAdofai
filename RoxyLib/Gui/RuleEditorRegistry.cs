using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using ImGuiNET;
using RoxyLib.Setting;
using RoxyLib.Utils;
using UnityEngine;
using Vector4 = System.Numerics.Vector4;

namespace RoxyLib.Gui;

public sealed class RuleEditorContext {
	public RuleInfo Rule { get; }
	internal RuleEditorContext(RuleInfo rule) { Rule = rule; }
	public T GetValue<T>() { return Rule.GetValue<T>(); }
	public bool SetValue<T>(T value) { return Rule.TrySetValue(value!, out _); }
}

/// <summary>Register either a data-only schema or an ImGui callback. Exact stable IDs avoid inheritance ambiguity.</summary>
public static class RuleEditorRegistry {
	private sealed class Editor {
		internal RuleEditorSchema? Schema;
		internal Action<RuleEditorContext>? Draw;
	}
	private static readonly Dictionary<string, Editor> Editors = new(StringComparer.Ordinal);

	public static IDisposable RegisterSchema(string type_id, RuleEditorSchema schema) {
		return Add(type_id, new Editor { Schema = schema ?? throw new ArgumentNullException(nameof(schema)) });
	}
	public static IDisposable RegisterCustom(string type_id, Action<RuleEditorContext> draw) {
		return Add(type_id, new Editor { Draw = draw ?? throw new ArgumentNullException(nameof(draw)) });
	}
	private static IDisposable Add(string type_id, Editor editor) {
		if (Editors.ContainsKey(type_id))
			throw new InvalidOperationException("Editor already registered: " + type_id);
		Editors.Add(type_id, editor);
		return new Registration(() => {
			if (Editors.TryGetValue(type_id, out Editor? current) && ReferenceEquals(current, editor))
				Editors.Remove(type_id);
		});
	}

	internal static void Draw(RuleInfo rule, EditorState state) {
		if (Editors.TryGetValue(rule.Type.Id, out Editor? editor)) {
			if (editor.Draw != null) {
				editor.Draw(new RuleEditorContext(rule));
				return;
			}
			int i = 0;
			foreach (EditorField field in editor.Schema!.Fields) {
				ImGui.PushID(i++);
				if (!string.IsNullOrEmpty(field.Label))
					ImGui.TextUnformatted(LanguageManager.Translate(rule.Id + ".Editor." + field.Label, field.Label));
				object current = field.Read(rule.GetValue());
				bool Apply(object value) {
					try {
						bool result = rule.TrySetValue(field.Write(rule.GetValue(), value), out string? error);
						state.Error = error;
						return result;
					}
					catch (Exception ex) { state.Error = ex.Message; return false; }
				}
				switch (field.Kind) {
				case EditorKind.Toggle:
					bool on = (bool)current;
					if (ImGui.Checkbox("##value", ref on))
						Apply(on);
					break;
				case EditorKind.Options:
					DrawOptions(field.Options!, (string)current, value => Apply(value));
					break;
				case EditorKind.Number:
					DrawNumber(rule.Id + "/" + i, current, field.ComponentType, field.Min!, field.Max!, state, Apply);
					break;
				default:
					DrawText(rule.Id + "/" + i, (string)current, state, value => Apply(value));
					break;
				}
				ImGui.PopID();
			}
			return;
		}
		if (rule.Type.Id == "core/bool") {
			bool on = rule.GetValue<bool>();
			if (ImGui.Checkbox("##value", ref on))
				rule.TrySetValue(on, out _);
		}
		else if (rule.Type.Id == "core/string")
			DrawText(rule.Id, rule.GetValue<string>(), state, value => rule.TrySetValue(value, out _));
		else if (rule.Type.IsNumeric)
			DrawNumber(rule.Id, rule.GetValue(), rule.ValueType, rule.Min!, rule.Max!, state, value => rule.TrySetValue(value, out _));
		else if (rule.Type.Id.StartsWith("core/enum/", StringComparison.Ordinal))
			DrawOptions(Enum.GetNames(rule.ValueType), rule.Serialize(), value => rule.TrySetText(value, out _));
		else if (rule.Type.Id == "core/color") {
			Color color = rule.GetValue<Color>();
			Vector4 rgba = new(color.r, color.g, color.b, color.a);
			if (ImGui.ColorEdit4("##value", ref rgba, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.DisplayHex))
				rule.TrySetValue(new Color(rgba.X, rgba.Y, rgba.Z, rgba.W), out _);
		}
		else {
			ImGui.TextWrapped(rule.Serialize());
			ImGui.TextDisabled(LanguageManager.Translate("RoxyLib.Editor.Missing", "No editor registered for this type."));
		}
	}

	private static void DrawOptions(IReadOnlyList<string> options, string current, Func<string, bool> apply) {
		ImGui.SetNextItemWidth(-1);
		if (!ImGui.BeginCombo("##value", current))
			return;
		try {
			foreach (string option in options)
				if (ImGui.Selectable(option, option == current))
					apply(option);
		}
		finally { ImGui.EndCombo(); }
	}
	private static void DrawText(string id, string current, EditorState state, Func<string, bool> apply) {
		EditorState.Draft draft = state.GetDraft(id, current);
		draft.Commit = apply;
		ImGui.SetNextItemWidth(-1);
		bool enter = ImGui.InputText("##text", ref draft.Text, 4096, ImGuiInputTextFlags.EnterReturnsTrue);
		draft.Active = ImGui.IsItemActive();
		if (enter || ImGui.IsItemDeactivatedAfterEdit()) {
			draft.Invalid = !apply(draft.Text);
			if (!draft.Invalid)
				state.Error = null;
		}
	}
	private static void DrawNumber(string id, object current, Type component_type, object min, object max,
		EditorState state, Func<object, bool> apply) {
		NumericRange range = new(component_type, min, max);
		float fraction = range.Fraction(current);
		ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - 160));
		if (ImGui.SliderFloat("##slider", ref fraction, 0, 1, "")) {
			if (apply(range.Interpolate(fraction)))
				state.Forget(id);
		}
		ImGui.SameLine();
		DrawText(id, Convert.ToString(current, CultureInfo.InvariantCulture)!, state, text => {
			try {
				object parsed = Convert.ChangeType(text, component_type, CultureInfo.InvariantCulture);
				if (!range.Contains(parsed)) { state.Error = $"Value must be between {min} and {max}."; return false; }
				return apply(parsed);
			}
			catch (Exception) { state.Error = LanguageManager.Translate("RoxyLib.Editor.Invalid", "Enter a valid value within the range."); return false; }
		});
	}
}

internal sealed class EditorState {
	internal sealed class Draft {
		internal string Text = "";
		internal bool Active;
		internal bool Invalid;
		internal Func<string, bool>? Commit;
	}
	private readonly Dictionary<string, Draft> Drafts = new();
	internal string? Error;
	internal Draft GetDraft(string id, string current) {
		if (!Drafts.TryGetValue(id, out Draft? draft))
			Drafts[id] = draft = new Draft();
		if (!draft.Active && !draft.Invalid)
			draft.Text = current;
		return draft;
	}
	internal void Forget(string id) { Drafts.Remove(id); Error = null; }
	internal void Clear() { Drafts.Clear(); Error = null; }
	internal bool CommitPending() {
		bool success = true;
		foreach (Draft draft in new List<Draft>(Drafts.Values)) {
			if ((!draft.Active && !draft.Invalid) || draft.Commit == null)
				continue;
			draft.Invalid = !draft.Commit(draft.Text);
			success &= !draft.Invalid;
		}
		return success;
	}
}
