# Vampire Hunters VR 0.9.6

An unofficial mod that lets you play **Vampire Hunters** (Steam, build 22267731) in a VR headset with 6DoF head tracking and two-handed motion controls. It is a BepInEx 6 (Unity IL2CPP) plugin plus a native OpenXR host that renders the game's own cameras into the headset. Virtual Desktop (VDXR) is the only OpenXR runtime tested on real hardware.

Not affiliated with or endorsed by the game's developer or publisher. Modding a running game can in principle affect online scores or accounts, so prefer single-player/offline play.

Download: <https://github.com/alsdud95920-sys/VHVR-vampire-hunter-VR-mod/releases/tag/v0.9.6>

## Install

1. Download `VampireHuntersVR-0.9.6.zip` from the release page and check its hash against `VampireHuntersVR-0.9.6.sha256.txt`:

   ```powershell
   Get-FileHash .\VampireHuntersVR-0.9.6.zip -Algorithm SHA256
   ```

2. **Close the game.** Extract the zip **into the game folder** — the folder that contains `Vampire Hunters.exe`.
   Upgrading from an older release: run `uninstall.bat` in the old folder **first**. The installer refuses to merge with an existing `BepInEx` folder, and it recreates `vhvr-openxr-host.json` with the defaults.
3. Run `install.bat` from the extracted `VampireHuntersVR-0.9.6` folder. If you get a permission error, run it as administrator. The installer only writes its own files and leaves the retail game files alone.
4. Run `VhvrSettings.exe` (same folder) to pick your values, then press **Apply**:
   - **Screen size** (per-eye render resolution): Low 0.8 / Normal 1.0 (default) / High 1.2 / Custom 0.5–2.0
   - **In-VR UI size**: 0.50–3.00 (default 1.50)
   - **Eye MSAA**: 1 (off) / 2 / 4 (default) / 8
   - **Anisotropic filtering**: off (default) / on (16x)
   - **Render FOV scale X / Y**: 0.50–1.00 (default 0.85 / 1.00). `1.00` passes the runtime FOV through; lower values narrow the rendered field of view, which magnifies everything on screen.

   Bigger values look sharper and cost frame time. Settings are read once at game start, so change them with the game closed.

> **Recommended for readability: set Render FOV scale X to the minimum (0.50).** A narrower FOV enlarges the whole image, so the in-VR menu, HUD and level-up / chest cards become clearly bigger and easier to read — no other setting has to change. It also draws fewer pixels, so it is cheaper for the GPU. If you prefer the widest view, leave it at 0.85.
5. Connect the headset (Virtual Desktop), then start the game from Steam. The first launch takes one to three minutes.
6. VR starts in the main menu. Pick a stage with the in-VR panel (or on the monitor), and the run starts in VR.

`uninstall.bat` in the same folder restores the stock game folder.

> **Auto-fire.** The mod is designed around the game's own auto-fire option being **off**, so that your trigger pulls are what fires. Leaving it on does not break anything — it just means the game fires on its own as well.

## Controls

Right-handed layout only. Your hands are tracked; the triggers fire the weapon in that hand.

**Recenter first.** Click **both thumbsticks at the same time** to re-place the in-VR menu panel in front of the direction you are looking. Use it whenever a panel ends up behind you or at a bad angle — no restart needed. It needs `[MenuScreen] RecenterChord=true` in `BepInEx\config\local.vampirehuntersvr.extras.cfg`, which is the default.

### Button map

