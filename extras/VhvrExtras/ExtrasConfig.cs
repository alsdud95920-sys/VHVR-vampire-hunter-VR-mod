using BepInEx.Configuration;

namespace VhvrExtras;

// Every requested behaviour is behind its own config entry so a playtest can turn one thing on at a time,
// and so the shipped defaults reproduce "nothing changed" if a feature misbehaves.
internal sealed class ExtrasConfig
{
    internal ConfigEntry<float> UiScale { get; }

    internal ConfigEntry<bool> CooldownHaptic { get; }
    internal ConfigEntry<float> CooldownHapticAmplitude { get; }
    internal ConfigEntry<int> CooldownHapticMilliseconds { get; }
    // D-186: the old CooldownHapticArmSeconds ("ArmCooldownSeconds" modelled timer) knob is REMOVED. The buzzer
    // is driven by the weapon's own second_shot_cooldown since D-183 (evidence: Weapon.last_shot_t probe run),
    // and every pulse logs that real value. A stale [AbilityHaptics] ArmCooldownSeconds entry is ignored.

    // D-186 (user decision 2026-10-16): AbilityFaceButton / AbilityFacePulseSeconds are REMOVED. Y/B now
    // carries the weapon's secondary/alt fire (core v0.7.0 WeaponRuntimeBridge) plus the head-near reload
    // gesture, so the extras must not consume the press as an ability any more. Stale [Ability] config
    // entries in the file are ignored from here on.
    internal ConfigEntry<bool> SemiAutoRetrigger { get; }
    internal ConfigEntry<float> SemiAutoRetriggerSeconds { get; }

    internal ConfigEntry<bool> MenuScreen { get; }
    internal ConfigEntry<bool> MenuScreenFirst { get; }
    internal ConfigEntry<float> MenuScreenDistance { get; }
    internal ConfigEntry<float> MenuScreenFov { get; }
    internal ConfigEntry<bool> MenuScreenPointer { get; }
    internal ConfigEntry<bool> MenuPointerHeadFrame { get; }
    internal ConfigEntry<bool> MenuRecenterChord { get; }
    internal ConfigEntry<bool> MenuBlackout { get; }
    internal ConfigEntry<float> MenuScreenScrollSpeed { get; }
    internal ConfigEntry<float> UiScalePaused { get; }

    internal ExtrasConfig(ConfigFile config)
    {
        UiScale = config.Bind("Ui", "Scale", 1.5f,
            new ConfigDescription("UI/HUD scale multiplier applied to the in-VR UI. 1.0 = shipped behaviour.",
                new AcceptableValueRange<float>(0.5f, 3f)));

        CooldownHaptic = config.Bind("AbilityHaptics", "CooldownHaptic", true,
            "Pulse the controller when the ROBOT ARM on that side is ready to fire again (left arm = left hand, right arm = right hand).");
        CooldownHapticAmplitude = config.Bind("AbilityHaptics", "Amplitude", 0.8f,
            new ConfigDescription("Haptic amplitude 0..1.", new AcceptableValueRange<float>(0f, 1f)));
        CooldownHapticMilliseconds = config.Bind("AbilityHaptics", "Milliseconds", 180,
            new ConfigDescription("Haptic pulse length in milliseconds (native clamp 0..500).",
                new AcceptableValueRange<int>(0, 500)));
        // D-186: the modelled ArmCooldownSeconds binding is gone - see the field comments above.

        // D-186: the [Ability] bindings (FaceButtonTriggersAbility / FaceButtonPulseSeconds) are gone -
        // see the field comments above.

        SemiAutoRetrigger = config.Bind("Fire", "SemiAutoRetrigger", true,
            "While the trigger is held, re-send a fire signal every RetriggerSeconds to EVERY weapon so a one-press-per-shot weapon (crossbow) keeps firing. The game's own cooldown still caps the real rate.");
        SemiAutoRetriggerSeconds = config.Bind("Fire", "RetriggerSeconds", 0.6f,
            new ConfigDescription("Seconds between re-sent fire signals while the trigger is held.",
                new AcceptableValueRange<float>(0.1f, 2f)));

        MenuScreen = config.Bind("MenuScreen", "MenuScreen", true,
            "Show the game's menu as a black-background panel in front of the player instead of leaving the headset black while the host runs.");
        MenuScreenFirst = config.Bind("MenuScreen", "MenuFirst", true,
            "Start the VR host in the main menu. Stage0 defers gameplay hand/weapon integration until a stage camera is acquired.");
        MenuScreenDistance = config.Bind("MenuScreen", "DistanceMeters", 2.0f,
            new ConfigDescription("Distance from the eye position to the menu panel. D-178: user asked for 0.5 m further back (1.5 -> 2.0).",
                new AcceptableValueRange<float>(0.5f, 4f)));
        MenuScreenFov = config.Bind("MenuScreen", "HorizontalFovDeg", 85f,
            new ConfigDescription("Horizontal field of view the panel fills at that distance. D-178: 40 -> 75 -> 85 (the user asked for larger again).",
                new AcceptableValueRange<float>(10f, 120f)));
        MenuScreenPointer = config.Bind("MenuScreen", "ControllerPointer", true,
            "Draw a laser from each controller and select menu items with the trigger (needs the game's EventSystem).");
        // D-201: the panel and the eye cameras are placed from the LIVE head pose, so the laser must be composed in
        // that same frame. The old code multiplied the game camera's rotation (ArenaCamera in the menu; measured at
        // Unity yaw -90 on 2026-10-18) into the ray, which is the 90 degree menu laser the user reported.
        MenuPointerHeadFrame = config.Bind("MenuScreen", "PointerHeadFrame", true,
            "true = draw the menu laser in the head/tracking frame, the same frame the menu panel and the eye cameras use (D-201). false = legacy game-camera frame (the ray skews ~90 degrees in the menu).");
        // D-201: manual recenter, so a panel that was placed from a bad head pose never needs a game restart.
        MenuRecenterChord = config.Bind("MenuScreen", "RecenterChord", true,
            "Click BOTH thumbsticks at once to re-place the menu panel in front of where you are looking (recenter).");
        // D-192: EDF6-style blackout. A big black backdrop sits behind the menu panel so the game world
        // disappears behind the menu ("검은 배경 + 평면 스크린"); the panel stays the interactive screen.
        // Retained only for config-file compatibility. The menu never creates a backdrop.
        MenuBlackout = config.Bind("MenuScreen", "MenuBlackout", false,
            "Deprecated: ignored. The menu does not create a black backdrop; the game world remains visible.");
        // D-191/D-192: thumbstick menu scrolling speed (scroll events per frame at full stick deflection).
        MenuScreenScrollSpeed = config.Bind("MenuScreen", "ScrollSpeed", 3.0f,
            new ConfigDescription("How fast the thumbstick scrolls menu lists while the laser is on them (3 = three scroll notches per frame at full deflection).",
                new AcceptableValueRange<float>(0.5f, 10f)));
        // D-192: category UI scale - the paused ESC/pause menu gets its own multiplier so just the menu can be
        // made bigger (user recommendation: 2.0) without inflating the HUD. 1.0 = the shipped 3.0 m width (D-181).
        UiScalePaused = config.Bind("Ui", "ScalePaused", 1.0f,
            new ConfigDescription("Multiplier for the paused ESC/pause menu only (1.0 = its shipped 3.0 m width; 2.0 is recommended when the menu feels small).",
                new AcceptableValueRange<float>(0.5f, 3f)));
    }
}

