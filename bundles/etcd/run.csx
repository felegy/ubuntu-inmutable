#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var bin = "/usr/local/bin";
Directory.CreateDirectory(bin);

foreach (var binary in new[] { "etcd", "etcdctl" })
{
    var src = binary;
    var dst = Path.Combine(bin, binary);
    File.Copy(src, dst, overwrite: true);
    var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x {dst}") { UseShellExecute = false });
    chmod?.WaitForExit();
    if (chmod is null || chmod.ExitCode != 0)
    {
        throw new InvalidOperationException($"Failed to set executable bit on {binary} binary");
    }
}

Console.WriteLine("etcd and etcdctl installed successfully in /usr/local/bin");
