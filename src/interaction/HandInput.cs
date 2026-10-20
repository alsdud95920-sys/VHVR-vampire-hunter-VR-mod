using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-091/D-093: controller locomotion and menus. A virtual Xbox pad (VirtualPad) feeds the game's own Gamepad scheme:
// left stick = analog move (360 deg, rotated to the HEADSET heading), left menu = Start (pause), right A = South
// (jump; Submit while paused), left X = left-stick press (Interact, D-101) AND SelectButton (D-184 turret placement
// confirm attempt; overlap approved by the user - D-206 proved the Select action is inert for building, so the grip
// chord below carries the real confirm). D-114: right stick up = North (Bloody Rush), down = East (Special Ability),
// left/right stick click = left/right shoulder (ArmL/ArmR). D-101: Y / B (upper face button) with that controller beside the headset = West (Reload). While paused the left stick navigates the game UI.
// D-206 (BUG-204): the engineer's build mode - left grip = previous turret blueprint, right grip = next, both grips
// together = the game's Fire action = build. Live only while the game reports PlayerController.is_turret_mode; the
// grips are otherwise unbound, exactly as D-057 decided.
// If the pad cannot be created it falls back to PlayerInputController.MoveInput/JumpInput.
// No OS keyboard/mouse input is synthesized. D-099: right stick X = snap turn. D-211: the host turns the VIEW by a
// session view-yaw offset about the anchor (HandPoseFrame=Head, the default); the legacy anchor rotation is still used
// when HandPoseFrame=Anchor. SnapTurnDegrees is one of three choices - 0 (off), 30, 45.
// NOT headset tested. The fallback path (no virtual pad) has no interact or reload route.
internal sealed partial class HandAimDriver
{
    private const float SnapPress = SnapTurnPolicy.PressAt, SnapRelease = SnapTurnPolicy.ReleaseAt;
    private bool snapArmed = true;
    private float pendingSnap;
    public float SnapDegrees;
    public int SnapTurns;
    public float SnapTotal;

    // One turn per stick push (SnapTurnPolicy): re-arms only after the stick returns near center.
    private void SnapTick(NativeHandsStatus status, bool available)
    {
        if (!InputEnabled || SnapDegrees <= 0f) return;
        float x = available ? status.Right.StickX : 0f;
        float d = SnapTurnPolicy.Next(snapArmed, x, SnapDegrees, out snapArmed);
        if (d != 0f) { pendingSnap += d; SnapTurns++; SnapTotal += d; }
    }

    // Consumed by the host, which owns the anchor rotation.
    public float TakeSnap() { float d = pendingSnap; pendingSnap = 0f; return d; }

    private const float MoveDeadzone = 0.18f;
    // GamepadButton bit indices. D-100, from the game's own action asset: Special Ability=East, Bloody Rush=North, Interact(+Run)=leftStickPress, Reload=West,
    // ArmL=leftShoulder, ArmR=rightShoulder.
    private const uint BitNorth = 1u << 4, BitEast = 1u << 5, BitSouth = 1u << 6, BitWest = 1u << 7, BitLeftStick = 1u << 8, BitLeftShoulder = 1u << 10, BitRightShoulder = 1u << 11, BitStart = 1u << 12, BitSelect = 1u << 13; // D-184: SelectButton = turret placement confirm
    // D-206 (BUG-204): the game's UI map binds Navigate to <Gamepad>/dpad/left,right as well as to the left stick, so a
    // blueprint step is sent as a pad bit AND as the direct navigationDirection field (see TurretBlock below).
    private const uint BitDpadLeft = 1u << 2, BitDpadRight = 1u << 3;
    private PlayerInputController? inputController;
    private DateTime nextInputLookup;
    private Vector2 lastMove;
    private bool menuHeld, jumpHeld, interactHeld, selectHeld, reloadHeld, reloadLHeld, reloadRHeld, faceLWas, faceRWas, specialHeld, bloodHeld, armLHeld, armRHeld, inputFailed, padTried, schemeSwitched;
    private VirtualPad? pad;
    private float uiSouthUntil, nextLevelUpCheck;
    private bool levelUpOpen;
    public int MenuBlockedByLevelUp;
    public bool InputEnabled;
    public int MoveCalls, MenuPresses, JumpPresses, InteractPresses, SelectPresses, ReloadPresses, FacePresses, SpecialPresses, BloodPresses, ArmLPresses, ArmRPresses;
    public string? InputError, PadNote;
    public int PadSent => pad?.Sent ?? 0;
    public string? PadError => pad?.Error;
    public float LastMoveYawDelta;
    // D-101: head-to-controller distance at each upper-face-button press (tuning data for the reload reach).
    public float LastFacePressHeadDistance = float.NaN, MinFacePressHeadDistance = float.NaN;

