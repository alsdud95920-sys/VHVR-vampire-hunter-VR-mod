using VampireHuntersVR.Interaction;
using VampireHuntersVR.Render;

namespace VampireHuntersVR.Stage0;

public sealed class OpenXrHostConfig
{
	public bool Enabled { get; set; }

	public string Output { get; set; } = "";

	public string Runtime { get; set; } = "";

	public int Frames { get; set; } = 120;

	public bool GameplayOnly { get; set; }

	public int GameplayWaitSeconds { get; set; } = 180;

	public bool HandAim { get; set; }

	// D-178: menu-first mode. The host starts in the game's main menu (no gameplay camera yet) instead of
	// waiting for one, and installs hand/weapon integration the moment a stage actually starts.
	public bool MenuFirst { get; set; }

	// D-200: startup scene gate. D-198 removed the fixed 15 s warm-up and started OpenXR on the first frame a
	// main camera existed - which on this game is the *intro* scene. The game then loaded the real menu, the
	// adopted camera was destroyed with that scene, and the host kept submitting from a dead source (black
	// headset, frozen window frames) while the XR loop ran on. StartupSkipScenes is a comma-separated,
	// case-insensitive list of scene names whose camera must never become the eye source; the host waits
	// (inside the same GameplayWaitSeconds budget) until the game leaves them. Empty/absent = pre-D-200.
	public string StartupSkipScenes { get; set; } = "Intro";

	// D-200: generic guard for the same failure on a scene we cannot name in advance. The active scene must have
	// been unchanged for this many seconds before its camera is adopted, so a scene that has just started (and
	// may still be replaced by a load) can never become the eye source. 0 = adopt immediately (pre-D-200).
	public double StartupSettleSeconds { get; set; } = 1.0;

	// D-202 (BUG-202): the frame the tracked hand pose is composed in - the choice the eye cameras already made in
	// D-195 and the hands were left out of. "Head" (the default, and the fix) composes the hand/aim pose in the same
	// frame the eyes are composed in (identity anchor rotation), so a weapon and its aim ray point where the
	// controller points and where the player looks. "Anchor" reproduces the pre-D-202 behaviour (the game camera
	// rotation - i.e. the player's yaw - multiplied into the pose) so one sitting can A/B the fix with no rebuild.
	// The 0.9.4 report ("the hand is bent 90 deg to the right") is exactly that anchor yaw: this game's camera yaw is
	// +-90 deg, and the eyes no longer carry it. Anything other than these two values is refused at startup - a typo
	// must never silently pick a frame, which is how the hands ended up 90 deg off the view in the first place.
	public string HandPoseFrame { get; set; } = "Head";

	public bool HandFire { get; set; }

	// D-206 (BUG-204): the engineer's turret build mode. While the game itself reports PlayerController.is_turret_mode
	// the left grip steps the blueprint back, the right grip steps it forward (one step per grip released outside a
	// chord) and both grips together hold the game's Fire action, which the game's own text names as the build confirm
	// (skill_desc_engineer_1: "Press {action=Fire} to build the turret"). Outside build mode the grips stay unbound, as
	// D-057 decided. TurretCycle picks the game surface one step is sent on, because the game's own text (Special
	// Ability = "switch blueprints") and its UI action map (Navigate = <Gamepad>/leftStick and <Gamepad>/dpad) do not
	// have to agree and one sitting can A/B them without a rebuild:
	//   "Stick"   = the pad's left stick (the game's Navigate leftStick composite) + the navigationDirection field.
	//   "Dpad"    = <Gamepad>/dpad/left,right + navigationDirection. The Arena map also binds dpad/right to Manual
	//               Level Up, so watch for that side effect.
	//   "Special" = pulse Special Ability (<Gamepad>/buttonEast), the switch the game's own text documents - forward
	//               only, so either grip steps forward.
	//   "Off"     = the pre-D-206 map exactly (grips unbound, D-057).
	// An unknown value is refused at startup rather than silently choosing a surface.
	public string TurretCycle { get; set; } = "Stick";

	public bool StockFireCadence { get; set; } = true;

	public bool AutonomousHeadAim { get; set; } = true;

	// D-136/D-138, restored by the D-206 verification (BUG-205): the read-only render-timing probe was orphaned by the
	// D-178 source recovery - its class, its bounded writer and its fixtures survived, but nothing ever installed it, and
	// the D-136 fixture "flag-default-and-guard" was left failing. Default ON as D-136 shipped it: it patches
	// Camera.FireOnPreCull/FireOnPreRender read-only, writes only <output>/render-timing.jsonl, stops itself after 180 s
	// and never moves a pose, animation, particle or camera. Harness flag -NoRenderTimingDiagnostics.
	public bool RenderTimingDiagnostics { get; set; } = true;

