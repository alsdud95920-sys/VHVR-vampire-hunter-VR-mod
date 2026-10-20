param(
    [ValidateSet('Plan', 'Install', 'Uninstall')][string]$Action = 'Plan',
    [string]$Workspace = '',
    # Optional project-local diagnostic config is planned/hashed with the payload, never a loose game write.
    [string]$StereoCaptureConfig = '',
    [string]$OpenXrHostConfig = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Stage 0 entry wrapper. Adds ONLY the pinned BepInEx IL2CPP loader + diagnostic plugin via the tested
# transaction engine. Never launches the game, never touches Steam settings, saves, or stock files.
# Plan = read-only. Install/Uninstall need the user's explicit authorization (given 2026-10-06, install only).
. (Join-Path $PSScriptRoot 'EntryTransaction.ps1')
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$game = if ($env:VHVR_GAME_ROOT) { $env:VHVR_GAME_ROOT } else { 'C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters' }
$acf = Join-Path (Split-Path -Parent (Split-Path -Parent $game)) 'appmanifest_2206270.acf'
$transactions = Join-Path $root 'artifacts\transactions'
$pointer = Join-Path $transactions 'current.txt'
$supported = Get-Content -LiteralPath (Join-Path $root 'config\supported-build.json') -Raw | ConvertFrom-Json

function Assert-NoGame {
    if (@(Get-Process | Where-Object { $_.ProcessName -eq 'Vampire Hunters' }).Count -gt 0) { throw 'Game running; close it first.' }
}
$guard = { Assert-EntryTargetBuild $game $acf $supported }

Assert-NoGame
& $guard
$manifest = Join-Path $root 'backups\20261006T104307Z-cdd080e1e50a4fda845ffa7c01158173\manifest.json'
if ((Get-EntryFileRecord $manifest 'manifest.json').SHA256 -cne '828B45CCC6D82AF32E2E4636B63D718E55FC188AFB5245F1B8DCC39A8F593A6F') { throw 'Pinned backup manifest differs.' }
$backup = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($backup.Game.Root -cne $game) { throw 'Backup game root differs.' }

if ($Action -eq 'Uninstall') {
    if (-not $Workspace) {
        if (-not (Test-Path -LiteralPath $pointer)) { throw 'No recorded install workspace; pass -Workspace.' }
        $Workspace = ([IO.File]::ReadAllText($pointer)).Trim()
    }
    $stock = Get-BackupInventory $game
    # Stock for recovery is the journal header captured at install time.
    $header = ([IO.File]::ReadAllLines((Join-Path $Workspace 'journal.jsonl'))[0] | ConvertFrom-Json)
    $result = Undo-EntryTransaction -Target $game -Stock $header.Stock -Workspace $Workspace -Guard $guard
    $result | ConvertTo-Json -Depth 5
    exit 0
}

# Pre-install state must equal the verified stock backup exactly.
$stock = Get-BackupInventory $game
Assert-BackupInventoryEqual $backup.Game $stock -CheckSourceTimes
$dependency = (Get-Content -LiteralPath (Join-Path $root 'docs\DEPENDENCIES.json') -Raw | ConvertFrom-Json).bepinex
$archive = Join-Path $root 'tools\bepinex-il2cpp-be788.zip'
if ((Get-Item -LiteralPath $archive).Length -ne $dependency.zipBytes) { throw 'Pinned ZIP size differs.' }
$extracted = Join-Path $root 'tools\bepinex-be788'
$loader = @(Get-EntryArchiveInventory $archive $dependency.sha256 $extracted)
$pluginPath = Join-Path $root 'src\core\bin\Release\net6.0\VampireHuntersVR.Stage0.dll'
$plugin = Get-EntryFileRecord $pluginPath 'BepInEx\plugins\VampireHuntersVR.Stage0.dll'
$stereoRelative = 'BepInEx\config\vhvr-stereo-capture.json'
if ($StereoCaptureConfig) {
    $config = Get-Content -LiteralPath $StereoCaptureConfig -Raw | ConvertFrom-Json
    if ($config.Enabled -ne $true -or -not [IO.Path]::IsPathRooted($config.Output) -or -not (Test-Path $config.RuntimeReceipt)) {
        throw 'Invalid optional stereo capture config; not installing.'
    }
    $configRecord = Get-EntryFileRecord $StereoCaptureConfig $stereoRelative
    $loader = @($loader) + @($configRecord)
}
$hostSources = @{}
if ($OpenXrHostConfig) {
    if ($StereoCaptureConfig) { throw 'Do not run both diagnostic components concurrently.' }
    $config = Get-Content -LiteralPath $OpenXrHostConfig -Raw | ConvertFrom-Json
    $simulator = if ($env:VHVR_XR_SIM_JSON) { $env:VHVR_XR_SIM_JSON } else { 'C:\MetaXR\meta_openxr_simulator.json' }
    $isHeadset = ($config.PSObject.Properties.Name -contains 'Headset') -and $config.Headset -eq $true
    if ($config.PSObject.Properties.Name -contains 'Headset' -and $config.Headset -isnot [bool]) { throw 'Headset must be boolean.' }
    # D-145: an empty Runtime means the native host adopts the machine's active OpenXR runtime, so any HMD runtime is allowed.
    $runtimeSpecified = -not [string]::IsNullOrEmpty([string]$config.Runtime)
    $runtimeOk = if ($isHeadset) { -not $runtimeSpecified -or (Test-Path -LiteralPath $config.Runtime) } else { $config.Runtime -ceq $simulator }
    if ($config.Enabled -ne $true -or -not [IO.Path]::IsPathRooted($config.Output) -or
        -not $runtimeOk -or $config.Frames -lt 120 -or $config.Frames -gt 1000) { throw 'Invalid OpenXR host config: the simulator runtime, or an existing (or empty = auto-detect) Runtime path with Headset=true.' }
    $names = $config.PSObject.Properties.Name
    if ($isHeadset -and (($names -notcontains 'GameplayOnly') -or ($names -notcontains 'HandAim') -or ($names -notcontains 'HeadsetSeconds') -or $config.GameplayOnly -ne $true -or $config.HandAim -ne $true -or $config.HeadsetSeconds -isnot [int] -or
        $config.HeadsetSeconds -lt 0 -or ($config.HeadsetSeconds -gt 0 -and ($config.HeadsetSeconds -lt 60 -or $config.HeadsetSeconds -gt 900)))) { throw 'Headset config needs GameplayOnly, HandAim and HeadsetSeconds 0 (unlimited) or 60-900.' }
    if($config.PSObject.Properties.Name -contains 'GameplayOnly' -and $config.GameplayOnly -isnot [bool]){throw 'GameplayOnly must be boolean.'}
    if($config.PSObject.Properties.Name -contains 'HandAim' -and ($config.HandAim -isnot [bool] -or ($config.HandAim -and $config.GameplayOnly -ne $true))){throw 'HandAim must be boolean and requires GameplayOnly.'}
    if($config.PSObject.Properties.Name -contains 'HandFire' -and ($config.HandFire -isnot [bool] -or ($config.HandFire -and $config.HandAim -ne $true))){throw 'HandFire must be boolean and requires HandAim.'}
    foreach($flag in @('StockFireCadence','AutonomousHeadAim','HideHudBackdrop','FlameEndpointVfx','FlameEmitterPin','RenderTimingDiagnostics')) {
        if($names -contains $flag -and $config.$flag -isnot [bool]) { throw "$flag must be boolean." }
    }
    if($names -contains 'UiVerticalOffset' -and (($config.UiVerticalOffset -isnot [int] -and $config.UiVerticalOffset -isnot [long] -and $config.UiVerticalOffset -isnot [double] -and $config.UiVerticalOffset -isnot [decimal]) -or
        [double]::IsNaN([double]$config.UiVerticalOffset) -or [double]::IsInfinity([double]$config.UiVerticalOffset) -or
        $config.UiVerticalOffset -lt -0.30 -or $config.UiVerticalOffset -gt 0.10)) { throw 'UiVerticalOffset must be finite, -0.30..0.10 metres.' }
    if($config.PSObject.Properties.Name -contains 'GameplayWaitSeconds' -and
        ($config.GameplayWaitSeconds -lt 30 -or $config.GameplayWaitSeconds -gt $(if($isHeadset){600}else{180}))){throw 'Invalid bounded gameplay wait.'}
    # D-206 (BUG-204): the turret build-mode grip cycle surface. An unknown value is refused here as well as in the host.
    if($names -contains 'TurretCycle' -and (@('Stick','Dpad','Special','Off') -notcontains [string]$config.TurretCycle)) { throw 'TurretCycle must be Stick, Dpad, Special or Off.' }
    $hostSources['BepInEx\config\vhvr-openxr-host.json'] = $OpenXrHostConfig
    $hostSources['BepInEx\plugins\VhvrNativeHost.dll'] = Join-Path $root 'src\render\NativeHost\bin\VhvrNativeHost.dll'
    $hostSources['BepInEx\plugins\openxr_loader.dll'] = Join-Path $root 'artifacts\openxr\extracted\x64\bin\openxr_loader.dll'
    if ((Get-FileHash $hostSources['BepInEx\plugins\openxr_loader.dll'] -Algorithm SHA256).Hash -cne
        '66946EA6E9CC2649E299EC412521C5EAFE56C635D05055097FE4DB81E11932DF') { throw 'Pinned OpenXR loader hash differs.' }
    foreach($relative in $hostSources.Keys) {
        $loader = @($loader) + @(Get-EntryFileRecord $hostSources[$relative] $relative)
    }
}
$plan = Get-EntryOwnedFilePlan $game $loader $plugin
$files = @()
foreach ($f in $plan.Files) {
    $source = if ($f.Path -ceq $plugin.Path) { $pluginPath }
        elseif ($hostSources.ContainsKey($f.Path)) { $hostSources[$f.Path] }
        elseif ($StereoCaptureConfig -and $f.Path -ceq $stereoRelative) { $StereoCaptureConfig }
        else { Join-Path $extracted $f.Path }
    $files += [pscustomobject]@{ Path=$f.Path; Bytes=$f.Bytes; SHA256=$f.SHA256; Source=$source }
}
if ($Action -eq 'Plan') {
    Write-Output ('INSTALL_PLAN_OK: files=' + $files.Count + '; roots=' + ($plan.OwnedRoots -join ',') + '; stock=' + @($stock.Files).Count + '. Nothing written.')
    exit 0
}

New-Item -ItemType Directory -Path $transactions -Force | Out-Null
if (-not $Workspace) { $Workspace = Join-Path $transactions ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')) }
$result = Invoke-EntryTransaction -Target $game -Files $files -Stock $stock -Workspace $Workspace -Guard $guard
[IO.File]::WriteAllText($pointer, $Workspace)
$result | ConvertTo-Json -Depth 5
exit 0