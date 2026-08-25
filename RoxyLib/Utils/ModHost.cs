using System.Reflection;
using UnityModManagerNet;

namespace RoxyLib.Utils;

/// <summary>
/// 一个已注册 mod 的宿主：提供规则扫描所需的程序集与配置路径。
/// </summary>
public sealed class ModHost {
	public string ModId { get; }
	public string Path { get; }
	public Assembly ModAssembly { get; }
	public string DisplayName { get; }

	internal ModHost(UnityModManager.ModEntry entry) {
		ModId = entry.Info.Id;
		DisplayName = entry.Info.DisplayName ?? entry.Info.Id;
		ModAssembly = entry.Assembly;
		Path = entry.Path;
	}
}
