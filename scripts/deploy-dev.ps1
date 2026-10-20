# Developer deploy: copy the freshly built plugin + settings tool into an installed game folder.
# This is NOT the release installer (installer\install.ps1): it overwrites files in an existing 0.9.x install,
# keeps a timestamped backup of everything it replaces, and refreshes the manifest hash of the plugin so a
# strict uninstall still recognises it. Used for headset verification of work that has no release yet.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy-dev.ps1 `
#       -GameRoot 'F:\SteamLibrary\steamapps\common\Vampire Hunters'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameRoot,
    [string]$Repo = '',
    [string]$BundleFolder = 'VampireHuntersVR-0.9.5-rc1',
    [string]$BackupRoot = 'F:\VRMods\backup'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# $PSScriptRoot is not populated yet while default parameter values are evaluated (Windows PowerShell 5.1).
if (-not $Repo) { $Repo = Split-Path -Parent $PSScriptRoot }

if (@(Get-Process -Name 'Vampire Hunters' -ErrorAction SilentlyContinue).Count) { throw 'Close the game first.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'Vampire Hunters.exe'))) { throw "No game under $GameRoot" }

$bundle = Join-Path $GameRoot $BundleFolder
if (-not (Test-Path -LiteralPath $bundle)) { throw "No bundle folder $bundle" }

$stamp = [DateTime]::Now.ToString('yyyyMMddTHHmmss')
$backup = Join-Path $BackupRoot ($stamp + '-dev-deploy')
$null = New-Item -ItemType Directory -Path $backup -Force

$settingsBin = Join-Path $Repo 'src\settings\bin\Release\net10.0-windows'
$coreBin = Join-Path $Repo 'src\core\bin\Release\net6.0'
$map = @(
    @{ From = (Join-Path $coreBin 'VampireHuntersVR.Stage0.dll'); To = (Join-Path $GameRoot 'BepInEx\plugins\VampireHuntersVR.Stage0.dll') },
    @{ From = (Join-Path $settingsBin 'VhvrSettings.exe'); To = (Join-Path $bundle 'VhvrSettings.exe') },
    @{ From = (Join-Path $settingsBin 'VhvrSettings.dll'); To = (Join-Path $bundle 'VhvrSettings.dll') },
    @{ From = (Join-Path $settingsBin 'VhvrSettings.pdb'); To = (Join-Path $bundle 'VhvrSettings.pdb') },
    @{ From = (Join-Path $settingsBin 'VhvrSettings.deps.json'); To = (Join-Path $bundle 'VhvrSettings.deps.json') },
    @{ From = (Join-Path $settingsBin 'VhvrSettings.runtimeconfig.json'); To = (Join-Path $bundle 'VhvrSettings.runtimeconfig.json') },
    @{ From = (Join-Path $Repo 'installer\install.ps1'); To = (Join-Path $bundle 'install.ps1') },
    @{ From = (Join-Path $Repo 'installer\README-KO.md'); To = (Join-Path $bundle 'README-KO.md') },
    @{ From = (Join-Path $Repo 'installer\SETTINGS-README-KO.md'); To = (Join-Path $bundle 'SETTINGS-README-KO.md') }
)
foreach ($item in $map) {
    if (-not (Test-Path -LiteralPath $item.From)) { throw ('Missing build output: ' + $item.From) }
}

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

# 1) back up everything that is about to change (plus the manifest and the live config, for rollback)
foreach ($item in $map) {
    if (Test-Path -LiteralPath $item.To) {
        $relative = $item.To.Substring($GameRoot.Length + 1)
        $destination = Join-Path $backup $relative
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force
        Copy-Item -LiteralPath $item.To -Destination $destination -Force
    }
}
Copy-Item -LiteralPath (Join-Path $GameRoot 'vhvr-uninstall.json') -Destination (Join-Path $backup 'vhvr-uninstall.json') -Force
Copy-Item -LiteralPath (Join-Path $GameRoot 'BepInEx\config\vhvr-openxr-host.json') -Destination (Join-Path $backup 'vhvr-openxr-host.json') -Force

# 2) copy the new binaries/documents in
foreach ($item in $map) { Copy-Item -LiteralPath $item.From -Destination $item.To -Force }

# 3) refresh the manifest hash of the plugin so uninstall.bat (strict mode) still recognises it
$manifestPath = Join-Path $GameRoot 'vhvr-uninstall.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$dllRel = 'BepInEx\plugins\VampireHuntersVR.Stage0.dll'
$dllPath = Join-Path $GameRoot $dllRel
$patched = $false
foreach ($entry in @($manifest.files)) {
    if ($entry.path -eq $dllRel) {
        $entry.bytes = [long](Get-Item -LiteralPath $dllPath).Length
        $entry.sha256 = (Get-Sha256 $dllPath)
        $patched = $true
    }
}
if (-not $patched) { throw ('No manifest entry for ' + $dllRel) }
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))

# 4) report exactly what landed
Write-Output ('BACKUP: ' + $backup)
foreach ($item in $map) {
    $relative = $item.To.Substring($GameRoot.Length + 1)
    Write-Output ('DEPLOYED {0,-58} {1,8} B  {2}' -f $relative, (Get-Item -LiteralPath $item.To).Length, (Get-Sha256 $item.To))
}
