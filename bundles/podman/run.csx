#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var debDir = "deb";
if (!Directory.Exists(debDir))
{
    throw new InvalidOperationException("deb directory not found in bundle payload");
}

var debFiles = Directory.GetFiles(debDir, "*.deb");
if (debFiles.Length == 0)
{
    throw new InvalidOperationException("No .deb files found in bundle payload");
}

var updateProc = new ProcessStartInfo("apt-get", "update") { UseShellExecute = false };
var updateProcess = Process.Start(updateProc);
updateProcess?.WaitForExit();
if (updateProcess is null || updateProcess.ExitCode != 0)
{
    throw new InvalidOperationException("apt-get update failed");
}

var installArgs = "apt-get install -y --no-install-recommends ./deb/*.deb";
var installProc = new ProcessStartInfo("bash", $"-lc \"{installArgs}\"") { UseShellExecute = false };
var installProcess = Process.Start(installProc);
installProcess?.WaitForExit();
if (installProcess is null || installProcess.ExitCode != 0)
{
    throw new InvalidOperationException("apt-get local .deb install failed");
}

Console.WriteLine("podman installed successfully");