| Input | Action |
|---|---|
| Left stick | Move (analog, relative to the direction you face). In the pause menu the same stick moves the game's UI cursor |
| Left stick click | Left robot arm (`Q`) |
| Left trigger | Left-hand weapon: primary fire (press ≥ 0.6) |
| Left X (lower face) | Interact (`Z`) |
| Left Y (upper face) | **Within 30 cm of your head: reload the left hand.** Further away: left-hand secondary fire |
| Left menu (≡) | Pause (`ESC`) |
| Right stick up | Blood ability (`F`) |
| Right stick down | Special ability / scout dash (`SHIFT`) |
| Right stick click | Right robot arm (`E`) |
| Right stick left/right | Snap turn — 30° per push by default; 45° or off in `VhvrSettings.exe` |
| Right trigger | Right-hand weapon: primary fire |
| Right A (lower face) | Jump (`SPACE`); in menus it confirms |
| Right B (upper face) | Same as left Y, for the right hand: reload close to your head, secondary fire further away |
| Grips (squeeze) | Unassigned during normal play. In the Engineer build mode only: left grip = previous turret type, right grip = next turret type, both grips = place the turret |
| Right system button (○) | Unused — reserved by the runtime |

### Menus, HUD and level-up cards

| Input | Action |
|---|---|
| Both thumbsticks clicked | Recenter the menu panel in front of your view |
| Either trigger | Click with that hand's laser: menu items (≥ 0.5), HUD buttons and level-up cards (≥ 0.6) |
| Stick up/down while the laser rests on a list | Scroll that list |

### Two things to know

- **Snap turn.** Right stick left/right turns you in a step — **30°** (default), **45°**, or **off**. Choose it in `VhvrSettings.exe` → *Snap turn (right stick)*, or edit `SnapTurnDegrees` (`0` / `30` / `45`) in `BepInEx\config\vhvr-openxr-host.json` with the game closed. One step per push, and you stay in the same spot: the view turns, your position in the level does not move, and your hands, weapon and reticle turn with the view. Verified in the headset at 30° (D-211, BUG-203 closed); 45° and off are the same code path with a different step value.
- **Engineer turret.** The build-mode buttons (grip to cycle the turret type, both grips to place it) exist in the code, but I have **not been able to confirm in the headset that the Engineer's turret actually builds and places correctly**, so treat that one class as unverified.


## Download (release package)

The playable package is published under **Releases**, not committed to this repository (`dist/` is git-ignored; the binaries live as a release asset).

- Release page: <https://github.com/alsdud95920-sys/VHVR-vampire-hunter-VR-mod/releases/tag/v0.9.6>
- Asset: `VampireHuntersVR-0.9.6.zip` — BepInEx 6 plugin, native OpenXR host, `VhvrSettings.exe`, install/uninstall scripts and the third-party licenses.
- Checksums: `VampireHuntersVR-0.9.6.sha256.txt` next to the asset.
- Korean user guides: `installer/README-KO.md` (usage) and `installer/SETTINGS-README-KO.md` (the settings tool).

Release binaries in that package are byte-identical to the set the author tested in the headset, and were built from the tree at tag `v0.9.6`. 0.9.6 adds the D-211 snap turn (off / 30° / 45°) on top of the 0.9.5 binaries; the plugin was tested in the headset, while the native host and `VhvrExtras.dll` are unchanged from 0.9.5.

## Repository contents

- `src/core` — BepInEx IL2CPP plugin and OpenXR host (`VampireHuntersVR.Stage0`), `Install-Stage0.ps1`
- `src/interaction`, `src/render` — hand aim/fire/UI/markers, stereo math, resolution policy
- `src/render/NativeHost` — native C++ OpenXR host (`VhvrNativeHost.dll`) and `Build.ps1`; Khronos headers in `include/`
- `src/settings` — `VhvrSettings` Windows settings tool
- `extras/VhvrExtras` — menu panel, pointer rays, arm haptics, UI scale plugin
- `config` — schema, defaults, bindings, manifest, supported build
- `installer` — install/uninstall scripts and the Korean user READMEs
- `tests` — offline fixtures and the scripts they read
- `docs` — design notes, decisions, bug log, open questions, the controller map (`docs/CONTROLS.md`) and playtest scripts
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

These fixtures check math, policy and source-level invariants only; they do not prove behaviour inside a headset.

## License and notices

The first-party code is licensed under **GNU GPL version 3 or (at your option) any later version** — see `LICENSE`. That license covers only code written for this project. It does not grant any rights to Vampire Hunters game material or to third-party components; see `THIRD-PARTY-NOTICES.txt` and `licenses/`.
