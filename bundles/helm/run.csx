#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var bin = "/usr/local/bin";
Directory.CreateDirectory(bin);

var src = "helm";
var dst = Path.Combine(bin, "helm");
File.Copy(src, dst, overwrite: true);
var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x {dst}") { UseShellExecute = false });
chmod?.WaitForExit();
if (chmod is null || chmod.ExitCode != 0)
{
	throw new InvalidOperationException("Failed to set executable bit on helm binary");
}

Console.WriteLine("helm installed successfully in /usr/local/bin");
