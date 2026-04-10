#!/usr/bin/env dotnet-script

using System.Diagnostics;
using System.IO;

var bin = "/usr/local/bin";
Directory.CreateDirectory(bin);

var src = "helm";
var dst = Path.Combine(bin, "helm");
File.Copy(src, dst, overwrite: true);
File.SetAttributes(dst, File.GetAttributes(dst) | FileAttributes.Other);

Console.WriteLine("helm installed successfully in /usr/local/bin");
