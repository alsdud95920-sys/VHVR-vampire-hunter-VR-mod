#Requires -Version 5.1
<#
.SYNOPSIS
    Assemble dist\VampireHuntersVR-<Version>.zip from the previously published package.

.DESCRIPTION
    The 0.9.4/0.9.5 packages carry 255 entries (BepInEx runtime, dotnet host, doorstop, licenses, config
    templates) that are not rebuilt per release. Rather than reassembling that payload by hand, this
    script extracts the last published ZIP, renames its root folder, and replaces only the files this
    release actually changed. Whatever is not in -Replace stays byte-identical to the source package.

    Run it after `dotnet build` for src\core and src\settings. The binaries in the ZIP must be the same
    bytes that were deployed to the headset, otherwise the "byte-identical to the tested build" claim in
    the release notes is false. The script prints every replaced file with its SHA256 so that claim can
    be checked against the install receipt.

.PARAMETER Version
    New package version, e.g. 0.9.6. Used for the root folder, ZIP name and the checksum sidecar.

.PARAMETER BaseVersion
    Version of the published ZIP to use as the base. Defaults to 0.9.5.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\pack-release.ps1 -Version 0.9.6
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$BaseVersion = '0.9.5'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# $PSScriptRoot is empty while default parameter values are evaluated on PS 5.1, so resolve it here.
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Test-Utf16String([byte[]]$bytes, [string]$needle) {
    $pattern = [Text.Encoding]::Unicode.GetBytes($needle)
    $last = $bytes.Length - $pattern.Length
    for ($i = 0; $i -le $last; $i++) {
        if ($bytes[$i] -ne $pattern[0]) { continue }
        $hit = $true
        for ($j = 1; $j -lt $pattern.Length; $j++) {
            if ($bytes[$i + $j] -ne $pattern[$j]) { $hit = $false; break }
        }
        if ($hit) { return $true }
    }
    return $false
}

$dist = Join-Path $repo 'dist'
$baseZip = Join-Path $dist ('VampireHuntersVR-{0}.zip' -f $BaseVersion)
$outZip = Join-Path $dist ('VampireHuntersVR-{0}.zip' -f $Version)
$stageRoot = Join-Path $repo ('artifacts\pack-{0}' -f $Version)
$stage = Join-Path $stageRoot ('VampireHuntersVR-{0}' -f $Version)
$baseRoot = Join-Path $stageRoot ('VampireHuntersVR-{0}' -f $BaseVersion)

if (-not (Test-Path -LiteralPath $baseZip)) { throw "Base package not found: $baseZip" }
if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

Write-Output ("Extracting {0}" -f $baseZip)
[IO.Compression.ZipFile]::ExtractToDirectory($baseZip, $stageRoot)
if (-not (Test-Path -LiteralPath $baseRoot)) { throw "Base package folder missing after extract: $baseRoot" }
Move-Item -LiteralPath $baseRoot -Destination $stage

# Files replaced for this release. Anything absent here stays as published in the base package.
$replace = @(
    @{ src = 'src\core\bin\Release\net6.0\VampireHuntersVR.Stage0.dll'; dst = 'payload\BepInEx\plugins\VampireHuntersVR.Stage0.dll' },
    @{ src = 'src\settings\bin\Release\net10.0-windows\VhvrSettings.exe';  dst = 'VhvrSettings.exe' },
    @{ src = 'src\settings\bin\Release\net10.0-windows\VhvrSettings.dll';  dst = 'VhvrSettings.dll' },
    @{ src = 'src\settings\bin\Release\net10.0-windows\VhvrSettings.pdb';  dst = 'VhvrSettings.pdb' },
    @{ src = 'src\settings\bin\Release\net10.0-windows\VhvrSettings.deps.json'; dst = 'VhvrSettings.deps.json' },
    @{ src = 'src\settings\bin\Release\net10.0-windows\VhvrSettings.runtimeconfig.json'; dst = 'VhvrSettings.runtimeconfig.json' },
    @{ src = 'installer\install.bat';           dst = 'install.bat' },
    @{ src = 'installer\install.ps1';           dst = 'install.ps1' },
    @{ src = 'installer\uninstall.bat';         dst = 'uninstall.bat' },
    @{ src = 'installer\uninstall.ps1';         dst = 'uninstall.ps1' },
    @{ src = 'installer\README-KO.md';          dst = 'README-KO.md' },
    @{ src = 'installer\SETTINGS-README-KO.md'; dst = 'SETTINGS-README-KO.md' },
    @{ src = 'installer\ModNotes.txt';          dst = 'ModNotes.txt' },
    @{ src = 'installer\ModNotes.txt';          dst = 'payload\ModNotes.txt' }
)

