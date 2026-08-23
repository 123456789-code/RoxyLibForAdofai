using System.IO;
using System.Reflection;
using UnityModManagerNet;

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

	internal RoxyHost(UnityModManager.ModEntry entry) {
		ModId = entry.Info.Id;
		DisplayName = entry.Info.DisplayName ?? entry.Info.Id;
		ModAssembly = entry.Assembly;
		ConfigPath = Path.Combine(entry.Path, RoxyLib.CONFIG_FILE);
		LangDir = Path.Combine(entry.Path, RoxyLib.LANG_DIR);
	}
}
