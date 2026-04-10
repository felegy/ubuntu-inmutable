#r "nuget: Bullseye, 3.8.0"
#r "nuget: SimpleExec, 12.0.0"
#r "nuget: System.CommandLine, 2.0.5"

#nullable enable

using System.Diagnostics;
using Bullseye;
using SimpleExec;
using SC = System.CommandLine;
using static Bullseye.Targets;

// Define typed command-line options using System.CommandLine for validation and error handling.
var pushOption = new SC.Option<bool?>("--push")
{
    Description = "Push image to registry"
};

var imageOption = new SC.Option<string?>("--image")
{
    Description = "Image name (default: ghcr.io/{repo})"
};

var baseImageOption = new SC.Option<string?>("--base-image")
{
    Description = "Base OS image (default: ubuntu:24.04)"
};

var kairosInitVersionOption = new SC.Option<string?>("--kairos-init-version")
{
    Description = "kairos-init version tag (default: 0.7.0)"
};

var defaultUserOption = new SC.Option<string?>("--default-user")
{
    Description = "Default OS user name (default: kairos)"
};

var variantOption = new SC.Option<string?>("--variant")
{
    Description = "Kairos variant: core or standard (default: core)"
};

var modelOption = new SC.Option<string?>("--model")
{
    Description = "Kairos model: generic or specific hw target (default: generic)"
};

var trustedBootOption = new SC.Option<bool?>("--trusted-boot")
{
    Description = "Enable trusted boot (default: false)"
};

var versionOption = new SC.Option<string?>("--image-version")
{
    Description = "Kairos OS version string (required for container-build)"
};

var kubernetesDistroOption = new SC.Option<string?>("--kubernetes-distro")
{
    Description = "Kubernetes provider: k3s or k0s (optional)"
};

var kubernetesVersionOption = new SC.Option<string?>("--kubernetes-version")
{
    Description = "Kubernetes version string (optional, defaults to --image-version in Containerfile)"
};

var debsOption = new SC.Option<string?>("--debs")
{
    Description = "Space-separated extra APT packages (default: open-vm-tools)"
};

var root = new SC.RootCommand("Bullseye build orchestrator for ubuntu-inmutable");
root.Options.Add(pushOption);
root.Options.Add(imageOption);
root.Options.Add(baseImageOption);
root.Options.Add(kairosInitVersionOption);
root.Options.Add(defaultUserOption);
root.Options.Add(variantOption);
root.Options.Add(modelOption);
root.Options.Add(trustedBootOption);
root.Options.Add(versionOption);
root.Options.Add(kubernetesDistroOption);
root.Options.Add(kubernetesVersionOption);
root.Options.Add(debsOption);
root.TreatUnmatchedTokensAsErrors = false;

// Parse arguments; unmatched tokens (targets, Bullseye flags) pass through to Bullseye.
var parseResult = root.Parse(Args.ToArray());

// Extract typed option values with appropriate fallbacks.
var push = parseResult.GetValue(pushOption) ?? IsCi();
var imageName = parseResult.GetValue(imageOption) ?? ResolveDefaultImageName();
var baseImage = ResolveWithEnvFallback(parseResult.GetValue(baseImageOption), "BASE_IMAGE", "ubuntu:24.04");
var kairosInitVersion = ResolveWithEnvFallback(parseResult.GetValue(kairosInitVersionOption), "KAIROS_INIT_VERSION", "0.7.0");
var defaultUser = ResolveWithEnvFallback(parseResult.GetValue(defaultUserOption), "KAIROS_DEFAULT_USER", "kairos");
var variant = ResolveWithEnvFallback(parseResult.GetValue(variantOption), "KAIROS_VARIANT", "core");
var model = ResolveWithEnvFallback(parseResult.GetValue(modelOption), "KAIROS_MODEL", "generic");
var trustedBoot = ResolveBoolWithEnvFallback(parseResult.GetValue(trustedBootOption), "KAIROS_TRUSTED_BOOT", false);
var version = ResolveWithEnvFallback(parseResult.GetValue(versionOption), "KAIROS_VERSION", "");
var kubernetesDistro = ResolveWithEnvFallback(parseResult.GetValue(kubernetesDistroOption), "KUBERNETES_DISTRO", "");
var kubernetesVersion = ResolveWithEnvFallback(parseResult.GetValue(kubernetesVersionOption), "KUBERNETES_VERSION", "");
var debs = ResolveWithEnvFallback(parseResult.GetValue(debsOption), "KAIROS_DEBS", "open-vm-tools");
var imageExtraTag = ResolveWithEnvFallback(null, "IMAGE_EXTRA_TAG", "");
var gitSha = ResolveGitSha();
var dotnetCommand = ResolveDotnetCommand();

// Build context holds the values reused by multiple targets so tag generation,
// timestamps, and image naming stay consistent across the run.
var context = new BuildContext(
    imageName,
    push,
    gitSha,
    DateTimeOffset.UtcNow.ToString("O"),
    baseImage,
    kairosInitVersion,
    defaultUser,
    variant,
    model,
    trustedBoot,
    version,
    kubernetesDistro,
    kubernetesVersion,
    debs,
    imageExtraTag);

