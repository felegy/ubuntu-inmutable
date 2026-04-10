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
    File.SetAttributes(dst, File.GetAttributes(dst) | FileAttributes.Other);
}

Console.WriteLine("etcd and etcdctl installed successfully in /usr/local/bin");
