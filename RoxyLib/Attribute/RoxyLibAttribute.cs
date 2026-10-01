using System;

namespace RoxyLib.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RoxyModAttribute : System.Attribute {
	public string ModId { get; set; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class RoxyRuleAttribute : System.Attribute {
	public string Category { get; set; } = "General";
	public new string? TypeId { get; set; }
	public bool Persistent { get; set; } = true;
	public object? Min { get; set; }  // Slider 必填
	public object? Max { get; set; }  // Slider 必填
}
