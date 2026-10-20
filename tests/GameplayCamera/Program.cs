using VampireHuntersVR.Stage0;

var valid = new GameplayCameraCandidate("Maze_Small", "Main Camera", true, true, true, true, false, true, true, true);
int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}
Check("active-player-gameplay-camera", GameplayCameraPolicy.Accept(valid));
foreach (string scene in new[] { "MainMenu_2", "mainmenu", "INTRO", "", " " })
    Check("reject-scene-" + scene, !GameplayCameraPolicy.Accept(valid with { Scene = scene }));
foreach (string camera in new[] { "ArenaCamera", "Mini Map Camera", "VHVR_RuntimeEye_0", "Main camera" })
    Check("reject-camera-" + camera, !GameplayCameraPolicy.Accept(valid with { CameraName = camera }));
Check("reject-not-main", !GameplayCameraPolicy.Accept(valid with { IsMain = false }));
Check("reject-inactive-camera", !GameplayCameraPolicy.Accept(valid with { CameraActive = false }));
Check("reject-disabled-camera", !GameplayCameraPolicy.Accept(valid with { CameraEnabled = false }));
Check("reject-orthographic", !GameplayCameraPolicy.Accept(valid with { Perspective = false }));
Check("reject-render-target", !GameplayCameraPolicy.Accept(valid with { HasTarget = true }));
Check("reject-inactive-player", !GameplayCameraPolicy.Accept(valid with { PlayerActive = false }));
Check("reject-disabled-player", !GameplayCameraPolicy.Accept(valid with { PlayerEnabled = false }));
Check("reject-cutscene-ancestor", !GameplayCameraPolicy.Accept(valid with { DirectPlayerChild = false }));
Check("empty-selection", GameplayCameraPolicy.Select(Array.Empty<GameplayCameraCandidate>()) == -1);
Check("one-selection", GameplayCameraPolicy.Select(new[] { valid }) == 0);
Check("reject-ambiguity", GameplayCameraPolicy.Select(new[] { valid, valid }) == -1);
Check("skip-invalid-first", GameplayCameraPolicy.Select(new[] { valid with { HasTarget = true }, valid }) == 1);
Check("invalid-only", GameplayCameraPolicy.Select(new[] { valid with { Scene = "MainMenu_2" } }) == -1);
// Source guards are deliberately labeled static checks, not live Unity execution.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string host = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHost.cs"));
// D-181 (TOOL-095): the host's option properties moved to OpenXrHostConfig.cs during the D-178 source recovery, and the
// recovered host is a decompile whose expressions are spelled differently from the original hand-written source. The
// guards below look for the SAME BEHAVIOUR in the current spelling; none of them was dropped or loosened to "always true".
string hostConfig = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHostConfig.cs"));
string hostAll = host + "\n" + hostConfig;
string runner = File.ReadAllText(Path.Combine(root, "tests", "openxr-game-host.ps1"));
Check("source-gameplay-opt-in", hostAll.Contains("public bool GameplayOnly { get; set; }") && host.Contains("GameplayCameraPolicy.Select(candidates)"));
Check("source-wait-watchdog", host.Contains("gameplay camera wait expired; no menu fallback") && host.Contains("DateTime.UtcNow - sessionStarted"));
Check("source-camera-loss-guard", host.Contains("((Component)source).transform.parent != (Object)(object)((Component)player).transform") && host.Contains("gameplay camera/scene ownership lost; no menu fallback"));
// D-195 (stale guard repaired by D-198): the eye composition used to read `val + anchorRotation * new Vector3(...)`.
// That double axis-flip is exactly the TOOL-097 regression the D-195 fix removed, so the literal is gone on purpose and
// this guard now asserts the SAME BEHAVIOUR in the current spelling: the eye source still follows the player's
// translation delta, and the runtime rotation is still composed WITHOUT re-applying anchorRotation (D-195 preserved).
Check("source-player-translation", host.Contains("((Component)player).transform.position - playerAnchor") && host.Contains("Vector3 val2 = val + (snapViewYaw == 0f ? eyeOffset : viewYaw * eyeOffset);"));
Check("source-no-double-axis-flip", host.Contains("Quaternion val3 = (snapViewYaw == 0f ? eyeRotation : viewYaw * eyeRotation);") && !host.Contains("anchorRotation * new Vector3("));
Check("source-readonly-muzzle-inventory", host.Contains("muzzlePath = (((Object)(object)muzzle_flash_spawn == (Object)null) ? null : ObjectPath(muzzle_flash_spawn))") && !host.Contains(".shoot_forward(") && !host.Contains(".fire("));
Check("runner-no-exhausted-motion", runner.Contains("if($Gameplay -and $Motion){throw") && runner.Contains("$summary.m4bPass=$false"));
Check("runner-exact-transfer", runner.Contains("runtime differs from exact Y-flipped source") && runner.Contains("$r.gameplayIdentity.scene -ne $r.scene"));
// D-198: the host must never go back to a fixed warm-up before it will even look at the game. The old
// `Time.unscaledTime < 15f` gate is documented by Unity as "seconds since the start of the game", so it delayed
// VR by a flat 15 s on every machine regardless of how fast the menu came up. The replacement must be a
// per-frame readiness check with a bounded, evidenced wait, and the receipt must carry how long it waited.
Check("source-no-fixed-warmup", !host.Contains("Time.unscaledTime < 15f") && !host.Contains("unscaledTime < 15f"));
Check("source-readiness-gate", host.Contains("private bool StartupReady()") && host.Contains("if (!configured && !StartupReady())"));
Check("source-bounded-startup-wait", host.Contains("private bool ReportStartupWait(string reason)") && host.Contains("startup wait expired after ") && host.Contains("TimeSpan.FromSeconds(Config.GameplayWaitSeconds)"));
Check("source-startup-wait-receipt", host.Contains("WAITING_FOR_STARTUP_CAMERA") && host.Contains("startupReadySeconds = startupReadySeconds,") && host.Contains("STARTUP_READY elapsed="));
// D-200: 0.9.3 (D-198) started OpenXR on the first frame a main camera existed, which on this game is the
// *intro* scene; the game then loaded the menu, destroyed that camera, and the host kept submitting from a dead
// source (black headset, window frames frozen). Three properties are pinned here: the gate skips named scenes,
// it waits for the active scene to settle, and the menu branch re-adopts the camera BEFORE its null-source early
// return - the return that made the D-181 adoption unreachable in the failing run.
Check("source-startup-skips-intro",
    hostAll.Contains("public string StartupSkipScenes { get; set; } = \"Intro\";") &&
    host.Contains("private bool StartupSceneSkipped(string scene)") &&
    host.Contains("if (StartupSceneSkipped(scene))") &&
    host.Contains("intro scene '\" + scene + \"' (D-200: waiting for the main menu)") &&
    host.Contains("STARTUP_SKIP scene='"));
