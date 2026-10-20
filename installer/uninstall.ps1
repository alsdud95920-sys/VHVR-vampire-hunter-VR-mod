#Requires -Version 5.1
# Vampire Hunters VR - uninstaller.
# Removes only the manifest-owned mod files/roots. The launcher (uninstall.bat) passes -Force by default;
# the installation manifest and strict game-running/root checks still apply.
[CmdletBinding()]
param([string]$GameRoot = '', [switch]$Force)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$here = $PSScriptRoot
$manifestName = 'vhvr-uninstall.json'
$allowedGenerated = @(
    'BepInEx\interop\*',
    'BepInEx\cache\*',
    'BepInEx\unity-libs\*',
    'BepInEx\*.log',
    'BepInEx\config\*.cfg',
    'BepInEx\config\vhvr-openxr-host.json.*.bak',   # written by VhvrSettings.exe before every Apply
    'doorstop*.log'
)

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

if ($GameRoot) { $game = [IO.Path]::GetFullPath($GameRoot) }
elseif (Test-Path -LiteralPath (Join-Path $here 'Vampire Hunters.exe')) { $game = $here }
else {
    $candidate = Join-Path $here 'vhvr-game-root.txt'
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "This mod was not installed next to uninstall.bat, and vhvr-game-root.txt is missing. Pass -GameRoot '<game folder>'."
    }
    $game = ([IO.File]::ReadAllText($candidate)).Trim()
}
if (-not (Test-Path -LiteralPath (Join-Path $game 'Vampire Hunters.exe'))) {
    throw ("No 'Vampire Hunters.exe' under " + $game)
}
if (@(Get-Process -Name 'Vampire Hunters' -ErrorAction SilentlyContinue).Count) {
    throw 'The game is running. Close it and run uninstall.bat again.'
}

$manifestPath = Join-Path $game $manifestName
if (-not (Test-Path -LiteralPath $manifestPath)) { throw ('No manifest found: ' + $manifestPath) }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

# 1) verify every installed file is still exactly what this installer wrote
$missing = New-Object 'Collections.Generic.List[string]'
$changed = New-Object 'Collections.Generic.List[string]'
foreach ($file in @($manifest.files)) {
    $full = Join-Path $game $file.path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { $missing.Add($file.path); continue }
    if (($file.PSObject.Properties.Name -contains 'editable') -and $file.editable) { continue }
    if ((Get-Sha256 $full) -cne $file.sha256) { $changed.Add($file.path) }
}
if ($changed.Count -and -not $Force) {
    throw ('These files were modified after install, so nothing was deleted. Re-run uninstall.bat with -Force to delete them anyway: ' + ($changed -join ', '))
}

# 2) every file left inside an owned root must be one of ours or a known generated artifact
$known = @{}
foreach ($file in @($manifest.files)) { $known[$file.path] = $true }
$foreign = New-Object 'Collections.Generic.List[string]'
foreach ($root in @($manifest.ownedRoots)) {
    $rootPath = Join-Path $game $root
    if (-not (Test-Path -LiteralPath $rootPath)) { continue }
    if (Test-Path -LiteralPath $rootPath -PathType Leaf) { if (-not $known.ContainsKey($root)) { $foreign.Add($root) }; continue }
    foreach ($item in @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force)) {
        $relative = $item.FullName.Substring($game.Length + 1)
        if ($known.ContainsKey($relative)) { continue }
        $isAllowed = $false
        foreach ($pattern in $allowedGenerated) { if ($relative -like $pattern) { $isAllowed = $true; break } }
        if (-not $isAllowed) { $foreign.Add($relative) }
    }
}
if ($foreign.Count -and -not $Force) {
    throw ('Unexpected files were found inside the mod folders, so nothing was deleted. Re-run uninstall.bat with -Force to delete them anyway: ' + ($foreign -join ', '))
}

# 3) delete the manifest files first, then the owned roots (which removes generated artifacts too)
$deleted = 0
foreach ($file in @($manifest.files)) {
    $full = Join-Path $game $file.path
    if (Test-Path -LiteralPath $full -PathType Leaf) { Remove-Item -LiteralPath $full -Force; $deleted++ }
}
foreach ($root in @($manifest.ownedRoots)) {
    $rootPath = Join-Path $game $root
    if (Test-Path -LiteralPath $rootPath) { Remove-Item -LiteralPath $rootPath -Recurse -Force }
}
# Doorstop's own log file lives in the game root, not inside a folder we own.
$generated = @(Get-ChildItem -LiteralPath $game -Filter 'doorstop*.log' -File -Force -ErrorAction SilentlyContinue)
foreach ($item in $generated) { Remove-Item -LiteralPath $item.FullName -Force }
Remove-Item -LiteralPath $manifestPath -Force
Remove-Item -LiteralPath (Join-Path $here 'vhvr-game-root.txt') -Force -ErrorAction SilentlyContinue

Write-Output ("UNINSTALLED: removed {0} installed files and the loader folders from {1}" -f $deleted, $game)
if ($missing.Count) { Write-Output ('NOTE: already missing before uninstall: ' + ($missing -join ', ')) }
if ($generated.Count) { Write-Output ('NOTE: also removed loader logs: ' + (($generated | ForEach-Object { $_.Name }) -join ', ')) }
Write-Output ("LOGS: left in {0} - delete that folder if you do not want them." -f $manifest.outputDirectory)
Write-Output 'The game itself was not modified.'