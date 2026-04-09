# Plan: System.CommandLine Argument Parsing in build.csx

Migrate the manual `ReadBoolOption`/`ReadStringOption` helpers in `build.csx` to typed `System.CommandLine` options, while preserving Bullseye's own argument handling for target names and its built-in flags (`--dry-run`, `--parallel`, etc.).

## Rationale

The current arg helpers are stringly-typed and do not validate the format. `System.CommandLine 2.0.5` offers:
- Type-safe `Option<bool>` and `Option<string>` definitions
- Automatic validation and error messages
- Cleaner separation of concerns between custom flags and Bullseye's built-in flags

## Implementation

### Add NuGet Reference

Add to the top of `build.csx` (after the Bullseye and SimpleExec refs):

```csharp
#r "nuget: System.CommandLine, 2.0.5"
using SC = System.CommandLine;
```

The alias `SC` avoids naming conflict with `SimpleExec.Command` and `System.CommandLine.Command`.

### Define Options

Replace the mutable `cliArgs` block with structured option definitions and parsing:

```csharp
var pushOption = new SC.Option<bool>("--push")
{
    Description = "Push image to registry",
    DefaultValueFactory = _ => IsCi()
};

var imageOption = new SC.Option<string>("--image")
{
    Description = "Image name (default: ghcr.io/{repo})",
    DefaultValueFactory = _ => ResolveDefaultImageName()
};

var root = new SC.RootCommand("Bullseye build orchestrator for ubuntu-inmutable");
root.Options.Add(pushOption);
root.Options.Add(imageOption);
root.TreatUnmatchedTokensAsErrors = false;
```

**Important:** Do NOT add SC's default `--help` or `--version` to `root.Options` — let Bullseye handle `--help` so it shows the target list instead of SC's generic help.

### Extract Typed Values and Pass Unmatched to Bullseye

After parsing, extract typed values and pass `UnmatchedTokens` to Bullseye:

```csharp
var parseResult = root.Parse(Args.ToArray());
var push = parseResult.GetValue(pushOption) ?? false;
var imageName = parseResult.GetValue(imageOption)!;
var gitSha = ResolveGitSha();
var dotnetCommand = ResolveDotnetCommand();

// ... build context as is ...

await RunTargetsAndExitAsync(parseResult.UnmatchedTokens.ToArray());
```

### Delete Old Helpers

Remove the two static helper methods:
- `ReadBoolOption`
- `ReadStringOption`

They are no longer used.

## Verification

After implementation:

1. `dotnet script build.csx -- print-context` works with defaults (`--push` inferred from CI env, `--image` resolved to default)
2. `dotnet script build.csx -- print-context --push true --image ghcr.io/test/repo` uses typed values
3. `dotnet script build.csx -- --dry-run ci` passes `--dry-run` to Bullseye without error
4. `dotnet script build.csx -- --help` shows Bullseye's target list (not SC's generic help)
5. `./build.sh print-context` works end-to-end via the launcher

## End State

- Argument parsing is type-safe and validated by SC's parser
- Bullseye flags (`--dry-run`, `--parallel`, `--help`) flow through unmatched and reach Bullseye unchanged
- Custom flags (`--push`, `--image`) are consumed by the SC parser and extracted before Bullseye runs
- The behavior of the build script is identical to the manual helper-based implementation

## Files Modified

- `build.csx` — migrate to SC, delete helper methods
