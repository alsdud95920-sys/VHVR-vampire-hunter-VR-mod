# Read-only independent deployment check. No build, probe, installation or launch.
# ExpectDeferredGpuWait (D-148b3): the prepared config must match the sitting that is about to be run; the deferred
# submit fence is an opt-in experiment, so both "off by default" and a deliberately armed sitting are checkable.
# ExpectPrepareAhead (D-149): the same "the config must match the sitting" contract for the early-frame-prep experiment.
# ExpectPerfProfile (D-150): the engine quality dial must match the sitting that is about to run (default 'off').
# ExpectEyePreset (D-153): the eye render-size preset must match the sitting too, so an '-EyePreset high' arm cannot be
# prepared with the normal preset by accident.
# ExpectLeftTwistDeg/ExpectRightTwistDeg (D-153, TOOL-073): the per-hand weapon rest-twist dial of the prepared config.
# ExpectGameBackdropMode (D-155): which of the game's OWN backdrops the mod may touch. authored (default) = none; dim/hide are
# the legacy alpha-cap / cull policies.
# ExpectWorldMarkers/ExpectMarkerKinds/ExpectMarkerMaxDistance/ExpectMarkerMaxCount (D-157): the world-anchor target icon set
# (chests, potions, boss chests) of the prepared config, so a marker seen in the headset is attributable to these keys.
# BaselineConfig (BUG-207): the accepted configuration this sitting is compared against. Session artifacts are not carried in
# every tree, so the historical default can be absent; naming a prepared config.json of an accepted sitting is the explicit
# way to restore the drift guard instead of silently skipping it.
# BUG-206: the D-178 source recovery truncated this file - the payload loop and the sitting-key checks were gone, so it could
# not even be parsed. Restored from artifacts\recovery\dumps-tree (same lineage) minus the features this tree does not have
# (menu screen, VR probe retry, eye scale), and extended with the D-206 keys (TurretCycle, RenderTimingDiagnostics).