    // D-206 (BUG-204): the engineer's turret build mode. The grips are unbound everywhere else (D-057); inside the
    // game's own turret mode (`PlayerController.is_turret_mode`) the left grip steps the blueprint back, the right grip
    // steps it forward, and both grips together hold the game's `Fire` action, which is the confirm the game's own text
    // names (skill_desc_engineer_1). TurretCycle picks the surface one step is sent on; "Off" disables the whole block.
    // All four values are validated by the host at startup (OpenXrHost refuses anything else).
    public string TurretCycle = "Stick";
    private readonly TurretGripPolicy turretGrips = new();
    private bool turretPrevWas, turretNextWas, turretConfirmWas, turretModeFailed, turretFireLatched;
    public int TurretModeFrames, TurretStepsBack, TurretStepsForward, TurretConfirms;
    public string? TurretModeError;
    private const float StepStick = 0.9f; // left-stick X a Stick step holds for TurretGripPolicy.StepSeconds

    // World distance from the headset to this hand's controller; infinity when either is unknown.
    private float HeadDistance(HandSlot slot) => hasHead && slot.HasAimRay ? (slot.AimOrigin - headPos).magnitude : float.PositiveInfinity;

    // D-103: a short pulse on the controller that just entered the 0.30 m reach beside the headset (the reload zone), so the hand
    // can feel the zone without looking. Edge only, per hand, never while paused. A native failure disables haptics only.
    public bool HapticEnabled;
    public float HapticAmplitude = HeadHapticPolicy.DefaultAmplitude;





    // Yaw of a rotation in degrees; falls back to the up vector when the forward axis is near vertical.
    private static float YawOf(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        Vector3 flat = new Vector3(f.x, 0f, f.z);
        if (flat.sqrMagnitude < 0.05f)
        {
            Vector3 up = q * Vector3.up;
            flat = f.y < 0f ? new Vector3(up.x, 0f, up.z) : new Vector3(-up.x, 0f, -up.z);
            if (flat.sqrMagnitude < 0.0001f) return 0f;
        }
        return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
    }

    // The game moves relative to its own (fixed) camera heading; the player looks wherever the headset points.
    private Vector2 HeadRelative(Vector2 stick, Camera source)
    {
        if (!hasHead || stick == Vector2.zero) return stick;
        float delta = YawOf(headRot) - YawOf(source.transform.rotation);
        LastMoveYawDelta = delta;
        Vector3 v = Quaternion.Euler(0f, delta, 0f) * new Vector3(stick.x, 0f, stick.y);
        return new Vector2(v.x, v.z);
    }

    private bool LevelUpOpen()
    {
        if (Time.unscaledTime < nextLevelUpCheck) return levelUpOpen;
        nextLevelUpCheck = Time.unscaledTime + 0.2f;
        bool open = false;
        foreach (var ui in Resources.FindObjectsOfTypeAll<LevelUpUI>()) if (ui != null && ui.gameObject.activeInHierarchy) { open = true; break; }
        foreach (var box in Resources.FindObjectsOfTypeAll<LevelUPBox>()) if (box != null && box.gameObject.activeInHierarchy) { open = true; break; }
        levelUpOpen = open && Time.timeScale <= 0.01f;
        return levelUpOpen;
    }

    private void SwitchToPad()
    {
        schemeSwitched = true;
        try
        {
            var pi = inputController!.playerInput;
            if (pi == null) { PadNote = "no PlayerInput"; return; }
            pi.SwitchCurrentControlScheme("Gamepad", new[] { pad!.Device! });
            pi.neverAutoSwitchControlSchemes = true; // a stray mouse move must not flip the game back to keyboard
            PadNote = "scheme switched";
        }
        catch (System.Exception e) { PadNote = "scheme switch failed: " + e.GetType().Name + ": " + e.Message; }
    }

    public string InputScheme()
    {
        try
        {
            var c = inputController;
            return c == null ? "none" : "gamepad=" + c.UsingGamepad + " kbm=" + c.UsingKeyboardAndMouse + " type=" + c.controllerType;
        }
        catch (System.Exception e) { return "err " + e.GetType().Name; }
    }