$rows = @()
foreach ($item in $replace) {
    $from = Join-Path $repo $item.src
    $to = Join-Path $stage $item.dst
    if (-not (Test-Path -LiteralPath $from)) { throw "Missing source file: $from" }
    Copy-Item -LiteralPath $from -Destination $to -Force
    $rows += [pscustomobject]@{
        file = $item.dst
        bytes = (Get-Item -LiteralPath $to).Length
        sha256 = (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
    }
}

# --- gates: the package must advertise the version we are shipping ---
$installText = Get-Content -LiteralPath (Join-Path $stage 'install.ps1') -Raw
if ($installText -notmatch ("version\s*=\s*'{0}'" -f [regex]::Escape($Version))) {
    throw ("install.ps1 does not declare version = '{0}'" -f $Version)
}
$notesFirst = (Get-Content -LiteralPath (Join-Path $stage 'ModNotes.txt') -TotalCount 1)
if ($notesFirst -notmatch [regex]::Escape($Version)) {
    throw ("ModNotes.txt first line does not name {0}: {1}" -f $Version, $notesFirst)
}
$pluginBytes = [IO.File]::ReadAllBytes((Join-Path $stage 'payload\BepInEx\plugins\VampireHuntersVR.Stage0.dll'))
if (-not (Test-Utf16String $pluginBytes 'snapTurnsApplied') -or -not (Test-Utf16String $pluginBytes 'SNAP_TURN') -or -not (Test-Utf16String $pluginBytes 'viewTurnedDegrees')) {
    throw 'Stage0.dll is not the D-211/D-213 build (turn receipt/log strings missing)'
}
$settingsBytes = [IO.File]::ReadAllBytes((Join-Path $stage 'VhvrSettings.dll'))
if (-not (Test-Utf16String $settingsBytes 'Snap turn') -or -not (Test-Utf16String $settingsBytes 'Smooth')) {
    throw 'VhvrSettings.dll is not the D-211/D-213 build (Snap/Smooth turn controls missing)'
}

if (Test-Path -LiteralPath $outZip) { Remove-Item -LiteralPath $outZip -Force }
Write-Output ("Compressing {0}" -f $outZip)
# ZipFile.CreateFromDirectory on .NET Framework writes backslash separators, which some extractors
# (and the 0.9.5 package) do not share. Write the entries ourselves with forward slashes and keep the
# VampireHuntersVR-<Version> root folder, matching the published layout.
$zip = [IO.Compression.ZipFile]::Open($outZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    $prefix = $stageRoot.TrimEnd('\') + '\'
    foreach ($file in (Get-ChildItem -LiteralPath $stageRoot -Recurse -File | Sort-Object FullName)) {
        $name = $file.FullName.Substring($prefix.Length).Replace('\', '/')
        $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $target = $entry.Open()
        $source = [IO.File]::OpenRead($file.FullName)
        try { $source.CopyTo($target) } finally { $source.Dispose(); $target.Dispose() }
    }
} finally {
    $zip.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $outZip -Algorithm SHA256).Hash
$zipSize = (Get-Item -LiteralPath $outZip).Length
$stage0 = ($rows | Where-Object { $_.file -like '*VampireHuntersVR.Stage0.dll' }).sha256
$native = (Get-FileHash -LiteralPath (Join-Path $stage 'payload\BepInEx\plugins\VhvrNativeHost.dll') -Algorithm SHA256).Hash
$extras = (Get-FileHash -LiteralPath (Join-Path $stage 'payload\BepInEx\plugins\VhvrExtras.dll') -Algorithm SHA256).Hash

$sidecar = @"
SHA256  $zipHash  VampireHuntersVR-$Version.zip
SIZE  $zipSize bytes
Stage0 SHA256  $stage0
NativeHost SHA256  $native
Extras SHA256  $extras

"@
$sidecarPath = Join-Path $dist ('VampireHuntersVR-{0}.sha256.txt' -f $Version)
[IO.File]::WriteAllText($sidecarPath, $sidecar, (New-Object Text.UTF8Encoding($false)))

Write-Output ''
Write-Output 'Replaced files:'
$rows | Format-Table -AutoSize | Out-String | Write-Output
Write-Output ("ZIP      {0}" -f $outZip)
Write-Output ("SIZE     {0} bytes" -f $zipSize)
Write-Output ("SHA256   {0}" -f $zipHash)
Write-Output ("SIDECAR  {0}" -f $sidecarPath)
Write-Output ("Stage0   {0}" -f $stage0)
Write-Output ("Native   {0}" -f $native)
Write-Output ("Extras   {0}" -f $extras)
