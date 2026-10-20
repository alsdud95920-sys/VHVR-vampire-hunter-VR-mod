# Stage 1 spike preparation — static UUVR compatibility matrix (read-only)

Status: **STATIC_RESOLUTION_ONLY** — nothing is loaded, patched, installed or executed; no headset, no game launch.
This is a pre-spike answer to "can UUVR v0.3.1 even bind in this build", not a hook and not a pass.

Producer: `Recon deps` — a new read-only dependency/reflection reader (`src/diagnostics/DependencyEvidence.cs`, D-049)
that reports which external types and members a real (non-dummy) CLI assembly actually references, whether the
supplied loader/interop assemblies satisfy them, and which string literals its IL carries.
Re-derived every verifier run by `tests/uuvr-compat-check.ps1` (`uuvr-compat-check` in `tests/verify.ps1`).

Sources (all git-ignored, none redistributed):
- pinned plugin `artifacts/uuvr/v0.3.1/il2cpp-legacy/plugins/Uuvr.dll` — 33,792 B,
  SHA256 `D188B8D39A0876D7E634316276B83A45902585B4692CE97653E821EFB29326BE`
- pinned loader `tools/bepinex-be788/BepInEx/core` and `tools/bepinex-be788/dotnet` (6.0.0-be.788+5b766a3)
- this build's interop set `artifacts/transactions/20261006T145312Z/quarantine/BepInEx/interop` (107 DLLs,
  game-derived from `global-metadata.dat`, D-030 quarantine)

## 1. Assembly resolution is not the expected failure mode

All **13/13** assembly references of the pinned plugin resolve by name inside the shipped loader + interop sets:

`System.Runtime`, `UnityEngine.CoreModule`, `BepInEx.Core`, `System.ComponentModel.Primitives`, `0Harmony`,
`BepInEx.Unity.IL2CPP`, `UnityEngine.UIModule`, `System.Collections`, `Il2Cppmscorlib`, `UnityEngine.UI`,
`Il2CppSystem.Core`, `Il2CppInterop.Runtime`, `System.Linq`

(No `NOT_FOUND`, no duplicate-name `AMBIGUOUS`.) This confirms D-047's assembly-level reading at member level: a
missing-assembly crash is not what a spike failure would look like.

## 2. Loader line be.692 -> be.788: exactly one unbound member, named

222 member references; **157 resolve**, 65 do not. Of those, exactly **one** is a critical, attributable mismatch:

