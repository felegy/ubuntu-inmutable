#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var bin = "/usr/local/bin";
var archiveDir = "/var/cache/apt/archives";
Directory.CreateDirectory(archiveDir);

// Copy .deb files from bundle into apt cache
var debDir = "deb";
if (Directory.Exists(debDir))
{
    foreach (var deb in Directory.GetFiles(debDir, "*.deb"))
    {
        var destFile = Path.Combine(archiveDir, Path.GetFileName(deb));
        File.Copy(deb, destFile, overwrite: true);
    }
}

// Update package cache
var updateProc = new ProcessStartInfo("apt-get", "update") { UseShellExecute = false };
var updateProcess = Process.Start(updateProc);
updateProcess.WaitForExit();
if (updateProcess.ExitCode != 0)
{
    throw new InvalidOperationException("apt-get update failed");
}

// Install docker packages without downloading (we pre-cached them)
var installArgs = "install -y --no-download docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin";
var installProc = new ProcessStartInfo("apt-get", installArgs) { UseShellExecute = false };
var installProcess = Process.Start(installProc);
installProcess.WaitForExit();
if (installProcess.ExitCode != 0)
{
    throw new InvalidOperationException("apt-get install failed");
}

// Enable services
foreach (var service in new[] { "docker", "containerd" })
{
    var enableProc = new ProcessStartInfo("systemctl", $"enable {service}") { UseShellExecute = false };
    var enableProcess = Process.Start(enableProc);
    enableProcess.WaitForExit();
    if (enableProcess.ExitCode != 0)
    {
        throw new InvalidOperationException($"Failed to enable {service}");
    }
}

Console.WriteLine("docker installed successfully and services enabled");