Check("source-startup-scene-settle",
    hostAll.Contains("public double StartupSettleSeconds { get; set; } = 1.0;") &&
    host.Contains("private bool StartupSceneSettled(string scene)") &&
    host.Contains("if (!StartupSceneSettled(scene))") &&
    host.Contains("not settled yet (D-200 settle gate)") &&
    host.Contains("invalid startup settle seconds: 0..10") &&
    host.Contains("invalid startup skip scene list: at most 256 characters"));
string hostFlat = host.Replace("\r\n", "\n");
int menuBranchAt = hostFlat.IndexOf("TryEnterGameplay((Object)(object)source == (Object)null);", StringComparison.Ordinal);
const int menuBranchWindow = 2000;
string menuBranch = (menuBranchAt >= 0 && menuBranchAt + menuBranchWindow <= hostFlat.Length)
    ? hostFlat.Substring(menuBranchAt, menuBranchWindow)
    : "";
int adoptAt = menuBranch.IndexOf("AdoptMenuCameraIfNeeded();", StringComparison.Ordinal);
int earlyReturnAt = menuBranch.IndexOf("if ((Object)(object)source == (Object)null)", StringComparison.Ordinal);
// The first null-source return after the gameplay scan must come AFTER the adoption, not before it.
Check("source-menu-adopt-before-return",
    menuBranchAt >= 0 && adoptAt >= 0 && earlyReturnAt > adoptAt &&
    menuBranch.Contains("D-200: adopt BEFORE the null-source return"));
Console.WriteLine($"RESULT {passed} gameplay camera policy fixtures passed; identity math only, no game/VR gate");