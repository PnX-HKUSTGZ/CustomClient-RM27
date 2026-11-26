param(
    [switch]$SkipGodot,
    [string]$GodotConsolePath = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$slnPath = Join-Path $repoRoot "RmClient.sln"
$appPath = Join-Path $repoRoot "src\RmClient.App"

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

if ([string]::IsNullOrWhiteSpace($GodotConsolePath)) {
    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_PATH)) {
        $candidate = Join-Path (Split-Path $env:GODOT_PATH -Parent) "Godot_v4.7.2-stable_mono_win64_console.exe"
        if (Test-Path $candidate) {
            $GodotConsolePath = $candidate
        }
    }
}

if ([string]::IsNullOrWhiteSpace($GodotConsolePath) -or -not (Test-Path $GodotConsolePath)) {
    throw "Godot 4.7.2 .NET console binary not found. Pass -GodotConsolePath or set GODOT_PATH."
}

Write-Host "=== 3. Godot --headless --import ==="
& $GodotConsolePath --headless --path $appPath --import
if ($LASTEXITCODE -ne 0) { throw "Godot import failed (exit $LASTEXITCODE)" }

Write-Host "=== 4. Godot --headless C# --self-test ==="
& $GodotConsolePath --headless --path $appPath -- --static-demo --self-test
if ($LASTEXITCODE -ne 0) { throw "Godot self-test failed (exit $LASTEXITCODE)" }

Write-Host "All verification checks passed."
