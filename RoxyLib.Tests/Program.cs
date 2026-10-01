using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RoxyLib.Tests;

internal static class Program {
	private static int Main(string[] args) {
		if (args.Length != 3)
			return 2;
		AppDomain.CurrentDomain.AssemblyResolve += (_, request) => {
			string name = new AssemblyName(request.Name).Name + ".dll";
			foreach (string directory in new[] { AppDomain.CurrentDomain.BaseDirectory, args[1], args[2], Path.Combine(args[2], "UnityModManager") }) {
				string path = Path.Combine(directory, name);
				if (File.Exists(path))
					return Assembly.LoadFrom(path);
			}
			return null;
		};
		return Run(args);
	}
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string[] args) {
		try { Regression.Run(args[0], args[1]); return 0; }
		catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
	}
}