```
Il2CppSystem.Collections.Generic.Dictionary`2.get_keys  KeyCollection<!0, !1> ()  ->  MISSING_MEMBER
```

Reading this build's own interop confirms the cause: `Il2Cppmscorlib`'s
`Il2CppSystem.Collections.Generic.Dictionary`2` exposes `get_Keys`, `get__keys` and `set__keys` — and no
`get_keys`. UUVR v0.3.1 was compiled against Il2CppInterop **1.4.6-ci.426** (be.692), whose generated property
accessors were lower-case; the pinned **be.788** generator emits the PascalCase name. Consequence: any UUVR code
path that reads `.Keys` off an `Il2CppSystem.Collections.Generic.Dictionary`2` will not bind at runtime
(`MissingMethodException` at that call), while every other member it calls does bind.

Two further references — `KeyCollection.GetEnumerator`, then `Enumerator.get_Current` / `Enumerator.MoveNext` — name
the **nested** collection types of that same dictionary *without their declaring path*. This reader cannot attribute
that name (recorded as `unattributed` in the report), and this build's interop defines neither name, so they belong
to the same code path. They are reported, but not counted as critical, because a bare nested name is a naming limit
of this reader rather than proof of absence.

The remaining 57 unresolved references are `System.Runtime`-scoped framework types (for example
`System.Collections.Generic.List`1`, `System.Func`2`, `System.EventHandler`1) that name-based resolution cannot
follow through type forwarding. They are excluded from the critical count **by rule** (documented in the report's
limitations), not because they were checked and found present.


## 3. The plugin's reflection names are in its own IL (67 literals)

The shipped binary carries, as `ldstr` literals: `GetLocalRotation`, `DisableAutoXRCameraTracking`,
`disablePositionalTracking`, and the assembly-qualified type probes
`UnityEngine.XR.InputTracking, UnityEngine.XRModule` / `... , UnityEngine.VRModule`,
`UnityEngine.VR.InputTracking, UnityEngine` / `... , UnityEngine.VRModule`,
`UnityEngine.XR.XRDevice, UnityEngine.XRModule` / `... , UnityEngine.VRModule`,
`UnityEngine.XR.XRSettings, UnityEngine.XRModule` / `... , UnityEngine.VRModule`,
`UnityEngine.VR.VRSettings, UnityEngine`, plus its own log strings
(`Failed to find InputTracking.GetLocalRotation. Destroying UUVR Pose Driver.`, ...).
So the lookups the spike must satisfy are known from the binary, not only from upstream source.

## 4. XR symbol surface: the pose path is available — and D-040 is corrected here

| Symbol UUVR looks up | In this build's interop | Consequence |
| --- | --- | --- |
| `UnityEngine.XR.InputTracking.GetLocalRotation(XRNode) : Quaternion` | **present** (`UnityEngine.XRModule.dll`, 14 fields / 31 methods; also `GetLocalPosition`, `GetNodeStates`, `_Injected` forms) | `UuvrPoseDriver`'s lookup *can* succeed. **D-040's "Absent: GetLocalRotation" is corrected**: that scan searched identifier strings in `global-metadata.dat`; the interop generated from that same metadata does contain the member. |
| `UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(Camera, bool)` | **present**, but in the assembly named **`UnityEngine.VRModule`**, not `UnityEngine.XRModule` (10 fields / 16 methods, also `GetTrackingSpaceType` / `SetTrackingSpaceType`) | UUVR probes both assembly-qualified names, so it finds it; no fallback log line in the expected path. |
| `UnityEngine.XR.XRSettings` | **present** in `UnityEngine.VRModule` (25 fields / 29 methods: `get_enabled`, `get_stereoRenderingMode`, `LoadDeviceByName`, `get_eyeTextureResolutionScale`, `get_showDeviceView`) | UUVR's stereo settings can be read and set. |
| `UnityEngine.Camera` stereo surface | **present** (`UnityEngine.CoreModule.dll`: `GetStereoViewMatrix`, `SetStereoViewMatrix`, `Get/SetStereoProjectionMatrix`, `ResetStereoViewMatrices`, `get_stereoEnabled`, `get/set_stereoTargetEye`, `stereoSeparation`, `stereoConvergence`) | Both UUVR's primary path and its documented fallback can bind. |
| `UnityEngine.XR.CommonUsages` | **present but empty** (0 fields / 0 methods); `UnityEngine.XR.XRDisplaySubsystem` is likewise an empty stub | The modern `InputDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, ...)` route is **not usable from managed code in this build**. UUVR does not use it (it uses the legacy `InputTracking` route), but our own Stage 1-2 pose work must read `InputTracking.GetLocalRotation(XRNode.CenterEye)` or a Harmony-level read. |

## 5. Pre-registered spike expectations (how a result now reads)

- **Expected on a healthy start:** the UUVR plugin logs its own banner and *no*
  `Failed to find InputTracking.GetLocalRotation. Destroying UUVR Pose Driver.`, and head rotation follows the headset
  (the one question only the headset can answer). `DisableAutoXRCameraTracking` being available means UUVR *will*
  disable Unity's auto camera tracking; the game's own writes (`PlayerController.LateUpdate`, D-046) remain a Stage 1
  problem and are not part of this spike's verdict.
- **Pre-registered failure symptom (not a surprise):** a `MissingMethodException` for
  `Il2CppSystem.Collections.Generic.Dictionary`2.get_keys` (or the matching `KeyCollection`/`Enumerator` members) in
  `Player.log` identifies the be.692-vs-be.788 accessor-renaming risk of section 2. Record which UUVR feature it
  interrupts; it does not by itself answer the pose/stereo question either way.
- Nothing here upgrades the spike's pre-committed SP1-SP9 rules in `docs/playtests/PLAYTEST-1-SPIKE.md`; it removes
  one unknown (symbol presence) and names one hazard (the accessor rename) beforehand.

## Reproduce

```powershell
$cli = 'src\diagnostics\bin\Release\net10.0\Recon.dll'
dotnet $cli deps artifacts\uuvr\v0.3.1\il2cpp-legacy\plugins\Uuvr.dll artifacts\uuvr-compat\deps `
  tools\bepinex-be788\BepInEx\core tools\bepinex-be788\dotnet `
  artifacts\transactions\20261006T145312Z\quarantine\BepInEx\interop
# and, re-derived with pinned expectations:
powershell -NoProfile -ExecutionPolicy Bypass -File tests\uuvr-compat-check.ps1
```

## Honesty / limitations

- Resolution is by **assembly name** against the supplied directories; it is not a runtime binding, and nothing is
  loaded, resolved, patched or executed. A resolvable member is not a working call.
- Member and reference counts are pinned by `tests/uuvr-compat-check.ps1` so that a loader or plugin change fails
  loudly instead of silently passing; a reader improvement also changes counts, which is why the check pins the
  decisive `unresolvedCritical` list and the pinned plugin hash as well.
- `get_keys` is proven absent *by name* on the resolved declaring type; whether UUVR executes that path in-game is a
  runtime question and stays open until the spike.
- Framework names are excluded from the critical count by rule; the interop copy is game-derived, git-ignored and
  never redistributed.
- No game launch, no loader install, no XR init, no game or save writes.