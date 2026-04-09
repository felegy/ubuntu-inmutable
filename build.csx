#r "nuget: Bullseye, 3.8.0"
#r "nuget: SimpleExec, 12.0.0"

using System.Diagnostics;
using Bullseye;
using SimpleExec;
using static Bullseye.Targets;

// Parse the command-line once and keep a mutable copy so option helpers
// can consume flags before Bullseye receives the remaining target args.
var cliArgs = Args.ToList();
var push = ReadBoolOption(cliArgs, "--push", defaultValue: string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase));
var imageName = ReadStringOption(cliArgs, "--image", defaultValue: ResolveDefaultImageName());
var gitSha = ResolveGitSha();
var dotnetCommand = ResolveDotnetCommand();

// Build context holds the values reused by multiple targets so tag generation,
// timestamps, and image naming stay consistent across the run.
var context = new BuildContext(
    imageName,
    push,
    gitSha,
    DateTimeOffset.UtcNow.ToString("O"));

// Print the resolved execution context for local debugging and CI logs.
Target("print-context", () =>
{
    Console.WriteLine($"Image: {context.ImageName}");
    Console.WriteLine($"Push: {context.Push}");
    Console.WriteLine($"SHA: {context.GitSha}");
    Console.WriteLine($"Tags: {string.Join(",", context.ComputeTags())}");
});

// Restore local tools from the repository tool manifest so dotnet-script and
// any future tools are available in a deterministic way.
Target("restore", () =>
{
    Command.Run(dotnetCommand, "tool restore");
});

// Fail fast if the container toolchain is missing before longer-running steps.
Target("verify-tools", () =>
{
    EnsureTool("docker", "Docker CLI is required.");
    Command.Run("docker", "buildx version");
});

// Build the image once with all requested tags and supply-chain metadata.
Target("container-build", ["restore", "verify-tools", "print-context"], () =>
{
    var tags = context.ComputeTags();
    var tagArguments = string.Join(" ", tags.Select(tag => $"--tag {context.ImageName}:{tag}"));
    var outputMode = context.Push ? "--push" : "--load";

    // The build command keeps SBOM and provenance enabled so local and CI
    // builds follow the same artifact policy.
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

// Scan every produced tag with Trivy and block CI on high-severity findings.
Target("scan", ["container-build"], () =>
{
    if (!ToolExists("trivy"))
    {
        // CI must fail if the scanner is unavailable; local runs can skip it.
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

// Publish is represented by the buildx --push mode; keep this target so the
// graph stays expressive even when non-push runs become a no-op here.
Target("publish", ["scan"], () =>
{
    if (!context.Push)
    {
        Console.WriteLine("Publish step is a no-op when --push false.");
    }
});

// CI is the public entrypoint used by workflows and local launcher scripts.
Target("ci", ["publish"]);
Target("default", ["ci"]);

await RunTargetsAndExitAsync(cliArgs.ToArray());

// Read a bool option while removing the consumed tokens from the mutable arg list.
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

// Read a string option while preserving Bullseye target args that remain.
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

// Prefer CI-provided repository metadata and fall back to a predictable local name.
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

// Prefer CI commit metadata and fall back to git for local runs.
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

// Launcher scripts can force a project-local dotnet binary via DOTNET_EXE.
static string ResolveDotnetCommand()
{
    var envValue = Environment.GetEnvironmentVariable("DOTNET_EXE");
    if (!string.IsNullOrWhiteSpace(envValue))
    {
        return envValue;
    }

    return "dotnet";
}

// Use a platform-appropriate lookup command so this helper works in scripts and CI.
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

// Centralize the failure message when a required external tool is missing.
static void EnsureTool(string tool, string errorMessage)
{
    if (!ToolExists(tool))
    {
        throw new InvalidOperationException(errorMessage);
    }
}

// CI-specific behavior is controlled by the conventional CI environment variable.
static bool IsCi()
{
    var value = Environment.GetEnvironmentVariable("CI");
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

// This immutable context keeps tag and metadata logic in one place.
sealed class BuildContext(string imageName, bool push, string gitSha, string createdIso)
{
    public string ImageName { get; } = imageName;
    public bool Push { get; } = push;
    public string GitSha { get; } = gitSha;
    public string CreatedIso { get; } = createdIso;

    // Short SHA is used for human-readable image tags.
    public string ShortSha => GitSha[..Math.Min(7, GitSha.Length)];

    // Local runs produce a single dev tag; CI push runs publish stable tags.
    public IReadOnlyList<string> ComputeTags()
    {
        if (!Push)
        {
            return ["dev"];
        }

        return ["latest", $"sha-{ShortSha}"];
    }
}
