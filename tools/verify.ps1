param(
    [switch]$SkipGodot,
    [string]$GodotConsolePath = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$slnPath = Join-Path $repoRoot "RmClient.sln"
$appPath = Join-Path $repoRoot "src\RmClient.App"

function Find-ConsoleInDirectory([string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Directory) -or -not (Test-Path $Directory -PathType Container)) {
        return $null
    }
    $mono = Get-ChildItem $Directory -Recurse -File -Filter "*mono*_console.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($mono) { return $mono }

    $any = Get-ChildItem $Directory -Recurse -File -Filter "*_console.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    return $any
}

function Resolve-GodotConsole([string]$ExplicitPath) {
    # 1. Explicit parameter (file or directory)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path $ExplicitPath -PathType Leaf) {
            return (Resolve-Path $ExplicitPath).Path
        }
        $fromDir = Find-ConsoleInDirectory $ExplicitPath
        if ($fromDir) { return $fromDir }
        throw "Godot console executable not found at explicit path: $ExplicitPath"
    }

    # 2. GODOT_PATH environment variable (file's parent dir or directory)
    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_PATH)) {
        if (Test-Path $env:GODOT_PATH -PathType Leaf) {
            $parent = Split-Path $env:GODOT_PATH -Parent
            $fromParent = Find-ConsoleInDirectory $parent
            if ($fromParent) { return $fromParent }
            if ($env:GODOT_PATH -like "*_console.exe") { return $env:GODOT_PATH }
        }
        else {
            $fromEnvDir = Find-ConsoleInDirectory $env:GODOT_PATH
            if ($fromEnvDir) { return $fromEnvDir }
        }
    }

    # 3. Common installation directories (WinGet Mono package, CI cache, Program Files)
    $searchRoots = @()
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $wingetPackages = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
        if (Test-Path $wingetPackages) {
            $searchRoots += Get-ChildItem $wingetPackages -Directory -Filter "GodotEngine.GodotEngine.Mono*" -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName
        }
    }
    $searchRoots += "C:\godot-4.7.2-mono"
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $searchRoots += Get-ChildItem $env:ProgramFiles -Directory -Filter "Godot*" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }

    foreach ($root in $searchRoots) {
        $found = Find-ConsoleInDirectory $root
        if ($found) { return $found }
    }

    # 4. Fallback to PATH (warn if WinGet Links shim)
    $cmd = Get-Command "godot_console" -ErrorAction SilentlyContinue
    if ($cmd -and (Test-Path $cmd.Source)) {
        if ($cmd.Source -like "*\WinGet\Links\*") {
            Write-Warning "Using WinGet shim ($($cmd.Source)); if headless .NET hangs, pass -GodotConsolePath to the real *_console.exe."
        }
        return $cmd.Source
    }

    throw "Godot 4.7.2 .NET console binary not found. Pass -GodotConsolePath, set GODOT_PATH, or install Godot .NET."
}

Write-Host "=== 1. dotnet build ==="
dotnet build $slnPath --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

Write-Host "=== 2. dotnet test ==="
dotnet test $slnPath --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed (exit $LASTEXITCODE)" }

if ($SkipGodot) {
    Write-Host "Skipping Godot headless checks (-SkipGodot)."
    exit 0
}

$resolvedGodot = Resolve-GodotConsole $GodotConsolePath
Write-Host "Using Godot console: $resolvedGodot"

Write-Host "=== 3. Godot --headless --import ==="
& $resolvedGodot --headless --path $appPath --import
if ($LASTEXITCODE -ne 0) { throw "Godot import failed (exit $LASTEXITCODE)" }

Write-Host "=== 4. Godot --headless C# --self-test ==="
& $resolvedGodot --headless --path $appPath -- --static-demo --self-test
if ($LASTEXITCODE -ne 0) { throw "Godot self-test failed (exit $LASTEXITCODE)" }

Write-Host "All verification checks passed."