    private void InputTick(NativeHandsStatus status, bool available, Camera source, PlayerController? player)
    {
        if (!InputEnabled || inputFailed) return;
        try
        {
            if (inputController == null)
            {
                if (DateTime.UtcNow < nextInputLookup) return;
                nextInputLookup = DateTime.UtcNow.AddSeconds(1);
                var found = Resources.FindObjectsOfTypeAll<PlayerInputController>();
                if (found.Length != 1 || found[0] == null) return; // ambiguous or absent: do nothing
                inputController = found[0];
                schemeSwitched = false; // new stage: re-attach the pad to the new PlayerInput
            }
            if (!padTried)
            {
                padTried = true;
                var candidate = new VirtualPad();
                if (candidate.Create()) pad = candidate;
                else { PadNote = "pad create failed: " + candidate.Error; candidate.Remove(); }
            }
            if (pad != null && !schemeSwitched) SwitchToPad();

            bool paused = Time.timeScale <= 0.01f;
            bool alive = available && !paused;
            var leftHand = available ? status.Left : default;
            var rightHand = available ? status.Right : default;

            Vector2 stick = available ? ApplyDeadzone(new Vector2(leftHand.StickX, leftHand.StickY)) : Vector2.zero;
            Vector2 move = alive ? HeadRelative(stick, source) : Vector2.zero;

            // The menu button must work while paused (that is how a menu is closed), so it ignores timeScale.
            bool menu = available && leftHand.Menu == 1 && !LevelUpOpen(); // D-094: Start during a level-up closed the card screen (ha5)
            if (available && leftHand.Menu == 1 && !menu) MenuBlockedByLevelUp++;
            bool pulse = Time.unscaledTime < uiSouthUntil;
            bool jump = (available && rightHand.Lower == 1 && (alive || pad != null)) || pulse; // while paused A = Submit on the pad
            bool interact = alive && leftHand.Lower == 1; // D-101: left X = the game's leftStickPress action (its Arena action asset binds Interact to it)
            // D-101: Y / B with that controller beside the headset = Reload (pad West). Starts on the press edge only, then holds until release.
            bool faceL = alive && leftHand.Face == 1, faceR = alive && rightHand.Face == 1;
            float headL = HeadDistance(left), headR = HeadDistance(right);
            if (faceL && !faceLWas) NoteFacePress(headL);
            if (faceR && !faceRWas) NoteFacePress(headR);
            reloadLHeld = ReloadLatchPolicy.Next(reloadLHeld, faceLWas, faceL, headL);
            reloadRHeld = ReloadLatchPolicy.Next(reloadRHeld, faceRWas, faceR, headR);
            faceLWas = faceL; faceRWas = faceR;
            bool reload = reloadLHeld || reloadRHeld;
            // D-114: right stick up = Bloody Rush, down = Special Ability (the scout's dash); stick clicks unchanged.
            var abilities = AbilityStickPolicy.Next(specialHeld, bloodHeld, rightHand.StickY, rightHand.StickX, alive);
            bool special = abilities.Special, blood = abilities.Blood;
            bool armL = alive && leftHand.StickClick == 1;
            bool armR = alive && rightHand.StickClick == 1;
            if (menu != menuHeld) { menuHeld = menu; if (menu) MenuPresses++; }
            if (jump != jumpHeld) { jumpHeld = jump; if (jump && alive) JumpPresses++; }
            if (interact != interactHeld) { interactHeld = interact; if (interact) InteractPresses++; }
            // D-184: left X carries BOTH Interact and Select (turret placement confirm; the user approved the overlap).
            // The direct setter runs every tick like SpecialAbilityInput below, so placement works even if the action
            // asset never binds Select to a pad button; the pad bit covers the action-asset path if it does.
            if (interact != selectHeld) { selectHeld = interact; if (interact) SelectPresses++; }
            inputController.SelectInput(interact && alive);
            if (reload != reloadHeld) { reloadHeld = reload; if (reload) ReloadPresses++; }
            if (special != specialHeld) { specialHeld = special; if (special) SpecialPresses++; }
            if (blood != bloodHeld) { bloodHeld = blood; if (blood) BloodPresses++; }
            if (armL != armLHeld) { armLHeld = armL; if (armL) ArmLPresses++; }
            if (armR != armRHeld) { armRHeld = armR; if (armR) ArmRPresses++; }

            // D-206 (BUG-204): turret build mode. The game's own flag is read every tick; a read failure disables only
            // this block and is reported in the receipt, like every other optional layer here.
            bool turret = false;
            if (alive && player != null && !turretModeFailed)
            {
                try { turret = player.is_turret_mode; }
                catch (System.Exception e) { turretModeFailed = true; TurretModeError = e.GetType().Name + ": " + e.Message; }
            }
            bool cycleOn = turret && !TurretCycle.Equals("Off", StringComparison.OrdinalIgnoreCase);
            var grips = turretGrips.Next(cycleOn, available ? leftHand.Squeeze : 0f, available ? rightHand.Squeeze : 0f, Time.unscaledTime);
            if (turret) TurretModeFrames++;
            if (grips.Previous && !turretPrevWas) TurretStepsBack++;
            if (grips.Next && !turretNextWas) TurretStepsForward++;
            if (grips.Confirm && !turretConfirmWas) TurretConfirms++;
            turretPrevWas = grips.Previous; turretNextWas = grips.Next; turretConfirmWas = grips.Confirm;
            bool stepForward = grips.Next, step = grips.Previous || stepForward;
            bool stickStep = step && TurretCycle.Equals("Stick", StringComparison.OrdinalIgnoreCase);
            bool dpadStep = step && TurretCycle.Equals("Dpad", StringComparison.OrdinalIgnoreCase);
            bool specialStep = step && TurretCycle.Equals("Special", StringComparison.OrdinalIgnoreCase);
            bool confirmHeld = grips.Confirm && alive;
            // The confirm is the game's own Fire action: its own callback plus, below, the pad's right trigger - the
            // control the game's own action asset binds Fire to (<Gamepad>/rightTrigger; the flat `X` key is that
            // action's keyboard binding, which is why D-184's key choice was right and its action choice was not).
            // Both surfaces are touched ONLY while the game itself reports build mode, plus one release tick after it
            // leaves, so the Fire action is never written during ordinary play.
            if (turret)
            {
                inputController.FirePressedInput(confirmHeld);
                turretFireLatched = confirmHeld;
                // Direct field write, the belt-and-braces shape D-184 used for Select: only ever while the game itself
                // says it is in turret mode, so the game's own UI navigation is never disturbed.
                inputController.navigationDirection = step ? new Vector2(stepForward ? 1f : -1f, 0f) : Vector2.zero;
            }
            else if (turretFireLatched)
            {
                inputController.FirePressedInput(false); // the chord was let go as the game left build mode: release Fire exactly once
                inputController.navigationDirection = Vector2.zero;
                turretFireLatched = false;
            }

            if (pad != null)
            {
                Vector2 axis = paused ? stick : move; // paused: the raw stick drives the game's UI navigation
                if (stickStep) axis = new Vector2(stepForward ? StepStick : -StepStick, axis.y); // D-206: a step on the Move axis (build mode locks movement)
                uint bits = (menu ? BitStart : 0u) | (jump ? BitSouth : 0u) | (interact ? (BitLeftStick | BitSelect) : 0u) | (reload ? BitWest : 0u) | (special || specialStep ? BitEast : 0u) |
                    (blood ? BitNorth : 0u) | (armL ? BitLeftShoulder : 0u) | (armR ? BitRightShoulder : 0u) | (dpadStep ? (stepForward ? BitDpadRight : BitDpadLeft) : 0u);
                pad.Send(axis.x, axis.y, 0f, 0f, 0f, confirmHeld ? 1f : 0f, bits);
                if (axis != lastMove) { lastMove = axis; MoveCalls++; }
                return;
            }

            // Fallback: the game's own PlayerInputController entry points. Without the pad only the direct surfaces
            // (FirePressedInput, navigationDirection, SpecialAbilityInput) can carry a turret step - see docs\CONTROLS.md.
            if (move != lastMove) { inputController.MoveInput(move); lastMove = move; MoveCalls++; }
            inputController.StartInput(menu);
            inputController.JumpInput(jump && alive);
            inputController.SpecialAbilityInput(special || specialStep);
            inputController.BloodyRushInput(blood); // fallback has no run or robot-arm path (their press/release pairing is unknown)
        }
        catch (System.Exception e) { inputFailed = true; InputError = e.GetType().Name + ": " + e.Message; }
    }

