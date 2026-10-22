#Requires -Version 5.1
# Vampire Hunters VR - installer.
# Copies the pinned payload into the game folder and writes this mod's own config.
# It never touches Steam settings, saves, or stock game files, and it never launches the game.
[CmdletBinding()]
param([string]$GameRoot = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$here = $PSScriptRoot
$payload = Join-Path $here 'payload'
$manifestName = 'vhvr-uninstall.json'
$ownedRoots = @('BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt', 'ModNotes.txt')

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

function Find-GameRoot {
    # No Steam library scanning on purpose: this must never touch a game folder the
    # player did not point at. Only the folder this script sits in (and its parent,
    # for the "extract the zip into the game folder" layout) are considered.
    if ($GameRoot) {
        $explicit = [IO.Path]::GetFullPath($GameRoot)
        if (-not (Test-Path -LiteralPath (Join-Path $explicit 'Vampire Hunters.exe'))) {
            throw ("No 'Vampire Hunters.exe' under " + $explicit)
        }
        return $explicit
    }
    $candidates = @($here, (Split-Path -Parent $here))
    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        if (Test-Path -LiteralPath (Join-Path $candidate 'Vampire Hunters.exe')) { return ([IO.Path]::GetFullPath($candidate)) }
    }
    return $null
}

if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'The payload folder is missing next to install.ps1.' }

$game = Find-GameRoot
if (-not $game) {
    throw "Vampire Hunters.exe was not found here or one folder above. Extract this folder into the game folder (the one that contains 'Vampire Hunters.exe'), or copy the contents of this folder directly into it, and run install.bat again."
}
if (@(Get-Process -Name 'Vampire Hunters' -ErrorAction SilentlyContinue).Count) {
    throw 'The game is running. Close it and run install.bat again.'
}
if (Test-Path -LiteralPath (Join-Path $game $manifestName)) {
    throw 'This mod is already installed. Run uninstall.bat first, then install.bat again.'
}
foreach ($owned in @('BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini')) {
    if (Test-Path -LiteralPath (Join-Path $game $owned)) {
        throw ("The game folder already contains " + $owned + ". This installer never merges with an existing BepInEx install; remove it first.")
    }
}

$probe = Join-Path $game '.vhvr-write-probe'
try {
    [IO.File]::WriteAllText($probe, 'probe')
    Remove-Item -LiteralPath $probe -Force
} catch {
    throw ("Cannot write to " + $game + ". Right-click install.bat and choose 'Run as administrator'.")
}

# Any registered OpenXR runtime is accepted (D-145). The config leaves Runtime empty, so the native host
# adopts whatever the machine has registered; installing without one is still allowed but warned about.
$runtime = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Khronos\OpenXR\1' -ErrorAction SilentlyContinue).ActiveRuntime
$runtimeReady = [bool]($runtime -and (Test-Path -LiteralPath $runtime))

$output = Join-Path $env:LOCALAPPDATA 'VampireHuntersVR\run'
[IO.Directory]::CreateDirectory($output) | Out-Null

$installed = New-Object 'Collections.Generic.List[object]'
foreach ($file in @(Get-ChildItem -LiteralPath $payload -Recurse -File)) {
    $relative = $file.FullName.Substring($payload.Length + 1)
    $destination = Join-Path $game $relative
    $folder = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $folder)) { [IO.Directory]::CreateDirectory($folder) | Out-Null }
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    $installed.Add([pscustomobject]@{ path = $relative; bytes = [long]$file.Length; sha256 = (Get-Sha256 $file.FullName) })
}

