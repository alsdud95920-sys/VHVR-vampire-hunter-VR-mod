# Manual headset drill for the soft-gated independent hand aim (D-087). NOT part of tests/verify.ps1: it needs a person.
# One command does: preflight -> transactional install (HandAim + Headset config) -> headset wait -> Steam launch ->
# monitor (hand-aim.json sampled into an aggregate) -> orderly stop -> transactional uninstall -> whole-tree stock check.
# It never changes the OpenXR runtime registry (the game selects Virtual Desktop XR through XR_RUNTIME_JSON for its own
# process only), never touches saves/Steam settings/anti-cheat and synthesizes no keyboard/mouse input. From D-089 the
# trigger calls the game per-slot fire path (HandFire, default on; -NoFire leaves the trigger unwired). Reload and
# secondary fire are not wired.
# Exit code = "did the harness run correctly" (stock restored, runtime unchanged). The scientific result is
# run.json -> observation + the user's own report (docs/playtests/PLAYTEST-6B-HANDAIM-KO.md). Nothing here passes a gate.
param(
    [string]$Tag = 'ha1',
    [int]$HeadsetSeconds = 600,
    [int]$EyeWidth = 2064,
    [int]$EyeHeight = 2208,
    # RenderFrames: engine frames the eye cameras stay enabled per XR frame. Default 1 (D-148: 2 renders an extra engine
    # frame that is discarded; d147a measured prepToSubmitMs 17.0 vs 8.5 ms, cycle 30.10 vs 22.53 ms at the same eye size).
    [ValidateRange(1,3)][int]$RenderFrames = 1,
    [switch]$DisableVSync,
    # Anisotropic: force anisotropic filtering 16x (D-104). Default OFF; A/B it against a run without.
    [switch]$Anisotropic,
    # EyePreset: low (0.8x) / normal (1.0x) / high (1.2x) scale EyeWidth/EyeHeight per eye; custom uses them as given (D-147).
    [ValidateSet('low','normal','high','custom')][string]$EyePreset = 'normal',
    # CaptureFirstFrames: write the frame-30 raw eye/source pair (4 files, ~74 MB at the default per-eye size). Default OFF (D-147).
    [switch]$CaptureFirstFrames,
    # MonitorCostProbe (D-148): three 120-frame windows - baseline, source camera disabled (monitor view off), restored.
    # Measurement only: prices the monitor view, which no sitting has measured. Default OFF.
    [switch]$MonitorCostProbe,
    # DeferredGpuWait (D-148b3): opt-in overlap experiment - the GPU completion proof for the two eye copies moves to the
    # start of the next submit (or the close drain) instead of spinning inside this one. Default OFF: the shipped path
    # still proves completion inside the submit.
    [switch]$DeferredGpuWait,
    # PrepareAhead (D-149): opt-in early frame prep - the native submit prepares the next XR frame itself, so the engine
    # never spends a whole frame on a prepare-only pass. Default OFF: the shipped frame structure is the measured control.
    [switch]$PrepareAhead,
    # PerfProfile (D-150): opt-in engine quality dial - off (default, the game's own settings) / light / strong. Applied
    # once at Initialize and logged before and after, one setting at a time. It only ever lowers a dial (shadow distance,
    # cascades, shadow resolution, pixel light count, LOD bias, particle raycast budget, reflection probes, soft
    # particles), so it is a quality cost that may buy fps - never a claim.
    [ValidateSet('off','light','strong')][string]$PerfProfile = 'off',
    # LeftTwistDeg/RightTwistDeg (D-153, TOOL-073): the per-hand weapon rest-twist dial in degrees about the muzzle axis.
    # 0 (default) is the authored rest pose; the dial turns the visible model only and never the muzzle/aim/fire origin.
    [ValidateRange(-180,180)][double]$LeftTwistDeg = 0,
    [ValidateRange(-180,180)][double]$RightTwistDeg = 0,
    [switch]$NoFlameEndpointVfx,
    [switch]$NoFlameEmitterPin,
    [int]$GameplayWaitSeconds = 600,
    [int]$HeadsetWaitSeconds = 300,
    [int]$InstallerTimeoutSeconds = 600,
    # DryRun: preflight + one headset probe + read-only install Plan. Writes nothing to the game folder, launches nothing.
    [switch]$DryRun,
    # NoFire: leave the trigger unwired (aim + reticle only). Default is trigger fire ON (D-089).
    [switch]$NoFire,
    [switch]$NoStockFireCadence,
    [switch]$NoAutonomousHeadAim,
[switch]$NoShot,
# ShotTracer: also patch draw_shoot_vfx to measure tracers. CRASHES the game (D-097); only for a deliberate crash repro.
[switch]$ShotTracer,
# NoRenderTimingDiagnostics: drop the D-136/D-138 read-only render-timing probe (no patches, no render-timing.jsonl).
# Default is the probe ON, exactly as D-136 shipped it.
[switch]$NoRenderTimingDiagnostics,
[switch]$NoUi,
[switch]$NoHudBackdrop,
 # GameBackdropMode (D-155): which of the game's OWN backdrops the mod may touch. authored (default) = none, the level-up
 # black background / menus / chest screens keep the game's own alpha; dim = the D-096 alpha cap only; hide = the pre-D-155
 # cull. The exact HUD Bottom Shadow is removed in every mode unless -NoHudBackdrop.
 [ValidateSet('authored','dim','hide')][string]$GameBackdropMode = 'authored',
# WorldMarkers (D-157/D-161): world-anchored target markers for the things the player must find (chests, potions, boss chests).
# off|on. MarkerKinds is a comma separated list of the build's exact PickUp.type enum names plus the reserved tokens CHEST
# (the game's RewardBox = chest / boss chest) and POI (an arrow target the mod cannot name; off by default). MarkerMaxDistance
# is metres and 0 means NO LIMIT (every chest and potion is marked); MarkerMaxCount caps how many arrows are drawn at once.
# An unknown mode or token makes the host refuse to start instead of showing a different set than this config asks for.
[ValidateSet('off','on')][string]$WorldMarkers = 'on',
[string]$MarkerKinds = 'CHEST,HEALING,POWER_UP,BOSS_CHEST',
[ValidateRange(0,200)][double]$MarkerMaxDistance = 0,
[ValidateRange(1,64)][int]$MarkerMaxCount = 32,
[ValidateRange(-0.30,0.10)][float]$UiVerticalOffset = -0.25,
[switch]$NoInput,
# NoHaptic: no controller pulse when a hand enters the 0.30 m reach beside the headset (D-103). Default is ON.
[switch]$NoHaptic,
    # SnapTurnDegrees: right-stick snap turn angle per push (D-099). 0 = off.
    [ValidateRange(0,180)][int]$SnapTurnDegrees = 30,
    # TurretCycle (D-206/BUG-204): which game surface one blueprint step is sent on while the engineer is in the game's
    # own turret build mode - Stick (the pad's left stick + the navigationDirection field, default), Dpad
    # (<Gamepad>/dpad/left,right), Special (pulse Special Ability = <Gamepad>/buttonEast) or Off (the pre-D-206 map, the
    # grips unbound as D-057 decided). One sitting A/Bs them by config file edit, with no rebuild.
    [ValidateSet('Stick','Dpad','Special','Off')][string]$TurretCycle = 'Stick',
    [switch]$SkipHeadsetCheck,
    # UsePreinstalled: the plugin + native host are ALREADY installed for this tag by an earlier invocation
    # (`headset-handaim.ps1 -Tag X -DryRun` then `Install-Stage0.ps1 -Action Install -Workspace <X>\transaction
    # -OpenXrHostConfig <X>\config.json`), so the headset can be worn without waiting for the install hashing.
    # Skips only the install step: it re-verifies that the installed DLLs match this build and that config.json
    # matches this invocation, and still uninstalls + checks stock at the end.
    [switch]$UsePreinstalled
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
# D-145: HeadsetSeconds 0 = unlimited session; any other value keeps the 60-900 s bound.
if ($Tag -notmatch '^[A-Za-z0-9_.-]+$' -or $HeadsetSeconds -lt 0 -or ($HeadsetSeconds -gt 0 -and ($HeadsetSeconds -lt 60 -or $HeadsetSeconds -gt 900)) -or $GameplayWaitSeconds -lt 30 -or
    $GameplayWaitSeconds -gt 600 -or $HeadsetWaitSeconds -lt 0 -or $HeadsetWaitSeconds -gt 900 -or
    $InstallerTimeoutSeconds -lt 300 -or $InstallerTimeoutSeconds -gt 900) { throw 'invalid parameter' }
$dir = Join-Path $root ('artifacts\headset-d087\' + $Tag)
if (Test-Path -LiteralPath $dir) {
    if (Test-Path -LiteralPath (Join-Path $dir 'retired.json')) { throw 'Retired preparation tag; use the current readiness board.' }
    if (-not $UsePreinstalled) { throw 'fresh tag required' }
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'transaction\journal.jsonl'))) { throw 'UsePreinstalled needs an installed transaction for this tag' }
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'config.json'))) { throw 'UsePreinstalled needs the pre-install config.json for this tag' }
} else {
    if ($UsePreinstalled) { throw 'UsePreinstalled but this tag was never pre-installed' }
    [IO.Directory]::CreateDirectory($dir) | Out-Null
}
$game = if ($env:VHVR_GAME_ROOT) { $env:VHVR_GAME_ROOT } else { 'C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters' }
$log = Join-Path $game 'BepInEx\LogOutput.log'
$workspace = Join-Path $dir 'transaction'
$runtimeKey = 'HKLM:\SOFTWARE\Khronos\OpenXR\1'
$probe = Join-Path $root 'src\render\OpenXrProbe\bin\Debug\net10.0\OpenXrProbe.exe'
$loader = Join-Path $root 'artifacts\openxr\extracted\x64\bin\openxr_loader.dll'
$checkStock = Join-Path $root 'artifacts\devtools\Check-Stock.ps1'
$plugin = Join-Path $root 'src\core\bin\Release\net6.0\VampireHuntersVR.Stage0.dll'
$native = Join-Path $root 'src\render\NativeHost\bin\VhvrNativeHost.dll'
$summary = [ordered]@{
    verdict = 'INCONCLUSIVE'; tag = $Tag; dryRun = [bool]$DryRun; preinstalled = [bool]$UsePreinstalled; installSkipped = $false; startedUtc = [DateTime]::UtcNow.ToString('o'); error = $null; cleanupError = $null
    stockVerified = $false; headsetAttached = $null; gameplayLockedUtc = $null; nativeClosedLine = $null; observation = 'NOT_RUN'
    userReportRequired = $true; gatesPassed = 'NONE (TOOL-033/Q-047/M4b stay open; hand aim is soft-gated and unverified until the user reports)'
}
$before = (Get-ItemProperty $runtimeKey).ActiveRuntime
$installed = $false; $launched = $false; $code = 2

