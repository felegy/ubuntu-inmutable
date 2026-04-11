#r "nuget: Bullseye, 3.8.0"
#r "nuget: SimpleExec, 12.0.0"
#r "nuget: System.CommandLine, 2.0.5"

#nullable enable

using System.Diagnostics;
using Bullseye;
using SimpleExec;
using SC = System.CommandLine;
using static Bullseye.Targets;

// Define command-line options using System.CommandLine for validation and error handling.
var bundleOption = new SC.Option<string?>("--bundle")
{
    Description = "Bundle name to build (default: all)"
};

var pushOption = new SC.Option<bool?>("--push")
{
    Description = "Push images to registry"
};

var dockerVersionOption = new SC.Option<string?>("--docker-version")
{
    Description = "Docker major version for docker bundle: latest|<major> (default: latest)"
};

var verbosityOption = new SC.Option<string?>("--verbosity")
{
    Description = "Output verbosity: quiet|minimal|normal|detailed|diagnostic (default: normal)"
};

var root = new SC.RootCommand("Bundle build orchestrator for ubuntu-inmutable");
root.Options.Add(bundleOption);
root.Options.Add(pushOption);
root.Options.Add(dockerVersionOption);
root.Options.Add(verbosityOption);
root.TreatUnmatchedTokensAsErrors = false;

// Parse arguments
var parseResult = root.Parse(Args.ToArray());

// Extract typed option values with fallbacks
var bundleName = parseResult.GetValue(bundleOption);
var push = parseResult.GetValue(pushOption) ?? IsCi();
var dockerVersion = ResolveDockerVersion(parseResult.GetValue(dockerVersionOption));
var verbosity = ResolveVerbosity(parseResult.GetValue(verbosityOption));

// Resolve image name prefix
var imageName = ResolveImagePrefix();
var gitSha = ResolveGitSha();

// Discover bundles (look for Containerfile in subdirectories)
var bundlesDir = Path.Combine(Environment.CurrentDirectory, "bundles");
var allBundles = Directory.GetDirectories(bundlesDir)
    .Where(dir => File.Exists(Path.Combine(dir, "Containerfile")))
    .Select(dir => Path.GetFileName(dir))
    .OrderBy(name => name)
    .ToList();

// Filter bundles if --bundle specified
var bundlesToBuild = string.IsNullOrWhiteSpace(bundleName) || bundleName == "all"
    ? allBundles
    : allBundles.Where(b => b.Equals(bundleName, StringComparison.OrdinalIgnoreCase)).ToList();

// Print build context
Target("print-context", () =>
{
    if (verbosity == BuildVerbosity.Quiet)
        return;

    Console.WriteLine($"Image Prefix: {imageName}");
    Console.WriteLine($"Push: {push}");
    Console.WriteLine($"DockerVersion: {dockerVersion}");
    Console.WriteLine($"SHA: {gitSha}");
    Console.WriteLine($"Verbosity: {ToDisplayString(verbosity)}");
    Console.WriteLine($"Bundles to build: {string.Join(", ", bundlesToBuild)}");
    Console.WriteLine($"Total bundles: {bundlesToBuild.Count}");
});

// Build all/selected bundles
Target("bundle-build", ["print-context"], () =>
{
    foreach (var bundle in bundlesToBuild)
    {
        BuildBundle(bundle);
    }
});

// Publish targets (no-op if not pushing)
Target("bundle-publish", ["bundle-build"], () =>
{
    if (!push)
    {
        Console.WriteLine("Publish step is a no-op when --push false.");
    }
});

// CI entrypoint
Target("bundle-ci", ["bundle-publish"]);
Target("default", ["bundle-ci"]);

// Pass unmatched tokens to Bullseye
await RunTargetsAndExitAsync(parseResult.UnmatchedTokens.ToArray());

// Build a single bundle
void BuildBundle(string bundleName)
{
    var bundleDir = Path.Combine(bundlesDir, bundleName);
    var tags = ComputeTagsForBundle(bundleName);
    var tagArguments = string.Join(" ", tags.Select(tag => $"--tag {imageName}:{tag}"));
    var outputMode = push ? "--push" : "--load";
    var provenanceMode = push ? "true" : "false";
    var sbomMode = push ? "true" : "false";
    var progressMode = GetProgressMode(verbosity);

    var buildArgParts = new List<string>
    {
        "buildx build",
        "--file Containerfile",
        $"--progress={progressMode}",
        $"--provenance={provenanceMode}",
        $"--sbom={sbomMode}",
        tagArguments,
        outputMode,
        "."
    };

    if (bundleName.Equals("docker", StringComparison.OrdinalIgnoreCase))
    {
        buildArgParts.Insert(6, $"--build-arg DOCKER_VERSION={dockerVersion}");
    }

    var buildCommand = string.Join(" ", buildArgParts);
    var currentDir = Environment.CurrentDirectory;
    try
    {
        Environment.CurrentDirectory = bundleDir;
        Command.Run("docker", buildCommand, noEcho: verbosity == BuildVerbosity.Quiet);
    }
    finally
    {
        Environment.CurrentDirectory = currentDir;
    }
}

