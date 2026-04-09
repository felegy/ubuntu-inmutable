Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$DotnetInstallDir = Join-Path $RepoRoot ".dotnet"
$DotnetExe = $null
$UsedLocalDotnet = $false

function Get-DotnetBinaryName {
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return "dotnet.exe"
    }

    return "dotnet"
}

function Ensure-Dotnet {
    $existing = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        return $existing.Source
    }

    $script:UsedLocalDotnet = $true
    New-Item -ItemType Directory -Force -Path $DotnetInstallDir | Out-Null

    $installScript = Join-Path $DotnetInstallDir "dotnet-install.ps1"
    if (-not (Test-Path $installScript)) {
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
    }

    & $installScript -JSonFile (Join-Path $RepoRoot "global.json") -InstallDir $DotnetInstallDir -NoPath
    return (Join-Path $DotnetInstallDir (Get-DotnetBinaryName))
}

$DotnetExe = Ensure-Dotnet

if ($UsedLocalDotnet) {
    $env:DOTNET_ROOT = $DotnetInstallDir
    $env:PATH = "$DotnetInstallDir$([System.IO.Path]::PathSeparator)$($DotnetInstallDir + [System.IO.Path]::DirectorySeparatorChar + 'tools')$([System.IO.Path]::PathSeparator)$env:PATH"
}

$env:DOTNET_EXE = $DotnetExe
& $DotnetExe tool restore
& $DotnetExe script (Join-Path $RepoRoot "build.csx") -- @Arguments
exit $LASTEXITCODE