    // Never leave the game believing a button or the stick is still held.
    private void InputRelease()
    {
        var controller = inputController;
        inputController = null;
        try
        {
            if (pad != null)
            {
                try
                {
                    var playerInput = controller?.playerInput;
                    if (playerInput != null) playerInput.neverAutoSwitchControlSchemes = false;
                }
                catch (System.Exception) { }
                pad.Remove();
                pad = null;
            }
            else if (controller != null)
            {
                if (lastMove != Vector2.zero) controller.MoveInput(Vector2.zero);
                controller.StartInput(false);
                controller.JumpInput(false);
                controller.SpecialAbilityInput(false);
                controller.BloodyRushInput(false);
                controller.SelectInput(false);
                // D-206 (BUG-204): never leave the game believing the turret build chord or a blueprint step is held.
                controller.FirePressedInput(false);
                controller.navigationDirection = Vector2.zero;
            }
        }
        catch (System.Exception) { }
        lastMove = Vector2.zero;
        menuHeld = jumpHeld = interactHeld = selectHeld = reloadHeld = reloadLHeld = reloadRHeld = faceLWas = faceRWas = specialHeld = bloodHeld = armLHeld = armRHeld = false;
        turretGrips.Reset();
        turretPrevWas = turretNextWas = turretConfirmWas = turretFireLatched = false;
    }
}