// Compute tags for a bundle
IReadOnlyList<string> ComputeTagsForBundle(string bundleName)
{
    if (!push)
    {
        return [$"{bundleName}-dev"];
    }

    var tags = new List<string>
    {
        $"{bundleName}-latest",
        $"{bundleName}-sha-{ShortSha}"
    };

    return tags;
}

// Get short SHA for tagging
string ShortSha => gitSha[..Math.Min(7, gitSha.Length)];

// Determine docker buildx progress mode
string GetProgressMode(BuildVerbosity v) => v is BuildVerbosity.Detailed or BuildVerbosity.Diagnostic ? "plain" : "auto";

// Resolve image prefix from environment
string ResolveImagePrefix()
{
    var imageName = Environment.GetEnvironmentVariable("IMAGE_NAME");
    if (!string.IsNullOrWhiteSpace(imageName))
    {
        return $"{imageName}-bundles";
    }

    var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
    if (!string.IsNullOrWhiteSpace(githubRepo) && githubRepo.Contains('/'))
    {
        return $"ghcr.io/{githubRepo.ToLowerInvariant()}-bundles";
    }

    var giteaRepo = Environment.GetEnvironmentVariable("GITEA_REPOSITORY");
    if (!string.IsNullOrWhiteSpace(giteaRepo) && giteaRepo.Contains('/'))
    {
        return $"ghcr.io/{giteaRepo.ToLowerInvariant()}-bundles";
    }

    return "ghcr.io/local/ubuntu-inmutable-bundles";
}

// Resolve Git SHA from CI env or git command
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
            return "unknown";

        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 ? stdout.Trim() : "unknown";
    }
    catch
    {
        return "unknown";
    }
}

// Check if running in CI
static bool IsCi()
{
    var value = Environment.GetEnvironmentVariable("CI");
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

// Resolve docker major version from CLI/env and validate accepted format.
static string ResolveDockerVersion(string? cliValue)
{
    var value = cliValue;
    if (string.IsNullOrWhiteSpace(value))
    {
        value = Environment.GetEnvironmentVariable("DOCKER_VERSION");
    }

    var normalized = string.IsNullOrWhiteSpace(value) ? "latest" : value.Trim().ToLowerInvariant();
    if (normalized == "latest")
    {
        return normalized;
    }

    if (normalized.All(char.IsDigit))
    {
        return normalized;
    }

    throw new InvalidOperationException($"Invalid docker version '{value}'. Allowed values: latest or numeric major (e.g. 27).");
}

// Resolve verbosity from CLI, env, or default
static BuildVerbosity ResolveVerbosity(string? cliValue)
{
    var value = cliValue;
    if (string.IsNullOrWhiteSpace(value))
    {
        value = Environment.GetEnvironmentVariable("BUILD_VERBOSITY");
    }
    if (string.IsNullOrWhiteSpace(value))
    {
        value = Environment.GetEnvironmentVariable("VERBOSITY");
    }

    return BuildVerbosityExtensions.Parse(value);
}

// Verbosity levels
enum BuildVerbosity
{
    Quiet,
    Minimal,
    Normal,
    Detailed,
    Diagnostic
}

static class BuildVerbosityExtensions
{
    public static BuildVerbosity Parse(string? value)
    {
        var normalized = (value ?? "normal").Trim().ToLowerInvariant();
        return normalized switch
        {
            "quiet" => BuildVerbosity.Quiet,
            "minimal" => BuildVerbosity.Minimal,
            "normal" => BuildVerbosity.Normal,
            "detailed" => BuildVerbosity.Detailed,
            "diagnostic" => BuildVerbosity.Diagnostic,
            _ => throw new InvalidOperationException($"Invalid verbosity '{value}'. Allowed values: quiet|minimal|normal|detailed|diagnostic.")
        };
    }

    public static string ToDisplayString(BuildVerbosity verbosity) => verbosity switch
    {
        BuildVerbosity.Quiet => "quiet",
        BuildVerbosity.Minimal => "minimal",
        BuildVerbosity.Normal => "normal",
        BuildVerbosity.Detailed => "detailed",
        BuildVerbosity.Diagnostic => "diagnostic",
        _ => "normal"
    };
}

static string ToDisplayString(BuildVerbosity verbosity) => BuildVerbosityExtensions.ToDisplayString(verbosity);
