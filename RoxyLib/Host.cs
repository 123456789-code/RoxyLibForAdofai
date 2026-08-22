using System.IO;
using System.Reflection;

namespace RoxyLib;

/// <summary>
/// 一个已注册 mod 的宿主：提供规则扫描所需的程序集与配置路径。
/// </summary>
public sealed class RoxyHost {
	public string ModId { get; }
	public string ConfigPath { get; }
	public string LangDir { get; }
	public Assembly ModAssembly { get; }
	public string DisplayName { get; }

	internal RoxyHost(string mod_id, string path, string display_name, Assembly assembly) {
		ModId = mod_id;
		DisplayName = display_name;
		ModAssembly = assembly;
		ConfigPath = Path.Combine(path, RoxyLib.CONFIG_FILE);
		LangDir = Path.Combine(path, RoxyLib.LANG_DIR);
	}
}

/// <summary>
/// 从 UMM 的 ModEntry 构造 RoxyHost（反射读取，核心库不依赖 UMM.dll）
/// </summary>
public static class RoxyHosts {
	public static RoxyHost FromUmm(object mod_entry) {
		// UMM 源码核实：ModEntry.Path/Info 是字段，ModInfo.Id/DisplayName 是字段，ModEntry.Assembly 是属性
		string path = (string)GetMember(mod_entry, "Path")!;
		object info = GetMember(mod_entry, "Info")!;
		string mod_id = (string)GetMember(info, "Id")!;
		string display_name = (string?)GetMember(info, "DisplayName") ?? mod_id;
		Assembly assembly = (Assembly?)GetMember(mod_entry, "Assembly") ?? Assembly.GetCallingAssembly();
		return new RoxyHost(mod_id, path, display_name, assembly);
	}

	/// <summary>字段或属性统一读取（先字段后属性，兼容 UMM 各版本成员形态）</summary>
	private static object? GetMember(object target, string name) {
		return target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
			?? target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
	}
}
