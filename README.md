# Vampire Hunters VR

An unofficial mod that lets you play **Vampire Hunters** (Steam, build 22267731) in a VR headset with 6DoF head tracking and two-handed motion controls. It is a BepInEx 6 (Unity IL2CPP) plugin plus a native OpenXR host that renders the game's own cameras into the headset.

This repository is the **source release**. The first-party code is licensed under **GNU GPL version 3 or (at your option) any later version** — see `LICENSE`. That license covers only code written for this project. It does not grant any rights to Vampire Hunters game material or to third-party components; see `THIRD-PARTY-NOTICES.txt`.

Not affiliated with or endorsed by the game's developer or publisher. Use it at your own risk, and prefer single-player/offline play: modding a running game can in principle affect online scores or accounts.

## Status

Be aware of what is and is not verified before relying on this:

- Offline test fixtures pass (9 projects, see below). They check math, policy and source-level invariants only; they do **not** prove behaviour inside the headset.
- Confirmed once by the author, in a single full headset session (main menu → stage → back to main menu): hand pose frame (`HandPoseFrame=Head`), the turret-build cycling input, and the right-hand menu ray/click all worked, with no crash (D-202 / D-206 / D-207). Multiple machines, other headsets, and long sessions are still unconfirmed.
- Snap turn is not available while `HandPoseFrame=Head` (BUG-203).
- An earlier `coreclr.dll` access violation (`0xc0000005`) has a plausible cause that was fixed (BUG-205, native/managed hand struct size mismatch), but the cause was never proven and long-session stability is unconfirmed.
- Only the Virtual Desktop OpenXR runtime has been tested on real hardware. If the headset is not connected when the game starts, the host fails once with `xrGetSystem XrResult=-35` and that session stays flat (RUN-001).
- In menus, `LogOutput.log` repeats `EnableHands returned 6/2 - retrying every frame` (BUG-206). It is harmless — controllers and rays work — it only inflates the log file.
- The release package's installer manifest still records `version = 0.9.5-rc1` and its console banner says "preview installer"; these are cosmetic, the shipped binaries are the tested 0.9.5 set.

Details: `docs/PHASE_PLAN.md`, `docs/BUGS.md`, `docs/QUESTIONS.md`, `docs/TARGET_PROFILE.md`, `docs/DECISIONS.md`.

## Contents

- `src/core` — BepInEx IL2CPP plugin and OpenXR host (`VampireHuntersVR.Stage0`), `Install-Stage0.ps1`
- `src/interaction`, `src/render` — hand aim/fire/UI/markers, stereo math, resolution policy
- `src/render/NativeHost` — native C++ OpenXR host (`VhvrNativeHost.dll`) and `Build.ps1`; Khronos headers in `include/`
- `src/settings` — `VhvrSettings` Windows settings tool
- `extras/VhvrExtras` — menu panel, pointer rays, arm haptics, UI scale plugin
- `config` — schema, defaults, bindings, manifest, supported build
- `installer` — install/uninstall scripts and the Korean user READMEs
- `tests` — offline fixtures and the scripts they read
- `docs` — design notes, decisions, bug log, open questions and playtest scripts
- `licenses` — license texts for the third-party components used

Not included: the game, BepInEx and .NET runtime binaries, interop assemblies generated from the game, compiled outputs, logs and backups.

## Building

You must supply two folders yourself (they are not redistributed here):

- `tools/bepinex-be788` — BepInEx 6.0.0-be.788+5b766a3 (Unity.IL2CPP win-x64), from the official BepInEx builds.
- `tools/interop` — the Il2CppInterop assemblies BepInEx generates from **your own** game install on first run.

Exact versions and hashes: `docs/DEPENDENCIES.json`. The native host needs the MSVC x64 toolchain and the OpenXR SDK loader library (`src/render/NativeHost/Build.ps1`).

## Offline tests

```powershell
dotnet run --project tests/HandAim/HandAim.Tests.csproj -c Release
```

Projects: `HandAim`, `GameplayCamera`, `WeaponRuntime`, `VhvrExtras`, `FlameEndpoint`, `FlameEmitter`, `ShotScopes`, `StereoMath`, `RenderTiming`.

`FlameEndpoint` and some scripts under `tests/` read the installed game. They default to `C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters`; set the environment variable `VHVR_GAME_ROOT` to the folder containing `Vampire Hunters.exe` if yours is elsewhere. `VHVR_XR_SIM_JSON` optionally points to a Meta XR Simulator runtime JSON.

## Download (release package)

The playable package is published under **Releases**, not committed to this repository (`dist/` is git-ignored; the binaries live as a release asset).

- Release page: <https://github.com/alsdud95920-sys/VHVR-vampire-hunter-VR-mod/releases/tag/v0.9.5>
- Asset: `VampireHuntersVR-0.9.5.zip` — BepInEx 6 plugin, native OpenXR host, `VhvrSettings.exe`, install/uninstall scripts and the third-party licenses.
- Checksums: `VampireHuntersVR-0.9.5.sha256.txt` next to the asset. Verify before installing:

```powershell
Get-FileHash .\VampireHuntersVR-0.9.5.zip -Algorithm SHA256
```

Release binaries in that package are byte-identical to the set tested in the headset session described above; the source tree in this repository is what produced them (no source edits were made for packaging — only user-facing documents differ).

## Installing and playing

Korean user guides are in `installer/README-KO.md` and `installer/SETTINGS-README-KO.md`. In short: copy the package folder into the game folder with the game closed, run `install.bat`, connect the headset (Virtual Desktop), start the game from Steam. `uninstall.bat` restores the stock game folder.