// Print the resolved execution context for local debugging and CI logs.
Target("print-context", () =>
{
    Console.WriteLine($"Image: {context.ImageName}");
    Console.WriteLine($"Push: {context.Push}");
    Console.WriteLine($"SHA: {context.GitSha}");
    Console.WriteLine($"Tags: {string.Join(",", context.ComputeTags())}");
    Console.WriteLine($"BaseImage: {context.BaseImage}");
    Console.WriteLine($"KairosInitVersion: {context.KairosInitVersion}");
    Console.WriteLine($"DefaultUser: {context.DefaultUser}");
    Console.WriteLine($"Variant: {context.Variant}");
    Console.WriteLine($"Model: {context.Model}");
    Console.WriteLine($"TrustedBoot: {context.TrustedBoot}");
    Console.WriteLine($"Version: {(string.IsNullOrEmpty(context.Version) ? "(not set)" : context.Version)}");
    Console.WriteLine($"KubernetesDistro: {(string.IsNullOrEmpty(context.KubernetesDistro) ? "(none)" : context.KubernetesDistro)}");
    Console.WriteLine($"KubernetesVersion: {(string.IsNullOrEmpty(context.KubernetesVersion) ? "(none)" : context.KubernetesVersion)}");
    Console.WriteLine($"Debs: {context.Debs}");
    Console.WriteLine($"ImageExtraTag: {(string.IsNullOrEmpty(context.ImageExtraTag) ? "(none)" : context.ImageExtraTag)}");

    if (string.IsNullOrEmpty(context.Version))
    {
        if (IsCi())
            throw new InvalidOperationException("--version / KAIROS_VERSION is required in CI.");
        Console.WriteLine("Warning: VERSION is empty - kairos-init will fail during container-build.");
    }
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
    var buildArgParts = new List<string>
    {
        "buildx build",
        "--file Containerfile",
        "--provenance=true",
        "--sbom=true",
        $"--build-arg BASE_IMAGE={context.BaseImage}",
        $"--build-arg KAIROS_INIT_VERSION={context.KairosInitVersion}",
        $"--build-arg DEFAULT_USER={context.DefaultUser}",
        $"--build-arg VARIANT={context.Variant}",
        $"--build-arg MODEL={context.Model}",
        $"--build-arg TRUSTED_BOOT={context.TrustedBoot.ToString().ToLowerInvariant()}",
        $"--build-arg VERSION={context.Version}",
        $"--build-arg DEBS={context.Debs}",
        $"--build-arg IMAGE_CREATED={context.CreatedIso}",
        $"--build-arg IMAGE_REVISION={context.GitSha}",
    };

    // Only append optional Kubernetes args when explicitly set — the Containerfile defaults handle the empty case.
    if (!string.IsNullOrEmpty(context.KubernetesDistro))
        buildArgParts.Add($"--build-arg KUBERNETES_DISTRO={context.KubernetesDistro}");
    if (!string.IsNullOrEmpty(context.KubernetesVersion))
        buildArgParts.Add($"--build-arg KUBERNETES_VERSION={context.KubernetesVersion}");

    buildArgParts.Add(tagArguments);
    buildArgParts.Add(outputMode);
    buildArgParts.Add(".");

    Command.Run("docker", string.Join(" ", buildArgParts));
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
        Command.Run("trivy", $"image --severity HIGH,CRITICAL --exit-code 1 --ignore-unfixed --ignorefile .trivyignore {context.ImageName}:{tag}");
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

// Pass unmatched tokens (target names, Bullseye flags) to Bullseye's orchestrator.
await RunTargetsAndExitAsync(parseResult.UnmatchedTokens.ToArray());

// Prefer CI-provided repository metadata and fall back to a predictable local name.
static string ResolveDefaultImageName()
{
    var explicitImageName = Environment.GetEnvironmentVariable("IMAGE_NAME");
    if (!string.IsNullOrWhiteSpace(explicitImageName))
    {
        return explicitImageName;
    }

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

// Resolve a string value from CLI option, then environment variable, then hard-coded default.
static string ResolveWithEnvFallback(string? cliValue, string envVar, string hardDefault)
{
    if (!string.IsNullOrEmpty(cliValue)) return cliValue;
    var envValue = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(envValue)) return envValue;
    return hardDefault;
}

// Resolve a bool value from CLI option, then environment variable, then hard-coded default.
static bool ResolveBoolWithEnvFallback(bool? cliValue, string envVar, bool hardDefault)
{
    if (cliValue.HasValue) return cliValue.Value;
    var envValue = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(envValue))
        return envValue.Equals("true", StringComparison.OrdinalIgnoreCase) || envValue == "1";
    return hardDefault;
}

// This immutable context keeps tag and metadata logic in one place.
sealed class BuildContext(
    string imageName,
    bool push,
    string gitSha,
    string createdIso,
    string baseImage,
    string kairosInitVersion,
    string defaultUser,
    string variant,
    string model,
    bool trustedBoot,
    string version,
    string kubernetesDistro,
    string kubernetesVersion,
    string debs,
    string imageExtraTag)
{
    public string ImageName { get; } = imageName;
    public bool Push { get; } = push;
    public string GitSha { get; } = gitSha;
    public string CreatedIso { get; } = createdIso;
    public string BaseImage { get; } = baseImage;
    public string KairosInitVersion { get; } = kairosInitVersion;
    public string DefaultUser { get; } = defaultUser;
    public string Variant { get; } = variant;
    public string Model { get; } = model;
    public bool TrustedBoot { get; } = trustedBoot;
    public string Version { get; } = version;
    public string KubernetesDistro { get; } = kubernetesDistro;
    public string KubernetesVersion { get; } = kubernetesVersion;
    public string Debs { get; } = debs;
    public string ImageExtraTag { get; } = imageExtraTag;

    // Short SHA is used for human-readable image tags.
    public string ShortSha => GitSha[..Math.Min(7, GitSha.Length)];

    // Local runs produce a single dev tag; CI push runs publish stable tags.
    public IReadOnlyList<string> ComputeTags()
    {
        if (!Push)
        {
            return ["dev"];
        }

        var tags = new List<string> { "latest", $"sha-{ShortSha}" };
        if (!string.IsNullOrWhiteSpace(ImageExtraTag))
        {
            tags.Add(ImageExtraTag);
        }

        return tags;
    }
}