$config = [ordered]@{
    Enabled = $true
    Output = $output
    Runtime = ''
    Frames = 120
    GameplayOnly = $true
    # D-202: controller pose shares the headset frame; snap-turn remains unavailable in this build.
    HandPoseFrame = 'Head'
    # D-206: default turret-cycle control surface.
    TurretCycle = 'Stick'
    # D-194/D-200: the release must enter VR at MainMenu_2, never in the intro scene. MenuFirst defers gameplay
    # hand/weapon hooks until a stage camera appears; OpenXrHost.Initialize installs them on that transition.
    MenuFirst = $true
    # D-200: VR must not start inside the intro. v0.9.3's readiness gate alone passed at 0.6 s in scene "Intro"
    # (its main camera satisfied it); the game then loaded MainMenu_2, destroyed that camera, and the host kept
    # submitting from a dead source - the black headset screen. StartupSkipScenes lists the scenes whose camera
    # must never become the eye source (comma-separated, case-insensitive) and StartupSettleSeconds makes the
    # gate wait until the active scene has been stable that long, so a scene that is still being loaded over can
    # never be adopted. VR therefore begins once the main menu is actually up, inside the same
    # [gameplayWaitSeconds] budget. Set StartupSkipScenes to '' to get the pre-D-200 behaviour back.
    StartupSkipScenes = 'Intro'
    StartupSettleSeconds = 1.0
    GameplayWaitSeconds = 600
    HandAim = $true
    HandFire = $true
    StockFireCadence = $true
    AutonomousHeadAim = $true
    HandUi = $true
    HideHudBackdrop = $true
    # D-155: authored = the game's OWN backdrops (level-up black background, menus, chest / boss-chest screens) are left exactly
    # as the game authored them; only the exact HUD Bottom Shadow is still removed. dim / hide are the legacy alpha-cap / cull
    # policies and are kept for an A/B comparison.
    GameBackdropMode = 'authored'
    # D-157/D-161: world-anchored target markers for the things the player must find. off|on; the kinds are the build's exact
    # PickUp.type enum names plus the reserved token CHEST (the game's RewardBox = chest / boss chest). MarkerMaxDistance is
    # metres, 0 = no limit (every chest and potion in the level is marked); MarkerMaxCount caps how many arrows are drawn at once.
    WorldMarkers = 'on'
    MarkerKinds = 'CHEST,HEALING,POWER_UP,BOSS_CHEST'
    MarkerMaxDistance = 0
    MarkerMaxCount = 32
    UiVerticalOffset = -0.25
    HandInput = $true
    HandHaptic = $true
    HandShot = $true
    HandShotTracer = $false
    # D-211/D-213: right-stick turn - Off (0), 30 deg (default), 45 deg, or Smooth (TurnMode = 'Smooth').
    SnapTurnDegrees = 30
    TurnMode = 'Snap'
    SmoothTurnDegreesPerSecond = 120
    Headset = $true
    HeadsetSeconds = 0
    # D-171: base size is PER EYE, 1920x1080 (= 3840x2160 both eyes). EyePreset low 0.8 / normal 1.0 / high 1.2 / custom (uses EyeScale 0.5-2.0).
    # Change it with VhvrSettings.exe (next to install.bat), or edit EyePreset / EyeScale here with the game closed.
    EyeWidth = 1920
    EyeHeight = 1080
    EyePreset = 'normal'
    EyeScale = 1.0
    RenderFrames = 1
    # D-153: promoted to the shipped default after D-152 measured 80.5 fps (12.42 ms) with both on together, vs the
    # 54.1 fps baseline without them. The player can still turn either one off in this file; an absent key stays off.
    PrepareAhead = $true
    DeferredGpuWait = $true
    # D-153 (TOOL-073): per-hand weapon rest-twist dial, degrees about the muzzle axis. 0 = the authored rest pose.
    LeftWeaponTwistDeg = 0
    RightWeaponTwistDeg = 0
    DisableVSync = $true
    # D-104: anisotropic filtering on eye texture sampling. v0.9 ships it ON (the user asked for it; cost is negligible).
    # Change it with VhvrSettings.exe; the host reads the same "Anisotropic" key.
    Anisotropic = $true
    # D-186-FovScale: per-axis render-FOV scales (1.0 = pass-through). Lower values narrow the rendered FOV so the
    # GPU draws fewer pixels. FovScaleX scales the horizontal frustum (angleLeft / angleRight) and FovScaleY scales
    # the vertical frustum (angleUp / angleDown). Valid range per axis is 0.50-1.00 in 0.05 steps. v0.9 ships
    # FovScaleX = 0.85 (the user asked for a slightly narrowed horizontal render) and FovScaleY = 1.0 (unchanged).
    # Change them with VhvrSettings.exe; an absent key falls back to 1.0.
    FovScaleX = 0.85
    FovScaleY = 1.0
    CaptureFirstFrames = $false
    MonitorCostProbe = $false
    FlameEndpointVfx = $true
    FlameEmitterPin = $true
    # D-187: the "GraphicsQuality" key is gone. The host uses the game's own saved QualitySettings level.
    # D-186: per-eye render-texture MSAA (key "EyeMsaa", the same file). 1 = off, 2/4/8 = that many samples.
    # 4 is the value the host hard-coded before this key existed, so writing 4 here matches the shipped look.
    # Change it with VhvrSettings.exe; the host refuses anything outside 1/2/4/8 and keeps 4 in that case.
    EyeMsaa = 4
    # In-VR UI scale (key "uiScale", lowercase), 0.50-3.00, 1.00 = shipped size. The extras plugin reads this exact key
    # from this file; an absent key falls back to its own default. Change it with VhvrSettings.exe.
    uiScale = 1.0
}
$configPath = Join-Path $game 'BepInEx\config\vhvr-openxr-host.json'
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
# The host config is ours, but the player is expected to edit it: mark it editable so
# uninstall still removes it without refusing on a hash mismatch.
$installed.Add([pscustomobject]@{
    path = 'BepInEx\config\vhvr-openxr-host.json'
    bytes = [long](Get-Item -LiteralPath $configPath).Length
    sha256 = (Get-Sha256 $configPath)
    editable = $true
})

$manifest = [ordered]@{
    mod = 'VampireHuntersVR'
    version = '0.9.6'
    gameRoot = $game
    installedUtc = [DateTime]::UtcNow.ToString('o')
    outputDirectory = $output
    ownedRoots = $ownedRoots
    generatedRoots = @('BepInEx\interop', 'BepInEx\cache', 'BepInEx\unity-libs')
    files = @($installed | Sort-Object path)
}
[IO.File]::WriteAllText((Join-Path $game $manifestName), ($manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
# Remember the game root so uninstall.bat works even when this folder is not inside the game folder.
[IO.File]::WriteAllText((Join-Path $here 'vhvr-game-root.txt'), $game, (New-Object Text.UTF8Encoding($false)))

Write-Output ("INSTALLED: {0} files -> {1}" -f $installed.Count, $game)
Write-Output ("LOGS: {0}" -f $output)
if ($runtimeReady) { Write-Output ("OPENXR RUNTIME: {0} (adopted automatically)" -f $runtime) }
else { Write-Output 'WARNING: no OpenXR runtime is registered (HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime). Install and activate one (Virtual Desktop is the tested one) before playing.' }
if ($runtimeReady -and $runtime -inotlike '*virtualdesktop*') {
    Write-Output 'NOTE: only Virtual Desktop has been tested with this release. Other runtimes are allowed but unverified.'
}
Write-Output 'Next: start the game and put the headset on. VR starts in the main menu.'
