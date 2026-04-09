#r "nuget: Bullseye, 3.8.0"
#r "nuget: SimpleExec, 12.0.0"

using System.Diagnostics;
using Bullseye;
using SimpleExec;
using static Bullseye.Targets;

var cliArgs = Args.ToList();
var push = ReadBoolOption(cliArgs, "--push", defaultValue: string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase));
var imageName = ReadStringOption(cliArgs, "--image", defaultValue: ResolveDefaultImageName());
var gitSha = ResolveGitSha();
var dotnetCommand = ResolveDotnetCommand();

var context = new BuildContext(
    imageName,
    push,
    gitSha,
    DateTimeOffset.UtcNow.ToString("O"));

Target("print-context", () =>
{
    Console.WriteLine($"Image: {context.ImageName}");
    Console.WriteLine($"Push: {context.Push}");
    Console.WriteLine($"SHA: {context.GitSha}");
    Console.WriteLine($"Tags: {string.Join(",", context.ComputeTags())}");
});

Target("restore", () =>
{
    Command.Run(dotnetCommand, "tool restore");
});

Target("verify-tools", () =>
{
    EnsureTool("docker", "Docker CLI is required.");
    Command.Run("docker", "buildx version");
});

Target("container-build", ["restore", "verify-tools", "print-context"], () =>
{
    var tags = context.ComputeTags();
    var tagArguments = string.Join(" ", tags.Select(tag => $"--tag {context.ImageName}:{tag}"));
    var outputMode = context.Push ? "--push" : "--load";

    var buildArgs = string.Join(" ",
        "buildx build",
        "--file Containerfile",
        "--provenance=true",
        "--sbom=true",
        $"--build-arg IMAGE_CREATED={context.CreatedIso}",
        $"--build-arg IMAGE_REVISION={context.GitSha}",
        tagArguments,
        outputMode,
        ".");

    Command.Run("docker", buildArgs);
});

Target("scan", ["container-build"], () =>
{
    if (!ToolExists("trivy"))
    {
        if (IsCi())
        {
            throw new InvalidOperationException("trivy is required in CI for security scanning.");
        }

        Console.WriteLine("Skipping scan: trivy not found.");
        return;
    }

    foreach (var tag in context.ComputeTags())
    {
        Command.Run("trivy", $"image --severity HIGH,CRITICAL --exit-code 1 --ignore-unfixed {context.ImageName}:{tag}");
    }
});

Target("publish", ["scan"], () =>
{
    if (!context.Push)
    {
        Console.WriteLine("Publish step is a no-op when --push false.");
    }
});

Target("ci", ["publish"]);
Target("default", ["ci"]);

await RunTargetsAndExitAsync(cliArgs.ToArray());

static bool ReadBoolOption(List<string> cliArgs, string optionName, bool defaultValue)
{
    var index = cliArgs.IndexOf(optionName);
    if (index < 0)
    {
        return defaultValue;
    }

    if (index == cliArgs.Count - 1 || cliArgs[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        cliArgs.RemoveAt(index);
        return true;
    }

    var value = cliArgs[index + 1];
    cliArgs.RemoveAt(index + 1);
    cliArgs.RemoveAt(index);
    return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
}

static string ReadStringOption(List<string> cliArgs, string optionName, string defaultValue)
{
    var index = cliArgs.IndexOf(optionName);
    if (index < 0)
    {
        return defaultValue;
    }

    if (index == cliArgs.Count - 1)
    {
        throw new ArgumentException($"Missing value for {optionName}");
    }

    var value = cliArgs[index + 1];
    cliArgs.RemoveAt(index + 1);
    cliArgs.RemoveAt(index);
    return value;
}

static string ResolveDefaultImageName()
{
    var repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
        ?? Environment.GetEnvironmentVariable("GITEA_REPOSITORY")
        ?? "local/ubuntu-inmutable";

    if (repo.Contains('/'))
    {
        return $"ghcr.io/{repo.ToLowerInvariant()}";
    }

    return "ghcr.io/owner/ubuntu-inmutable";
}

static string ResolveGitSha()
{
    var envSha = Environment.GetEnvironmentVariable("GITHUB_SHA")
        ?? Environment.GetEnvironmentVariable("GITEA_SHA")
        ?? Environment.GetEnvironmentVariable("CI_COMMIT_SHA");

    if (!string.IsNullOrWhiteSpace(envSha))
    {
        return envSha;
    }

    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse HEAD",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return "unknown";
        }

        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return "unknown";
        }

        return stdout.Trim();
    }
    catch
    {
        return "unknown";
    }
}

static string ResolveDotnetCommand()
{
    var envValue = Environment.GetEnvironmentVariable("DOTNET_EXE");
    if (!string.IsNullOrWhiteSpace(envValue))
    {
        return envValue;
    }

    return "dotnet";
}

static bool ToolExists(string tool)
{
    var probeTool = OperatingSystem.IsWindows() ? "where" : "which";

    try
    {
        Command.Run(probeTool, tool, noEcho: true);
        return true;
    }
    catch
    {
        return false;
    }
}

static void EnsureTool(string tool, string errorMessage)
{
    if (!ToolExists(tool))
    {
        throw new InvalidOperationException(errorMessage);
    }
}

static bool IsCi()
{
    var value = Environment.GetEnvironmentVariable("CI");
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

sealed class BuildContext(string imageName, bool push, string gitSha, string createdIso)
{
    public string ImageName { get; } = imageName;
    public bool Push { get; } = push;
    public string GitSha { get; } = gitSha;
    public string CreatedIso { get; } = createdIso;

    public string ShortSha => GitSha[..Math.Min(7, GitSha.Length)];

    public IReadOnlyList<string> ComputeTags()
    {
        if (!Push)
        {
            return ["dev"];
        }

        return ["latest", $"sha-{ShortSha}"];
    }
}