function Say([string]$Text) { Write-Host ('[' + (Get-Date -Format 'HH:mm:ss') + '] ' + $Text) }
function Prop($o, [string]$n) { if ($null -eq $o) { return $null }; $p = $o.PSObject.Properties[$n]; if ($null -ne $p) { return $p.Value }; return $null }
function ReadJson([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    try { return (Get-Content -LiteralPath $path -Raw -ErrorAction Stop | ConvertFrom-Json) } catch { return $null }
}
function GameProcesses { @(Get-Process -Name 'Vampire Hunters' -ErrorAction SilentlyContinue) }
function Installer([string]$Action, [string]$Config = '') {
    $a = @('-NoProfile', '-NonInteractive', '-File', (Join-Path $root 'src\core\Install-Stage0.ps1'), '-Action', $Action)
    if ($Action -ne 'Plan') { $a += @('-Workspace', $workspace) }
    if ($Config) { $a += @('-OpenXrHostConfig', $Config) }
    $line = ($a | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $child = Start-Process powershell -ArgumentList $line -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $dir ($Action + '.log')) -RedirectStandardError (Join-Path $dir ($Action + '.stderr.log'))
    $handle = $child.Handle
    # TOOL-034: full-tree hashes at every recovery boundary can take minutes; bounded, never skipped.
    if (-not $child.WaitForExit($InstallerTimeoutSeconds * 1000)) { Stop-Process $child.Id -Force; throw ($Action + ' backstop; inspect journal') }
    $child.WaitForExit(); $summary[$Action + 'Exit'] = $child.ExitCode
    if ($child.ExitCode -ne 0) { throw ($Action + ' failed; see ' + $Action + '.stderr.log') }
}
function Invoke-Probe([string]$Name) {
    $probeDir = Join-Path $dir 'probe'; [IO.Directory]::CreateDirectory($probeDir) | Out-Null
    $out = Join-Path $probeDir ($Name + '.json')
    $a = @('--loader', $loader, '--out', $out, '--milestone', 'M2', '--frames', '1', '--budget-seconds', '5', '--ready-seconds', '20', '--extensions')
    $line = ($a | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $p = Start-Process -FilePath $probe -ArgumentList $line -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $probeDir ($Name + '.stdout.log')) -RedirectStandardError (Join-Path $probeDir ($Name + '.stderr.log'))
    $handle = $p.Handle
    $deadline = (Get-Date).AddSeconds(30)
    while (-not $p.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; $p.WaitForExit(10000) | Out-Null }
    return (ReadJson $out)
}

# ---- aggregate of the per-second hand-aim.json receipts (the receipt itself is overwritten each second)
function New-HandAgg { @{ statesSeen = @(); sampleActiveEver = $false; maxTrigger = 0.0; faceEver = $false; maxDrivenFrames = 0; maxGameOverwrites = 0
    maxWeaponsInSlot = 0; maxFireCalls = 0; maxReleaseCalls = 0; fireHeldEver = $false; reticleVisibleEver = $false; reticleHitEver = $false; container = $null; sharedEver = $false; muzzle = $null; minMuzzleDeg = $null; maxMuzzleDeg = $null; lastMuzzleDeg = $null } }
function Add-Hand($a, $h) {
    if ($null -eq $h) { return }
    $st = [string](Prop $h 'state'); if ($st -and ($a['statesSeen'] -notcontains $st)) { $a['statesSeen'] += $st }
    if ((Prop $h 'samplesActive') -eq $true) { $a['sampleActiveEver'] = $true }
    $t = Prop $h 'trigger'; if ($null -ne $t -and [double]$t -gt $a['maxTrigger']) { $a['maxTrigger'] = [double]$t }
    if ((Prop $h 'faceUpper') -eq $true) { $a['faceEver'] = $true }
    foreach ($pair in @(@('drivenFrames', 'maxDrivenFrames'), @('gameOverwrites', 'maxGameOverwrites'), @('weaponsInSlot', 'maxWeaponsInSlot'), @('fireCalls', 'maxFireCalls'), @('releaseCalls', 'maxReleaseCalls'))) {
        $v = Prop $h $pair[0]; if ($null -ne $v -and [int]$v -gt $a[$pair[1]]) { $a[$pair[1]] = [int]$v }
    }
    if ((Prop $h 'fireHeld') -eq $true) { $a['fireHeldEver'] = $true }
    if ((Prop $h 'reticleVisible') -eq $true) { $a['reticleVisibleEver'] = $true }
    if ((Prop $h 'reticleHitSurface') -eq $true) { $a['reticleHitEver'] = $true }
    $c = Prop $h 'container'; if ($c) { $a['container'] = [string]$c }
    if ((Prop $h 'sharedOrAmbiguous') -eq $true) { $a['sharedEver'] = $true }
    $m = Prop $h 'muzzle'; if ($m) { $a['muzzle'] = [string]$m }
    $d = Prop $h 'muzzleToHandForwardDegrees'
    if ($null -ne $d) {
        $d = [double]$d; $a['lastMuzzleDeg'] = $d
        if ($null -eq $a['minMuzzleDeg'] -or $d -lt $a['minMuzzleDeg']) { $a['minMuzzleDeg'] = $d }
        if ($null -eq $a['maxMuzzleDeg'] -or $d -gt $a['maxMuzzleDeg']) { $a['maxMuzzleDeg'] = $d }
    }
}
$agg = @{ controllersAvailableEver = $false; nativeFailuresMax = 0; lastError = $null; fireFailed = $false; fireError = $null; scenes = @(); receipts = 0; left = (New-HandAgg); right = (New-HandAgg) }
function Add-Receipt($r) {
    if ($null -eq $r) { return }
    $agg['receipts']++
    if ((Prop $r 'controllersAvailable') -eq $true) { $agg['controllersAvailableEver'] = $true }
    $nf = Prop $r 'nativeFailures'; if ($null -ne $nf -and [int]$nf -gt $agg['nativeFailuresMax']) { $agg['nativeFailuresMax'] = [int]$nf }
    $le = Prop $r 'lastError'; if ($le) { $agg['lastError'] = [string]$le }
    if ((Prop $r 'fireFailed') -eq $true) { $agg['fireFailed'] = $true }
    $fe = Prop $r 'fireError'; if ($fe) { $agg['fireError'] = [string]$fe }
    $sc = [string](Prop $r 'scene'); if ($sc -and ($agg['scenes'] -notcontains $sc)) { $agg['scenes'] += $sc }
    Add-Hand $agg['left'] (Prop $r 'left'); Add-Hand $agg['right'] (Prop $r 'right')
}

try {
    # ---------------- preflight (read-only)
    Say 'preflight'
    if (@(GameProcesses).Count -or (Get-Process OpenXrProbe -ErrorAction SilentlyContinue)) { throw 'target/probe already running; no takeover' }
    if (-not (Test-Path -LiteralPath $probe)) { throw ('probe not built: ' + $probe + ' (dotnet build src\render\OpenXrProbe\OpenXrProbe.csproj)') }
    foreach ($f in $loader, $checkStock, $plugin, $native) { if (-not (Test-Path -LiteralPath $f)) { throw ('missing: ' + $f) } }
    # D-145: any registered OpenXR runtime is accepted now (the drill itself still never switches the registry).
    if ([string]::IsNullOrEmpty([string]$before) -or -not (Test-Path -LiteralPath $before)) { throw ('no existing OpenXR runtime is registered (found: ' + $before + '); this drill never switches runtimes') }
    Say ('active OpenXR runtime: ' + $before)
    $summary.gitHead = ((& git -C $root rev-parse --short HEAD) | Out-String).Trim()
    $summary.gitDirtyFiles = @(& git -C $root status --porcelain).Count
    $summary.pluginSha256 = (Get-FileHash $plugin).Hash
    $summary.nativeSha256 = (Get-FileHash $native).Hash
    $summary.pluginBuiltUtc = (Get-Item $plugin).LastWriteTimeUtc.ToString('o')
    $summary.nativeBuiltUtc = (Get-Item $native).LastWriteTimeUtc.ToString('o')

    $config = Join-Path $dir 'config.json'
    # D-145: Runtime is left empty so the native host adopts whatever runtime the machine has registered.
    $want = [ordered]@{ Enabled = $true; Output = $dir; Runtime = ''; Frames = 120; GameplayOnly = $true; GameplayWaitSeconds = $GameplayWaitSeconds
        HandAim = $true; HandFire = (-not $NoFire.IsPresent); StockFireCadence = (-not $NoStockFireCadence.IsPresent); AutonomousHeadAim = (-not $NoAutonomousHeadAim.IsPresent); RenderTimingDiagnostics = (-not $NoRenderTimingDiagnostics.IsPresent); HandUi = (-not $NoUi.IsPresent); HideHudBackdrop = (-not $NoHudBackdrop.IsPresent); UiVerticalOffset = $UiVerticalOffset; GameBackdropMode = $GameBackdropMode; WorldMarkers = $WorldMarkers; MarkerKinds = $MarkerKinds; MarkerMaxDistance = $MarkerMaxDistance; MarkerMaxCount = $MarkerMaxCount; HandInput = (-not $NoInput.IsPresent); HandHaptic = (-not $NoHaptic.IsPresent); HandShot = (-not $NoShot.IsPresent); HandShotTracer = $ShotTracer.IsPresent; SnapTurnDegrees = $SnapTurnDegrees; TurretCycle = $TurretCycle; Headset = $true; HeadsetSeconds = $HeadsetSeconds; EyeWidth = $EyeWidth; EyeHeight = $EyeHeight; RenderFrames = $RenderFrames; DisableVSync = $DisableVSync.IsPresent; Anisotropic = $Anisotropic.IsPresent; EyePreset = $EyePreset; CaptureFirstFrames = $CaptureFirstFrames.IsPresent; MonitorCostProbe = $MonitorCostProbe.IsPresent; DeferredGpuWait = $DeferredGpuWait.IsPresent; PrepareAhead = $PrepareAhead.IsPresent; PerfProfile = $PerfProfile; LeftWeaponTwistDeg = $LeftTwistDeg; RightWeaponTwistDeg = $RightTwistDeg; FlameEndpointVfx = (-not $NoFlameEndpointVfx.IsPresent); FlameEmitterPin = (-not $NoFlameEmitterPin.IsPresent) }
    if ($UsePreinstalled) {
        # The payload hashes were fixed at install time, so a sitting with different settings is refused.
        $have = ReadJson $config
        if ($null -eq $have) { throw 'UsePreinstalled needs the pre-install config.json for this tag' }
        $diff = @($want.Keys | Where-Object { "$(Prop $have $_)" -ne "$($want[$_])" })
        if ($diff.Count -gt 0) { throw ('UsePreinstalled config differs at: ' + ($diff -join ',')) }
        Say 'pre-installed config matches this invocation'
    } else {
        $want | ConvertTo-Json | Set-Content $config -Encoding UTF8
    }

    if ($DryRun) {
        Say 'DRY RUN: one headset probe + read-only install plan (nothing is written to the game folder)'
        $r = Invoke-Probe 'dry'
        $err = if ($null -ne $r) { [string](Prop $r 'Error') } else { 'no receipt' }
        $summary.headsetAttached = ($null -ne $r -and (Prop $r 'Verdict') -eq 'PASS')
        $summary.probeError = $err
        Say ('headset attached now: ' + $summary.headsetAttached + $(if ($err) { '  (' + $err + ')' } else { '' }))
        Installer 'Plan' $config
        $summary.planOutput = (Get-Content (Join-Path $dir 'Plan.log') -Raw).Trim()

        $summary.verdict = 'DRY_RUN_OK'; $summary.observation = 'DRY_RUN'; $code = 0
        return
    }

    # ---------------- install first (slow hashing), so the headset can be connected meanwhile
    if ($UsePreinstalled) {
        $summary.installSkipped = $true
        foreach ($pair in @(@('BepInEx\plugins\VampireHuntersVR.Stage0.dll', $plugin), @('BepInEx\plugins\VhvrNativeHost.dll', $native))) {
            $installedPath = Join-Path $game $pair[0]
            if (-not (Test-Path -LiteralPath $installedPath)) { throw ('UsePreinstalled: missing installed ' + $pair[0]) }
            if ((Get-FileHash $installedPath -Algorithm SHA256).Hash -cne (Get-FileHash $pair[1] -Algorithm SHA256).Hash) { throw ('UsePreinstalled: installed ' + $pair[0] + ' differs from this build; re-install instead of reusing') }
        }
        Say 'pre-installed payload in use (install skipped); installed DLLs match this build'
    } else {
        Say 'installing plugin + native host (transactional; this takes a few minutes - connect the headset now)'
        Installer 'Install' $config; $installed = $true
        Say 'install done'
    }

    # ---------------- headset must be attached BEFORE the stage starts (native Init needs a system)
    if (-not $SkipHeadsetCheck) {
        Say ('waiting up to ' + $HeadsetWaitSeconds + ' s for the headset (Virtual Desktop connected)')
        $deadline = (Get-Date).AddSeconds($HeadsetWaitSeconds); $attempt = 0; $r = $null
        do {
            $attempt++; $r = Invoke-Probe ('wait' + $attempt)
            if ($null -ne $r -and (Prop $r 'Verdict') -eq 'PASS') { break }
            $err = if ($null -ne $r) { [string](Prop $r 'Error') } else { '' }
            if ($err -notmatch 'FormFactorUnavailable') { break }
            Start-Sleep -Seconds 5
        } while ((Get-Date) -lt $deadline)
        $summary.headsetAttached = ($null -ne $r -and (Prop $r 'Verdict') -eq 'PASS'); $summary.headsetProbeAttempts = $attempt
        if (-not $summary.headsetAttached) { $summary.verdict = 'NO_HEADSET_ATTACHED'; throw ('headset not attached: ' + $(if ($null -ne $r) { [string](Prop $r 'Error') } else { 'no probe receipt' })) }
        Say 'headset attached'
    }

    # ---------------- launch
    Start-Process 'steam://rungameid/2206270' | Out-Null; $launched = $true
    Say 'Steam launch requested'
    $appear = (Get-Date).AddSeconds(180)
    while (@(GameProcesses).Count -eq 0 -and (Get-Date) -lt $appear) { Start-Sleep -Milliseconds 500 }
    if (@(GameProcesses).Count -eq 0) { throw 'game process never appeared (TOOL-031 Steam URI launch); nothing was played' }
    Say ('game running. ON THE MONITOR: Play -> pick a stage -> start it. When it says GAMEPLAY LOCKED, put the headset on.')
    Say ('to stop early run: New-Item ' + (Join-Path $dir 'stop.request'))

    # ---------------- monitor
    $hostJson = Join-Path $dir 'host.json'; $nativeLog = Join-Path $dir 'host.json.native.log'
    $handFile = Join-Path $dir 'hand-aim.json'; $stopFile = Join-Path $dir 'stop.request'
    $timeline = Join-Path $dir 'hand-aim.timeline.jsonl'
    $locked = $false; $stopSeen = $null; $lastPrint = [DateTime]::MinValue; $lastTimeline = [DateTime]::MinValue
    # D-145: an unlimited session (HeadsetSeconds 0) still keeps this generous wall-clock backstop for the drill itself.
    $sessionBackstopSeconds = if ($HeadsetSeconds -gt 0) { $HeadsetSeconds } else { 3600 }
    $hardDeadline = (Get-Date).AddSeconds($GameplayWaitSeconds + $sessionBackstopSeconds + 240)
    $ended = $false
    while ((Get-Date) -lt $hardDeadline) {
        Start-Sleep -Milliseconds 700
        if (@(GameProcesses).Count -eq 0) { $summary.gameExitedBeforeEnd = $true; Say 'game process exited'; break }
        if (-not $locked -and (Test-Path (Join-Path $dir 'gameplay-camera.json'))) {
            $locked = $true; $summary.gameplayLockedUtc = [DateTime]::UtcNow.ToString('o')
            try { [Console]::Beep(880, 400); [Console]::Beep(1175, 400) } catch {}
            $sessionLabel = if ($HeadsetSeconds -gt 0) { "$HeadsetSeconds s" } else { 'unlimited' }
            Say ('*** GAMEPLAY LOCKED - PUT THE HEADSET ON NOW (session budget: ' + $sessionLabel + ') ***')
        }
        $h = ReadJson $hostJson; $hand = ReadJson $handFile
        Add-Receipt $hand
        $hostStatus = Prop $h 'status'
        $st = Prop $hostStatus 'State'; $frames = Prop $hostStatus 'Frames'
        # D-146: the receipt now names why the sitting ended (running / stop-request / level-exit / session-ended / watchdog).
        if ($null -ne $h) { $summary.endReason = Prop $h 'endReason' }
        $now = Get-Date
        if (($now - $lastTimeline).TotalSeconds -ge 2 -and $null -ne $hand) {
            $lastTimeline = $now
            (@{ t = [DateTime]::UtcNow.ToString('o'); frames = $frames; hand = $hand } | ConvertTo-Json -Depth 8 -Compress) | Add-Content -LiteralPath $timeline -Encoding UTF8
        }
        $lr = Prop $hand 'right'; $ll = Prop $hand 'left'
        @{ phase = $(if (-not $locked) { 'WAITING_FOR_GAMEPLAY' } elseif ($ended) { 'ENDING' } else { 'SESSION' }); hostState = $st; frames = $frames
           scene = (Prop $h 'scene'); controllersAvailable = (Prop $hand 'controllersAvailable')
           leftState = (Prop $ll 'state'); rightState = (Prop $lr 'state'); rightTrigger = (Prop $lr 'trigger'); leftTrigger = (Prop $ll 'trigger')
           rightDriven = (Prop $lr 'drivenFrames'); rightOverwrites = (Prop $lr 'gameOverwrites'); updatedUtc = [DateTime]::UtcNow.ToString('o')
        } | ConvertTo-Json | Set-Content (Join-Path $dir 'progress.json') -Encoding UTF8
        if (($now - $lastPrint).TotalSeconds -ge 10) {
            $lastPrint = $now
            Say ('locked=' + $locked + ' hostState=' + $st + ' frames=' + $frames + ' controllers=' + (Prop $hand 'controllersAvailable') +
                 ' R=' + (Prop $lr 'state') + '/driven=' + (Prop $lr 'drivenFrames') + '/overwrites=' + (Prop $lr 'gameOverwrites') +
                 ' L=' + (Prop $ll 'state') +
                 ' fire R/L=' + (Prop $lr 'fireCalls') + '/' + (Prop $ll 'fireCalls') + ' reticle R/L=' + (Prop $lr 'reticleVisible') + '/' + (Prop $ll 'reticleVisible'))
        }
        if (Test-Path -LiteralPath (Join-Path $dir 'host.json.failure.json')) { $summary.hostFailure = ReadJson (Join-Path $dir 'host.json.failure.json'); Say 'HOST FAILED (see failure json)'; break }
        if ($null -eq $stopSeen -and (Test-Path -LiteralPath $stopFile)) { $stopSeen = Get-Date; Say 'stop.request seen; waiting for the orderly native close' }
        if (Test-Path -LiteralPath $nativeLog) {
            $closed = Select-String -LiteralPath $nativeLog -Pattern 'CLOSED orderly=' -ErrorAction SilentlyContinue | Select-Object -Last 1
            if ($closed) { $summary.nativeClosedLine = $closed.Line.Trim(); $ended = $true; Say ('native session closed: ' + $summary.nativeClosedLine); Start-Sleep -Seconds 2; break }
        }
        if ($null -ne $stopSeen -and ((Get-Date) - $stopSeen).TotalSeconds -gt 40) { Say 'stop requested but no orderly close line within 40 s'; break }
    }
    if (-not $ended -and -not (Test-Path (Join-Path $dir 'host.json.failure.json')) -and (Get-Date) -ge $hardDeadline) { $summary.timedOut = $true; Say 'harness deadline reached' }
    Add-Receipt (ReadJson $handFile)
    # D-104: frame timing windows (120 submitted frames each) written by the native host. Facts only.
    if (Test-Path -LiteralPath $nativeLog) {
        $tl = @(Select-String -LiteralPath $nativeLog -Pattern " timing frames=" -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        $summary.timingWindows = $tl.Count
        $summary.timingLast = @($tl | Select-Object -Last 3)
        if ($tl.Count -gt 0) {
            $cyc = @($tl | ForEach-Object { if ($_ -match "cycleMs=([0-9.]+)") { [double]$Matches[1] } } | Sort-Object)
            if ($cyc.Count -gt 0) { $summary.timingMedianCycleMs = $cyc[[int][Math]::Floor($cyc.Count / 2)]; $summary.timingMedianFps = [Math]::Round(1000.0 / $summary.timingMedianCycleMs, 1) }
            # D-148: the submit split. fenceMs is the GPU completion wait inside submitMs, endFrameMs is the runtime call.
            # "- " patterns are exact so "prepToSubmitMs" never matches the "submitMs" pattern.
            # D-148b: the cycle decomposition. Same leading-space exact-match rule, so "prepBodyMs" never matches
            # "prepToSubmitMs". prepares is the Prepare-side population; the identity is cycleMs = gapMs + pumpMs +
            # waitMs + prepBodyMs + prepToSubmitMs + submitMs (logMs is a sub-span of those, never additive).
            foreach ($pair in @(@('prepToSubmitMs',' timingPreSubmitMidMs'), @('submitMs',' timingSubmitMidMs'), @('copyMs',' timingCopyMidMs'), @('fenceMs',' timingFenceMidMs'), @('endFrameMs',' timingEndFrameMidMs'), @('displayPeriodMs',' timingDisplayPeriodMidMs'), @('prepBodyMs',' timingPrepBodyMidMs'), @('handsMs',' timingHandsMidMs'), @('pumpMs',' timingPumpMidMs'), @('gapMs',' timingGapMidMs'), @('logMs',' timingLogMidMs'), @('prepares',' timingPreparesMidMs'))) {
                $vals = @($tl | ForEach-Object { if ($_ -match (" " + $pair[0] + "=([0-9.]+)")) { [double]$Matches[1] } } | Sort-Object)
                if ($vals.Count -gt 0) { $summary.($pair[1]) = $vals[[int][Math]::Floor($vals.Count / 2)] }
            }
        }
    }
    # D-147 (TOOL-078/080): our own per-phase cost windows (PERF, 45 submitted frames each) and the read-only AA/pipeline probe.
    $perfLog = $(if (Test-Path -LiteralPath (Join-Path $dir 'game.log')) { Join-Path $dir 'game.log' } else { $log })
    if (Test-Path -LiteralPath $perfLog) {
        $pf = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] PERF frames=' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        $summary.perfWindows = $pf.Count
        $summary.perfFirst = @($pf | Select-Object -First 3)
        $summary.perfLast = @($pf | Select-Object -Last 3)
        $summary.qualityAa = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] QUALITY_AA ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        $summary.monitorProbe = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] MONITOR_PROBE ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        # D-148b3: proves the opt-in overlap experiment was actually armed for this sitting (empty = the default path ran).
        $summary.deferredGpuWait = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] DEFERRED_GPU_WAIT ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        # D-149: proves the early-frame-prep opt-in was actually armed for this sitting (empty = the default path ran).
        $summary.prepareAhead = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] PREPARE_AHEAD ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
        # D-150: proves the engine quality dial was armed and shows before/after, so a quality change is never unstated.
        $summary.perfProfile = @(Select-String -LiteralPath $perfLog -Pattern '\[OPENXR_HOST\] PERF_PROFILE ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() })
    }

    # ---------------- observation (facts only; NEVER a gate pass)
    $L = $agg['left']; $R = $agg['right']
    $obs = if (-not $locked) { 'NEVER_REACHED_GAMEPLAY' }
        elseif ($summary.Contains('hostFailure')) { 'HOST_FAILED' }
        elseif ($agg['receipts'] -eq 0) { 'NO_HAND_RECEIPT' }
        elseif (-not $agg['controllersAvailableEver']) { 'CONTROLLERS_NOT_DELIVERED' }
        elseif (-not ($R['sampleActiveEver'] -or $L['sampleActiveEver'])) { 'CONTROLLER_POSES_NEVER_ACTIVE' }
        elseif (($R['statesSeen'] -notcontains 'Driving') -and ($L['statesSeen'] -notcontains 'Driving')) { 'CONTROLLERS_ACTIVE_BUT_NEVER_DRIVING' }
        elseif (($R['maxGameOverwrites'] -gt 0) -or ($L['maxGameOverwrites'] -gt 0)) { 'DRIVING_BUT_GAME_OVERWRITES_CONTAINER' }
        else { 'DRIVING_OBSERVED_AWAITING_USER_REPORT' }
    $summary.observation = $obs
    $summary.hand = [ordered]@{ controllersAvailableEver = $agg['controllersAvailableEver']; receipts = $agg['receipts']; nativeFailuresMax = $agg['nativeFailuresMax']
        lastError = $agg['lastError']; fireFailed = $agg['fireFailed']; fireError = $agg['fireError']
        fireEnabled = (-not $NoFire.IsPresent); scenes = $agg['scenes']; left = $L; right = $R }
    $summary.verdict = 'HEADSET_RUN_COMPLETE_NOT_VERIFIED'; $code = 0
} catch {
    if ($summary.verdict -eq 'INCONCLUSIVE') { $summary.verdict = 'FAIL' }
    $summary.error = $_.Exception.ToString(); Write-Host ('HOST STOP: ' + $_.Exception.Message)
} finally {
    if ($launched) {
        foreach ($p in @(GameProcesses)) { [void]$p.CloseMainWindow() }
        Start-Sleep -Seconds 5
        foreach ($p in @(GameProcesses)) { Stop-Process $p.Id -Force }
    }
    if (Test-Path -LiteralPath $log) { try { Copy-Item $log (Join-Path $dir 'game.log') } catch { $summary.cleanupError = 'log capture: ' + $_.Exception.Message } }
    if ($installed -or (Test-Path -LiteralPath (Join-Path $workspace 'journal.jsonl'))) {
        try {
            Say 'uninstalling (transactional) and checking stock'
            Installer 'Uninstall'
            & powershell -NoProfile -NonInteractive -File $checkStock > (Join-Path $dir 'stock.log')
            if ($LASTEXITCODE -ne 0) { throw 'stock differs' }
            $summary.stockVerified = $true
        } catch { $summary.cleanupError = $_.Exception.ToString(); $summary.verdict = 'FAIL'; $code = 2 }
    } elseif ($DryRun) { $summary.stockVerified = $null }
    $summary.runtimeBefore = $before; $summary.runtimeAfter = (Get-ItemProperty $runtimeKey).ActiveRuntime
    if ($summary.runtimeAfter -ne $before) { $summary.error = 'runtime changed'; $code = 2; $summary.verdict = 'FAIL' }
    $summary.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $dir 'run.json') -Encoding UTF8
    Write-Output ($summary.verdict + ' observation=' + $summary.observation + ' stock=' + $summary.stockVerified)
    exit $code
}