	public bool HandUi { get; set; }

	public bool HideHudBackdrop { get; set; } = true;

	public string GameBackdropMode { get; set; } = "authored";

	public string WorldMarkers { get; set; } = "on";

	public string MarkerKinds { get; set; } = "CHEST,HEALING,POWER_UP,BOSS_CHEST";

	public float MarkerMaxDistance { get; set; }

	public int MarkerMaxCount { get; set; } = 32;

	public float UiVerticalOffset { get; set; } = -0.25f;

	public bool HandInput { get; set; }

	public bool HandShot { get; set; }

	public bool HandShotTracer { get; set; }

	public float SnapTurnDegrees { get; set; } = 30f;

	// D-213: "Snap" (default: uses SnapTurnDegrees 0/30/45) or "Smooth" (continuous rotation while the right stick is held).
	public string TurnMode { get; set; } = "Snap";

	public float SmoothTurnDegreesPerSecond { get; set; } = 120f;

	public bool HandHaptic { get; set; } = true;

	public float HapticAmplitude { get; set; } = 0.4f;

	public int HapticMilliseconds { get; set; } = 60;

	public bool Headset { get; set; }

	public int HeadsetSeconds { get; set; } = 600;

	public int EyeWidth { get; set; } = 1920;

	public int EyeHeight { get; set; } = 1080;

	public string EyePreset { get; set; } = "normal";

	public double EyeScale { get; set; } = 1.0;

	public int RenderFrames { get; set; } = 1;

	public bool DisableVSync { get; set; }

	public bool Anisotropic { get; set; }

	public string PerfProfile { get; set; } = "off";

	public bool CaptureFirstFrames { get; set; }

	public bool MonitorCostProbe { get; set; }

	public bool DeferredGpuWait { get; set; }

	public bool PrepareAhead { get; set; }

	public float LeftWeaponTwistDeg { get; set; }

	public float RightWeaponTwistDeg { get; set; }

	public bool FlameEndpointVfx { get; set; } = true;

	public bool FlameEmitterPin { get; set; } = true;

	// D-187: the host no longer exposes Unity quality level. The game's own saved QualitySettings choice is used
	// as-is, so the mod never second-guesses a value the player already picked. The matching key, the matching
	// POCO field and the matching settings program row were all removed (the legacy key in old JSON files is
	// silently ignored by System.Text.Json).
	//
	// D-186: per-eye render-texture MSAA (1 = off, 2 / 4 / 8 = that many samples). 4 is the value the host
	// hard-coded before this key existed, so an absent key keeps the shipped look. Anything outside 1/2/4/8 is
	// refused by the settings tool and logged + ignored by the host (never applied silently).
	public int EyeMsaa { get; set; } = 4;

	// D-186-FovScale: per-axis render-FOV scales applied to the four asymmetric angles OpenXR reports from
	// xrLocateViews. 1.0 = pass-through (the shipped value, no over-render reduction). Lower values narrow the
	// rendered FOV so the GPU draws fewer pixels (the OpenXR runtime usually reports a wider FOV than the
	// lens displays; the extra is the lens-distortion / barrel margin and is normally invisible).
	//
	// FovScaleX is applied to the horizontal angles (angleLeft, angleRight). FovScaleY is applied to the
	// vertical angles (angleUp, angleDown). Either axis can be narrowed independently: X<1 + Y=1 is a pure
	// horizontal over-render cut (the common performance case); X=Y<1 is the symmetric cut; X≠Y with both
	// below 1 is the rare aspect-change case (it stretches the picture).
	//
	// The native host multiplies the four angles before both the managed projection and the compositor
	// submit, so the picture stays aligned at the edges (one-sided narrowing shows black bars). The valid
	// range for each axis is 0.50-1.00 with 0.05 steps; anything outside is refused by the settings tool
	// and logged + ignored by the host.
	//
	// D-186-FovScale legacy: a pre-Stage-3.5 file with the single FovScale key (no X/Y suffix) is read as
	// FovScaleX=that value, FovScaleY=1.0 (horizontal-only cut). The legacy key is removed on the next save.
	public float FovScaleX { get; set; } = 1.0f;
	public float FovScaleY { get; set; } = 1.0f;
}
