#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var bin = "/usr/local/bin";
Directory.CreateDirectory(bin);

var src = "k9s";
var dst = Path.Combine(bin, "k9s");
File.Copy(src, dst, overwrite: true);
File.SetAttributes(dst, File.GetAttributes(dst) | FileAttributes.Other);

Console.WriteLine("k9s installed successfully in /usr/local/bin");
