using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RoxyLib.Attribute;
using RoxyLib.Gui;
using RoxyLib.Setting;
using RoxyLib.Utils;
using UnityEngine;

namespace RoxyLib.Tests;

[RoxyMod(ModId = "TestMod")]
public static class TestRules {
	[RoxyRule(Min = 0, Max = 100)]
	public static int Count = 10;
	[RoxyRule]
	public static bool Enabled = true;
	[RoxyRule(Persistent = false)]
	public static bool SessionOpen = false;
}

internal static class Regression {
	private static int Checks;
	private static string Root = "";
	internal static void Run(string root, string library_output) {
		Root = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
		Directory.CreateDirectory(Root);
		RoxyLog.Sink = message => Console.WriteLine("diagnostic: " + message);
		RegistryAndValues();
		HotkeysAndDrafts();
		Persistence();
		Lifecycle();
		Language();
		NativeUi.Run(Root, library_output);
		Console.WriteLine($"PASS: {Checks} assertions; native GUI smoke and software preview completed.");
	}
	internal static void Check(bool condition, string message) {
		Checks++;
		if (!condition)
			throw new InvalidOperationException("FAIL: " + message);
	}
	private static void Throws(Action action, string message) {
		bool threw = false;
		try { action(); }
		catch { threw = true; }
		Check(threw, message);
	}
	private static RuleInfo Number(string name = "Count", int value = 10) {
		return new RuleInfo("TestMod", name, "General", RuleTypeRegistry.Resolve(typeof(int)), value, 0, 100);
	}
	private static void RegistryAndValues() {
		RuleInfo count = Number();
		int changed = 0;
		count.ValueChanged += _ => changed++;
		Check(count.TrySetValue(20, out _) && changed == 1, "one notification for a change");
		Check(count.TrySetValue(20, out _) && changed == 1, "no notification for identical assignment");
		Check(!count.TrySetValue(101, out _) && count.GetValue<int>() == 20, "out-of-range updates cannot mutate values");
		Check(!count.TrySetValue("25", out _), "typed setter rejects accidental string conversion");
		Check(count.TrySetText("25", out _) && count.GetValue<int>() == 25, "text uses the registered parser");
		Check(count.Reset() && count.GetValue<int>() == 10, "reset restores declared default");
		count.ValueChanged += _ => throw new Exception("intentional consumer failure");
		int observed = 0;
		count.ValueChanged += _ => observed++;
		count.TrySetValue(30, out _);
		Check(observed == 1, "consumer failure cannot stop later observers");
		Throws(() => new RuleInfo("TestMod", "Bad", "General", RuleTypeRegistry.Resolve(typeof(int)), 5), "numeric bounds required");
		Throws(() => new RuleInfo("TestMod", "Bad", "General", RuleTypeRegistry.Resolve(typeof(int)), 5, 10, 1), "reversed bounds rejected");
		Throws(() => RuleTypeRegistry.Resolve(typeof(DateTime)), "unknown types fail explicitly");
		var type = new RuleType<DateTime>("tests/date", x => x.ToString("O", CultureInfo.InvariantCulture),
			(string s, out DateTime v) => DateTime.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out v));
		using (RuleTypeRegistry.Register(type)) {
			Check(ReferenceEquals(RuleTypeRegistry.Resolve(typeof(DateTime)), type), "external field inference");
			Check(ReferenceEquals(RuleTypeRegistry.Resolve(typeof(DateTime), "tests/date"), type), "explicit external type");
			Throws(() => RuleTypeRegistry.Register(type), "duplicate type IDs rejected");
			Throws(() => RuleTypeRegistry.Resolve(typeof(string), "tests/date"), "mismatched explicit type rejected");
		}
		Throws(() => RuleTypeRegistry.Resolve(typeof(DateTime)), "registration token unregisters inference");
		CultureInfo before = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");
			RuleInfo number = new("TestMod", "Float", "General", RuleTypeRegistry.Resolve(typeof(double)), 1.25, 0, 100);
			Check(number.Serialize() == "1.25" && number.TrySetText("2.75", out _), "config numeric format is culture invariant");
			Check(!number.TrySetText("NaN", out _) && !number.TrySetText("Infinity", out _), "nonfinite values rejected");
		}
		finally { CultureInfo.CurrentCulture = before; }
		RuleInfo wide = new("TestMod", "Wide", "General", RuleTypeRegistry.Resolve(typeof(ulong)), ulong.MaxValue, 0UL, ulong.MaxValue);
		Check(wide.Serialize() == "18446744073709551615", "64-bit exact serialization");
		Check(wide.TrySetText("18446744073709551614", out _) && wide.GetValue<ulong>() == ulong.MaxValue - 1, "64-bit exact text editing");
		RuleInfo huge = new("TestMod", "Huge", "General", RuleTypeRegistry.Resolve(typeof(double)), double.MaxValue, -double.MaxValue, double.MaxValue);
		Check(huge.TrySetText(huge.Serialize(), out _) && huge.GetValue<double>() == double.MaxValue, "full floating domain round-trips exactly");
		NumericRange floating = new(typeof(double), -double.MaxValue, double.MaxValue);
		Check(floating.Fraction(0d) == 0.5f && (double)floating.Interpolate(0.5f) == 0, "full floating range slider avoids overflow");
		NumericRange exact = new(typeof(decimal), decimal.MinValue, decimal.MaxValue);
		Check(exact.Fraction(0m) == 0.5f && (decimal)exact.Interpolate(0.5f) == 0, "full decimal range slider avoids overflow");
		Throws(() => new NumericRange(typeof(int), 0.1, 10), "fractional integer bounds rejected");
		Throws(() => new NumericRange(typeof(float), 0, double.MaxValue), "unrepresentable floating bounds rejected");
		RuleInfo color = new("TestMod", "Color", "General", RuleTypeRegistry.Resolve(typeof(Color)), new Color(1, 0, 0, 1));
		Check(color.Serialize() == "#FF0000FF" && color.TrySetText("#00FF0080", out _), "RGBA round-trip");
		Check(!color.TrySetValue(new Color(255, 0, 0, 1), out _), "color channels validated");
		Check(KeyCombination.TryParse("Ctrl+Shift+K", out KeyCombination chord) && chord.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift), "chord parser");
		Check(chord.ToString() == "Ctrl+Shift+K", "chord format");
		Check(!KeyCombination.TryParse("Bogus+K", out _) && !KeyCombination.TryParse("Ctrl+LeftShift", out _), "invalid chords rejected");
		Check(KeyCombination.TryParse("None", out chord) && chord.IsEmpty, "no default key");
		using (RuleEditorRegistry.RegisterSchema("tests/schema", new RuleEditorSchema(EditorField.Text<string>("Value", x => x, (_, v) => v))))
			Throws(() => RuleEditorRegistry.RegisterCustom("tests/schema", _ => { }), "ambiguous editor registration rejected");
	}
	private static void HotkeysAndDrafts() {
		RuleInfo first = new("TestMod", "First", "Keys", RuleTypeRegistry.Resolve(typeof(bool)), false);
		RuleInfo second = new("TestMod", "Second", "Keys", RuleTypeRegistry.Resolve(typeof(bool)), false);
		RuleInfo[] rules = [first, second];
		first.Keybind!.Combination = second.Keybind!.Combination = new KeyCombination(KeyCode.K, KeyModifiers.Control);
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control, false);
		Check(first.GetValue<bool>() && second.GetValue<bool>(), "conflicting chords toggle all rules in the same batch");
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control, false);
		Check(first.GetValue<bool>() && second.GetValue<bool>(), "held keys do not retrigger");
		KeybindManager.Poll(rules, _ => false, KeyModifiers.None, false);
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control, true);
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control, false);
		Check(first.GetValue<bool>() && second.GetValue<bool>(), "captured or obscured key does not fire when suppression ends");
		KeybindManager.Poll(rules, _ => false, KeyModifiers.None, false);
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control | KeyModifiers.Shift, false);
		Check(first.GetValue<bool>(), "extra modifiers do not match a chord");
		KeybindManager.Poll(rules, _ => false, KeyModifiers.None, false);
		KeybindManager.Poll(rules, _ => true, KeyModifiers.Control, false);
		Check(!first.GetValue<bool>() && !second.GetValue<bool>(), "release and press retriggers every matching rule");
		EditorState state = new();
		RuleInfo number = Number();
		EditorState.Draft draft = state.GetDraft("number", "10");
		draft.Active = true;
		draft.Text = "42";
		draft.Commit = text => number.TrySetText(text, out _);
		Check(state.CommitPending() && number.GetValue<int>() == 42, "closing commits the currently active text field");
		draft.Text = "999";
		Check(!state.CommitPending() && draft.Invalid && number.GetValue<int>() == 42, "invalid draft stays visible and prevents silent loss on close");
		draft.Text = "33";
		Check(state.CommitPending() && !draft.Invalid && number.GetValue<int>() == 33, "correcting a draft permits close");
	}
	private static void Persistence() {
		string directory = Path.Combine(Root, "config");
		Directory.CreateDirectory(directory);
		ConfigStore store = new(directory, "TestMod");
		RuleInfo value = Number();
		RuleInfo transient = new("TestMod", "Session", "General", RuleTypeRegistry.Resolve(typeof(bool)), false, persistent: false);
		transient.TrySetValue(true, out _);
		transient.Keybind!.Combination = new KeyCombination(KeyCode.K, KeyModifiers.Control);
		Check(store.Save(new[] { value, transient }), "first save");
		string file = Path.Combine(directory, "config.json");
		Check(File.Exists(file) && !Directory.Exists(file), "first save uses a file, not config.json/config.json");
		var json = JObject.Parse(File.ReadAllText(file));
		Check(json["Rules"]!["General.Session"] == null, "transient open state is not persisted");
		Check(json["Keybinds"]!["General.Session"]!.Value<string>() == "Ctrl+K", "transient rule keybind is persisted");
		json["Rules"]!["General.Future"] = "42";
		json["Rules"]!["General.Count"] = "not-a-number";
		File.WriteAllText(file, json.ToString());
		store = new ConfigStore(directory, "TestMod");
		store.Load(value);
		Check(value.GetValue<int>() == 10, "invalid value leaves default intact");
		RuleInfo future = Number("Future");
		store.Load(future);
		Check(future.GetValue<int>() == 42, "another rule loads despite an invalid neighbor");
		value.TrySetValue(35, out _);
		Check(store.Save(new[] { value }), "atomic replacement succeeds");
		json = JObject.Parse(File.ReadAllText(file));
		Check(json["Rules"]!["General.Future"]!.Value<string>() == "42", "unknown or temporarily removed values survive save");
		Check(!File.Exists(file + ".tmp"), "temporary file consumed on success");
		File.WriteAllText(file, "{broken");
		ConfigStore broken = new(directory, "TestMod");
		Check(!broken.Save(new[] { value }) && File.ReadAllText(file) == "{broken", "unreadable original never overwritten");
		File.Move(file, file + ".preserved");
		Check(broken.Save(new[] { value }) && File.ReadAllText(file + ".preserved") == "{broken", "moving unreadable original aside allows explicit retry");
		string failed = Path.Combine(Root, "write-failure");
		Directory.CreateDirectory(Path.Combine(failed, "config.json"));
		ConfigStore denied = new(failed, "TestMod");
		Check(!denied.Save(new[] { value }) && denied.LastError != null, "save error retained for UI/retry");
	}
	private static void Lifecycle() {
		string directory = Path.Combine(Root, "lifecycle");
		RoxyLib.Activate("TestMod", "Tests", directory, typeof(TestRules).Assembly);
		RuleInfo rule = RuleManager.Find("TestMod", "General.Count")!;
		Check(rule.GetValue<int>() == 10 && (int)rule.DefaultValue == 10, "field discovery captures defaults");
		rule.TrySetValue(80, out _);
		Check(TestRules.Count == 80, "field-backed update");
		ModHost host = RoxyLib.GetHosts().Single();
		Check(host.HasUnsavedChanges, "field change marks its own host dirty");
		RuleInfo runtime = Number("Late");
		RuleManager.Register(runtime);
		runtime.TrySetValue(66, out _);
		Check(host.Save() && !host.HasUnsavedChanges, "runtime rule participates in save");
		Throws(() => RuleManager.Register(Number("Late")), "duplicate rule identities rejected");
		RuleManager.Remove(runtime);
		RuleInfo replacement = Number("Late");
		RuleManager.Register(replacement);
		Check(replacement.GetValue<int>() == 66, "re-register loads remembered value");
		RuleInfo session = RuleManager.Find("TestMod", "General.SessionOpen")!;
		session.TrySetValue(true, out _);
		session.Keybind!.Combination = new KeyCombination(KeyCode.F3);
		host.Save();
		RoxyLib.Deactivate("TestMod");
		Check(RuleManager.GetRules("TestMod").Count == 0 && RoxyLib.GetHosts().Count == 0, "deactivation removes session and all rules");
		RoxyLib.Activate("TestMod", "Tests", directory, typeof(TestRules).Assembly);
		rule = RuleManager.Find("TestMod", "General.Count")!;
		Check(rule.GetValue<int>() == 80 && (int)rule.DefaultValue == 10, "re-enable separates saved value from original default");
		session = RuleManager.Find("TestMod", "General.SessionOpen")!;
		Check(!session.GetValue<bool>() && session.Keybind!.Combination.Key == KeyCode.F3, "re-enable resets transient state but restores binding");
		RoxyLib.Deactivate("TestMod");
		Throws(() => RuleManager.Register(Number()), "runtime registration requires active owner");
		void Reject(RuleInfo value) { throw new InvalidOperationException("intentional registration failure"); }
		RuleManager.Registered += Reject;
		try {
			Throws(() => RoxyLib.Activate("TestMod", "Tests", directory, typeof(TestRules).Assembly), "failed activation is reported");
			Check(!RoxyLib.IsRegistered("TestMod") && RuleManager.GetRules("TestMod").Count == 0, "failed activation rolls back host and rules");
		}
		finally { RuleManager.Registered -= Reject; }
	}
	private static void Language() {
		string directory = Path.Combine(Root, "lang-test");
		Directory.CreateDirectory(Path.Combine(directory, "lang"));
		File.WriteAllText(Path.Combine(directory, "lang", "en-us.json"), "{\"Demo.Title\":\"English fallback\",\"Other.Title\":\"Forbidden\"}");
		File.WriteAllText(Path.Combine(directory, "lang", "zh-cn.json"), "{\"Demo.Local\":\"中文\"}");
		LanguageManager.Load("Demo", directory);
		Check(LanguageManager.Translate("Demo.Title", "raw", LanguageEnum.ZhCn) == "English fallback", "English fallback chain");
		Check(LanguageManager.Translate("Demo.Local", "raw", LanguageEnum.ZhCn) == "中文", "selected language");
		Check(LanguageManager.Translate("Other.Title", "raw", LanguageEnum.EnUs) == "raw", "translations are scoped to their owner");
		LanguageManager.Remove("Demo");
		Check(LanguageManager.Translate("Demo.Title", "raw", LanguageEnum.ZhCn) == "raw", "translations released on unload");
	}
}