param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Tag, [switch]$ExpectDeferredGpuWait, [switch]$ExpectPrepareAhead, [ValidateSet('off','light','strong')][string]$ExpectPerfProfile = 'off', [ValidateSet('low','normal','high','custom')][string]$ExpectEyePreset = 'normal', [ValidateRange(-180,180)][double]$ExpectLeftTwistDeg = 0, [ValidateRange(-180,180)][double]$ExpectRightTwistDeg = 0, [ValidateSet('authored','dim','hide')][string]$ExpectGameBackdropMode = 'authored', [ValidateSet('off','on')][string]$ExpectWorldMarkers = 'on', [string]$ExpectMarkerKinds = 'CHEST,HEALING,POWER_UP,BOSS_CHEST', [ValidateRange(0,200)][double]$ExpectMarkerMaxDistance = 0, [ValidateRange(1,64)][int]$ExpectMarkerMaxCount = 32, [string]$BaselineConfig = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root ('artifacts\headset-d087\' + $Tag)
$game = if ($env:VHVR_GAME_ROOT) { $env:VHVR_GAME_ROOT } else { 'C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters' }
$code = 2
try {
    if (Get-Process -Name 'Vampire Hunters','OpenXrProbe' -ErrorAction SilentlyContinue) { throw 'Game/probe running.' }
    if (Test-Path -LiteralPath (Join-Path $dir 'run.json')) { throw 'Consumed tag; never prepare a completed sitting again.' }
    if (Test-Path -LiteralPath (Join-Path $dir 'retired.json')) { throw 'Retired preparation tag.' }
    if ([IO.File]::ReadAllText((Join-Path $dir 'install.exit.txt')).Trim() -cne '0') { throw 'Installer exit not zero.' }
    $install = Get-Content -LiteralPath (Join-Path $dir 'Install.log') -Raw | ConvertFrom-Json
    $workspace = Join-Path $dir 'transaction'
    if ($install.Status -cne 'ADDITIONS_INSTALLED' -or $install.GameLaunched -ne $false -or $install.Workspace -cne $workspace) { throw 'Invalid install receipt.' }
    . (Join-Path $root 'src\core\EntryTransaction.ps1')
    $build = Get-Content -LiteralPath (Join-Path $root 'config\supported-build.json') -Raw | ConvertFrom-Json
    Assert-EntryTargetBuild $game (Join-Path (Split-Path -Parent (Split-Path -Parent $game)) 'appmanifest_2206270.acf') $build
    $backupPath = Join-Path $root 'backups\20261006T104307Z-cdd080e1e50a4fda845ffa7c01158173\manifest.json'
    if ((Get-FileHash -LiteralPath $backupPath).Hash -cne '828B45CCC6D82AF32E2E4636B63D718E55FC188AFB5245F1B8DCC39A8F593A6F') { throw 'Backup pin differs.' }
    $stock = (Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json).Game
    $state = Get-TransactionJournal $game $workspace $stock
    if (-not $state.Installed -or $state.Recovered -or $state.Quarantine.Count -ne 0) { throw 'Not actively installed.' }
    if ([IO.File]::ReadAllText((Join-Path $root 'artifacts\transactions\current.txt')).Trim() -cne $workspace) { throw 'Wrong transaction pointer.' }
    foreach ($top in $state.Plan.Roots) { Assert-TransactionPayload $game $top $state.Plan.Files }
    Assert-TransactionStock $game $stock $state.Plan.Roots
    $pins = Get-Content -LiteralPath (Join-Path $dir 'payload-pins.json') -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath (Join-Path $root 'artifacts\verification.json')).Hash -cne $pins.verificationSha256) { throw 'Full validation receipt changed after pinning.' }
    foreach ($pair in @(
        @('BepInEx\plugins\VampireHuntersVR.Stage0.dll','src\core\bin\Release\net6.0\VampireHuntersVR.Stage0.dll',$pins.pluginSha256),
        @('BepInEx\plugins\VhvrNativeHost.dll','src\render\NativeHost\bin\VhvrNativeHost.dll',$pins.nativeSha256)
    )) {
        if ((Get-FileHash -LiteralPath (Join-Path $game $pair[0])).Hash -cne $pair[2] -or
            (Get-FileHash -LiteralPath (Join-Path $root $pair[1])).Hash -cne $pair[2]) { throw 'Build/live/pinned payload mismatch.' }
    }
    $configPath = Join-Path $dir 'config.json'
    $configHash = (Get-FileHash -LiteralPath $configPath).Hash
    if ($configHash -cne $pins.configSha256 -or $configHash -cne (Get-FileHash -LiteralPath (Join-Path $game 'BepInEx\config\vhvr-openxr-host.json')).Hash) { throw 'Config identity differs.' }
    $cfg = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    # BUG-207: the drift guard below compares against a config an accepted sitting actually ran with. Those artifacts are
    # per-session and are not carried in every tree, so a missing baseline is named instead of surfacing as a raw file error,
    # and -BaselineConfig is the explicit way to point the guard at another accepted prepared config. No silent skip.
    $baselinePath = if ($BaselineConfig) { $BaselineConfig } else { Join-Path $root 'artifacts\headset-d087\d130a\config.json' }
    if (-not (Test-Path -LiteralPath $baselinePath)) { throw ('Accepted-configuration baseline missing (BUG-207): ' + $baselinePath + ' is not in this tree; pass -BaselineConfig with the prepared config.json of an accepted sitting') }
    $prior = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    $baselineNames = @($prior.PSObject.Properties.Name)
    if ($cfg.Output -cne $dir) { throw 'Wrong output directory.' }
    $endpointFlag = $cfg.PSObject.Properties.Name -contains 'FlameEndpointVfx'
    if ($endpointFlag -and $cfg.FlameEndpointVfx -ne $true) { throw 'Prepared endpoint VFX flag must be true.' }
    $emitterFlag = $cfg.PSObject.Properties.Name -contains 'FlameEmitterPin'
    if ($emitterFlag -and $cfg.FlameEmitterPin -ne $true) { throw 'Prepared emitter pin flag must be true.' }
    # D-147: the opt-in raw dump (default false) and the per-eye resolution preset are two more keys on top of the flame flags.
    $captureFlag = $cfg.PSObject.Properties.Name -contains 'CaptureFirstFrames'
    if ($captureFlag -and $cfg.CaptureFirstFrames -ne $false) { throw 'Prepared frame-dump flag must be false.' }
    $presetKey = $cfg.PSObject.Properties.Name -contains 'EyePreset'
    if ($presetKey -and @('low','normal','high','custom') -notcontains [string]$cfg.EyePreset) { throw 'Prepared eye preset invalid.' }
    # D-148: the monitor-cost probe is measurement-only and must be off in a prepared install.
    $probeKey = $cfg.PSObject.Properties.Name -contains 'MonitorCostProbe'
    if ($probeKey -and $cfg.MonitorCostProbe -ne $false) { throw 'Prepared monitor-cost probe must be false.' }
    # D-148b3/D-149/D-150/D-153: every opt-in experiment and dial below is part of what a sitting IS, so a prepared
    # config that disagrees with the invocation about to run is rejected before the headset is worn.
    $deferredKey = $cfg.PSObject.Properties.Name -contains 'DeferredGpuWait'
    if ($deferredKey -and [bool]$cfg.DeferredGpuWait -ne $ExpectDeferredGpuWait.IsPresent) { throw 'Prepared deferred GPU wait flag differs from the requested sitting.' }
    $prepareAheadKey = $cfg.PSObject.Properties.Name -contains 'PrepareAhead'
    if ($prepareAheadKey -and [bool]$cfg.PrepareAhead -ne $ExpectPrepareAhead.IsPresent) { throw 'Prepared early-frame-prep flag differs from the requested sitting.' }
    $perfProfileKey = $cfg.PSObject.Properties.Name -contains 'PerfProfile'
    if ($perfProfileKey -and [string]$cfg.PerfProfile -cne $ExpectPerfProfile) { throw 'Prepared perf profile differs from the requested sitting.' }
    if ($presetKey -and [string]$cfg.EyePreset -cne $ExpectEyePreset) { throw 'Prepared eye preset differs from the requested sitting.' }
    $twistLeftKey = $cfg.PSObject.Properties.Name -contains 'LeftWeaponTwistDeg'
    $twistRightKey = $cfg.PSObject.Properties.Name -contains 'RightWeaponTwistDeg'
    if ($twistLeftKey -ne $twistRightKey) { throw 'Only one weapon twist key is present; the dial is per-hand and always written as a pair.' }
    if ($twistLeftKey -and ([Math]::Abs([double]$cfg.LeftWeaponTwistDeg - $ExpectLeftTwistDeg) -gt 1e-4 -or
        [Math]::Abs([double]$cfg.RightWeaponTwistDeg - $ExpectRightTwistDeg) -gt 1e-4)) { throw 'Prepared weapon twist dial differs from the requested sitting.' }
    # D-155: the backdrop policy is part of what a sitting is (a restored level-up background must be attributable to this key),
    # so a prepared install that disagrees with the sitting about to run is rejected before the headset is worn.
    $gameBackdropKey = $cfg.PSObject.Properties.Name -contains 'GameBackdropMode'
    if ($gameBackdropKey -and [string]$cfg.GameBackdropMode -cne $ExpectGameBackdropMode) { throw 'Prepared game backdrop mode differs from the requested sitting.' }
    # D-157: the world-anchor marker set is part of what a sitting is too - a marker seen in the headset, or one missing, has
    # to be attributable to these keys instead of argued from memory.
    $markersKey = $cfg.PSObject.Properties.Name -contains 'WorldMarkers'
    if ($markersKey -and [string]$cfg.WorldMarkers -cne $ExpectWorldMarkers) { throw 'Prepared world markers mode differs from the requested sitting.' }
    $markerKindsKey = $cfg.PSObject.Properties.Name -contains 'MarkerKinds'
    if ($markerKindsKey -and [string]$cfg.MarkerKinds -cne $ExpectMarkerKinds) { throw 'Prepared marker kinds differ from the requested sitting.' }
    $markerDistanceKey = $cfg.PSObject.Properties.Name -contains 'MarkerMaxDistance'
    if ($markerDistanceKey -and [Math]::Abs([double]$cfg.MarkerMaxDistance - $ExpectMarkerMaxDistance) -gt 1e-4) { throw 'Prepared marker max distance differs from the requested sitting.' }
    $markerCountKey = $cfg.PSObject.Properties.Name -contains 'MarkerMaxCount'
    if ($markerCountKey -and [int]$cfg.MarkerMaxCount -ne $ExpectMarkerMaxCount) { throw 'Prepared marker max count differs from the requested sitting.' }
    # D-206/BUG-204: the engineer build-mode step surface is part of the path a sitting tests, so a prepared install has to
    # carry a legal value; D-206 also restores the D-136/D-138 read-only render-timing probe (BUG-205), so its key is back
    # and both of its values are legitimate (it patches nothing but its own log) - only the type is asserted.
    $turretKey = $cfg.PSObject.Properties.Name -contains 'TurretCycle'
    if ($turretKey -and @('Stick','Dpad','Special','Off') -notcontains [string]$cfg.TurretCycle) { throw 'Prepared turret cycle invalid.' }
    $renderTimingKey = $cfg.PSObject.Properties.Name -contains 'RenderTimingDiagnostics'
    if ($renderTimingKey -and $cfg.RenderTimingDiagnostics -isnot [bool]) { throw 'Prepared render timing flag must be boolean.' }
    foreach ($property in $prior.PSObject.Properties) {
        # D-145: Runtime is empty (auto-detected) in the new payload. D-148: RenderFrames changed default 2 -> 1 by
        # measurement, so the pre-D-148 accepted config@d130a is expected to differ there and only there.
        if ($property.Name -in @('Output','Runtime','RenderFrames')) { continue }
        # BUG-207: a baseline key the payload no longer writes is drift as well, and under StrictMode a plain read of the
        # missing property would raise a raw error instead of naming the accepted key that disappeared.
        if ($cfg.PSObject.Properties.Name -notcontains $property.Name) { throw ('Accepted configuration dropped: ' + $property.Name) }
        if ([string]$cfg.($property.Name) -cne [string]$property.Value) { throw ('Accepted configuration changed: ' + $property.Name) }
    }
    # BUG-207/D-206: the expected key count is derived instead of hard-coded - a whitelisted sitting key counts as an extra
    # only when the baseline does not already carry it. The historical d130a baseline predates every one of them, so this is
    # the arithmetic the old "+1 per flag" line meant, while a newer baseline - or an arm that legitimately drops a restored
    # key, e.g. -NoRenderTimingDiagnostics - no longer produces a false 'Unexpected config keys.' The guard still fails
    # closed: a key that the baseline does not have and that is not in this list makes the counts disagree.
    $sittingKeys = @('FlameEndpointVfx','FlameEmitterPin','CaptureFirstFrames','EyePreset','MonitorCostProbe','DeferredGpuWait','PrepareAhead','PerfProfile','LeftWeaponTwistDeg','RightWeaponTwistDeg','GameBackdropMode','WorldMarkers','MarkerKinds','MarkerMaxDistance','MarkerMaxCount','RenderTimingDiagnostics','TurretCycle')
    $expectedExtras = 0
    foreach ($name in $sittingKeys) { if (($cfg.PSObject.Properties.Name -contains $name) -and ($baselineNames -notcontains $name)) { $expectedExtras++ } }
    if (@($cfg.PSObject.Properties).Count -ne @($prior.PSObject.Properties).Count + $expectedExtras) { throw 'Unexpected config keys.' }
    $runtime = (Get-ItemProperty 'HKLM:\SOFTWARE\Khronos\OpenXR\1').ActiveRuntime
    if ($runtime -cne $pins.runtimeBefore -or ($cfg.Runtime -and -not (Test-Path -LiteralPath $cfg.Runtime))) { throw 'Runtime changed/missing.' }
    foreach ($f in @('tests\headset-handaim.ps1','src\render\OpenXrProbe\bin\Debug\net10.0\OpenXrProbe.exe','artifacts\devtools\Check-Stock.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $f))) { throw ('Missing prerequisite ' + $f) }
    }
    if (-not (Get-Process -Name '*VirtualDesktop*' -ErrorAction SilentlyContinue)) { Write-Output 'NOTE: no Virtual Desktop streamer process; any registered OpenXR runtime is allowed after D-145.' }
    if (Get-Process -Name 'Vampire Hunters','OpenXrProbe' -ErrorAction SilentlyContinue) { throw 'Game/probe appeared.' }
    $receipt = [ordered]@{
        tag = $Tag; status = 'READY_PREINSTALLED_NOT_LAUNCHED'; verifiedUtc = [DateTime]::UtcNow.ToString('o')
        transaction = $workspace; filesVerified = @($state.Plan.Files).Count; stockOutsideOwnedRootsVerified = $true
        stockHashesAndWriteTimesVerified = $true; pluginSha256 = $pins.pluginSha256; nativeSha256 = $pins.nativeSha256
        configSha256 = $configHash; runtimeUnchanged = $true; gameLaunched = $false
        baselineConfig = $baselinePath; expectedConfigKeys = @($prior.PSObject.Properties).Count + $expectedExtras
        launchScript = Join-Path $root 'tests\headset-handaim.ps1'; launchArguments = @('-Tag',$Tag,'-DisableVSync','-UsePreinstalled')
        flameEndpointVfx = $endpointFlag
        flameEmitterPin = $emitterFlag
        eyePreset = if ($presetKey) { [string]$cfg.EyePreset } else { 'absent' }
        perfProfile = if ($perfProfileKey) { [string]$cfg.PerfProfile } else { 'absent' }
        deferredGpuWait = if ($deferredKey) { [bool]$cfg.DeferredGpuWait } else { 'absent' }
        prepareAhead = if ($prepareAheadKey) { [bool]$cfg.PrepareAhead } else { 'absent' }
        leftWeaponTwistDegrees = if ($twistLeftKey) { [double]$cfg.LeftWeaponTwistDeg } else { $null }
        rightWeaponTwistDegrees = if ($twistRightKey) { [double]$cfg.RightWeaponTwistDeg } else { $null }
        gameBackdropMode = if ($gameBackdropKey) { [string]$cfg.GameBackdropMode } else { 'absent' }
        worldMarkers = if ($markersKey) { [string]$cfg.WorldMarkers } else { 'absent' }
        markerKinds = if ($markerKindsKey) { [string]$cfg.MarkerKinds } else { 'absent' }
        markerMaxDistance = if ($markerDistanceKey) { [double]$cfg.MarkerMaxDistance } else { $null }
        markerMaxCount = if ($markerCountKey) { [int]$cfg.MarkerMaxCount } else { $null }
        turretCycle = if ($turretKey) { [string]$cfg.TurretCycle } else { 'absent' }
        renderTimingDiagnostics = if ($renderTimingKey) { [bool]$cfg.RenderTimingDiagnostics } else { 'absent' }
        gatesPassed = 'NONE; visual acceptance/root cause pending; offline/build/install checks only'
    }
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dir 'prepare.json') -Encoding UTF8
    $receipt | ConvertTo-Json -Depth 5
    $code = 0
# BUG-206: the receipt write used to assume the tag directory already exists, so a run that failed before the directory
# check (missing/typo'd tag) died in `finally` with a path error and exit 1 instead of the 2 that means "not ready".
} catch { Write-Output ('NOT_READY: ' + $_.Exception.ToString()) }
finally { try { [IO.File]::WriteAllText((Join-Path $dir 'verify.exit.txt'),[string]$code) } catch { Write-Output ('NOTE: no exit receipt written: ' + $_.Exception.Message) } }
exit $code
