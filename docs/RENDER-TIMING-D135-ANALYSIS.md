# D-135 — Sequential video mapping and concrete render-state blind spots
Date: 2026-10-11 | Mode: agentic | Stages6B/8 investigation | No production change

## Result, not a fix
- The hand/weapon pose jump is present in the sequentially decoded **raw** recording, not introduced solely by editing or inaccurate seeking. It is not yet evidence of two distinct hand meshes, a particular bone writer, or a normal/matched HMD frame.
- Exact decoded content correspondence is now established for this clip. The edited 60-fps export repeats raw 30-fps image content, with independent recompression; it is not 60 independent game observations per second.
- Concrete static paths to investigate: native `PlayerController.Update` writes `WeaponAnim.weapon_base` local pose; `HandAimDriver.Tick` skips all correction/trace/reticle work when native `ReadHands` returns busy=6. Separate eye-enable windows can make desktop/HMD sample different states. **Their co-occurrence at the recorded incident is unverified.**
- Preserve accepted aim/damage. TOOL-069/070/071/072 remain OPEN; no root-cause declaration, visual acceptance, new readiness or launch.

## Sequential decoding and provenance
Inputs (unchanged):
| Input | SHA256 | Decoded video |
|---|---|---|
| `<Recordings>\편집후\이상한점.mp4` | `0FBB7FD81B2FD0361D19BB1E18D875AB4005EC7D58C60A6AE863C3D63CBD8F0A` | H.264,1920x1080,60fps,2160 frames,36.000s |
| `<Recordings>\2026-10-11_14-59-17.mp4` | `49DE650E72F68722BC4782A7F16AD182A3EFE63217FB25509685BFC49A69C1DF` | H.264,1280x720,30fps,3240 frames; nominal stream108.000s,container109.120s,last framePTS108.066667s |

- FFmpeg9.0.2 Gyan release-essentials portable ZIP downloaded only to `<RepoRoot>\artifacts\d135-analysis`; publisher SHA256 verified `60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA`. Official https://ffmpeg.org/download.html links this Windows provider; binaries are publisher builds, not FFmpeg-project signed binary releases. No PATH/system installation, plugin linkage or redistribution. GPL tool used externally for offline diagnostics, not adopted as a VR framework/component.
- `Decode.ps1` uses separate ffprobe/ffmpeg processes. No `-ss`, no output `-r`, no FPS filter: full sequential decode from the beginning with `-xerror`, `showinfo`, `-fps_mode passthrough`. Original-resolution showinfo is recorded **before**64x36 grayscale downsampling. Both decode exits0; probe/decode byte counts match2160/3240. Receipts `edited-decode.receipt.json` and `raw-decode.receipt.json` statePASS.
- Offline .NET10 helper compares central grayscale image MAE (mean absolute pixel difference; excludes edge HUD). First search all raw frames for36 one-second anchors, then constrain each of2160 edited frames to±0.15s of the discovered mapping. Every mapped frame fits the formula below, mismatches0; maximum central MAE1.161/255. This is decoded **image-content** matching, not an editor timeline file or a Unity frame-ID mapping. Three raw close-ups and small continuous contact sheets visually confirm the same event. Large initial sheets were omitted by image transport and are not claimed inspected.

### Mapping, zero-based decoded indices
For edited frame `e` (0..2159), raw frame `r = 512 + ceil(e / 2)`.
- Even `e`: rawPTS≈editedPTS+17.066667s.
- Odd `e`: rawPTS≈editedPTS+17.083333s.
-1079 raw frames each supply2 edited content frames,2 boundary raw frames supply1 each. Independently compressed pairs are **not byte-identical** edited frames. No motion interpolation detected by this content mapping; exact export settings remain unknown.
-Do not apply one fixed17.066667 offset to odd frames or count the doubled export images as independent flicker events.

| Raw decoded index / PTS | Edited indices / PTS | Visually inspected |
|---|---|---|
|1095 /36.500000s|1165–1166 /19.416667–19.433333s|Large close foreground weapon/hand structure toward right; display reads03 |
|1096 /36.533333s|1167–1168 /19.450000–19.466667s|Weapon much smaller/lower-right; flame/hit effects remain visible |
|1097 /36.566667s|1169–1170 /19.483333–19.500000s|Large weapon/hand structure moves toward center-left; display reads03 |

These successive raw frames have ordinary33.333ms spacing, not a missing-timestamp gap. The scene also changes with player/enemy motion. The number display and appearance are consistent with one weapon changing presentation, **not proof of Unity object identity**. These views do not establish two simultaneously duplicated player hands or the actual particle velocity/damage axis. Event frames are P/B/P and all non-keyframes; no claim that this alone excludes an encoder defect.

### Cadence limits
- Raw corresponding window≈17.067–53.067s:1081 frames,maximum adjacentPTS33.334ms,no identical adjacent64x36 images,no identical adjacent original-resolution showinfo checksums. This excludes exact decoded whole-image repetition/timestamp gaps **in this window**, not all source-texture repetition, CPU overload, judder, local hand-state flicker or smooth HMD delivery.
- Full raw:140 adjacent small-image duplicates total:139 in the decoded black tail and1 earlier atframe3077/102.566667s. First flat black frame3100/103.333333s. Last raw frame has133.334ms gap. Container109.120s is not proof of3276 decoded video frames; ffprobe and sequential decoder both count3240. OBS previously reported3277drawn/3276output: different counters; this discrepancy is recorded, not labeled36 dropped frames without packet/output analysis.
- No quantitative flicker frequency or all-recording generalization. GOP-only fixes remain unjustified; edited export frame doubling may affect perceived cadence but cannot solely create this already-raw pose jump.
- Recording start05:59:17.134Z+36.500s gives **nominal**05:59:53.634Z,not a synchronized render clock. Existing d130a timeline near it has cached handUpdated05:59:53.672Z in row05:59:54.474Z; scopeDepth0/overwriteSamples0 are receipt-time/post-correction data. No PTS→Unity-frame/busy-return/render-event identity. Never claim these rows capture the anomalous raw frame.

## Static writer / timing paths
Read-only stock GameAssembly/metadata analysis reuses existing LibCpp2IL/Iced stack and Unity2022.3.37f1.36 selected methods disassembled,exit0. Artifacts `native-disasm.log`, `offset-api.log`, `method-catalog.log`; no game assets/binaries committed or redistributed. Addresses below are relative to the pinned stock build, not portable signatures.

1. **Concrete native local-pose writer**: `PlayerController.Update` RVA501830 iterates field230h=`weapon_anim` and calls `WeaponAnim.update(float)` at RVA50F650. `WeaponAnim.update` RVA4AB8F0 computes recoil/tilt state; it is **not itself shown calling the local transform setters**. Later `PlayerController.Update` takes each `WeaponAnim.weapon_base` (field60h) and calls `Transform.set_localPosition` at50FC41,`set_localRotation` at50FDF8,and world`set_position` at50FEA9. Inputs include authoredstart_position/start_rotation,recoil_p/angular_recoil_p,tilt_p andweapon_bob. This is actual native code, not interop stub-body interpretation. Whether this instance/base is the pictured hand mesh, and the incident's branch/values, remain unknown.
2. FOV placement functions (`update_weapons_position_primary_based_on_fov`52FD70,etc.) also contain local-position setters. They are candidates,not proven incident triggers. `PlayerController.LateUpdate`4FB470 conditionally tail-jumps to`execute_stop_game`519D00; no routine hand-mesh writer established there.
3. **Busy skip, existing code**: `<RepoRoot>\src\render\NativeHost\Host.cpp`243–247 tries host.mutex; failure zeros output and returns6. `OnRenderEvent`273–280 holds that same mutex acrossPrepare/Submit;Prepare includesxrWaitFrame91 andHandsSync103. `<RepoRoot>\src\interaction\HandAimDriver.cs`94–106 returns on6 **before**gaze,container writes,andPublishShotFrames. TracePose108/reticle109 are also skipped. No busy count/frame ID is currently saved. Do not turn this into a blocking wait; avoiding render-thread stalls remains necessary.
4. `WeaponRuntimeBridge.cs`74–81 applies autonomous gaze before/after nativePlayer.Update, preserving the roots' **current** world poses, not necessarily the last controller-authored pose. MainLateUpdate normally corrects atHandAimDriver203–214 and aligns the first muzzle by rotating the mount. On a busy skip it does neither. Existingpost-drivertrace overwrite0 cannot cover a frame with no trace call.
5. Eye cameras are distinct textures,enabled atPrepareCameras anddisabled afterrenderUntil (`OpenXrHost.cs`206–227,321–329,373–402);originalsourcecamera remains enabled. This supplies a **conditional** desktop/HMD sampling discriminator,not proof that eye-enable phase correlates with busy or native pose state in this run. Pose freshness also differs between hands and eye preparation.
6. Root world-preserving `WorldPoseScope` does not snapshot/undo every descendant'slocal animation. Existingpose trace observescontainer/firstWeapon/firstMuzzle/camera,not allhandRenderer/bone/WeaponBase transforms. CopyFrom copies camera properties,not all script/pipeline/material caches. Metadata scan finds onlyTMP_MaterialManager andPostProcessLayer namedpre-render callbacks; no weapon callback found. This negative scan cannot exclude engine/skinned/shader/native writers or pipeline rendering callbacks.

## Offline counterexamples and validation
- New project-local `timing\timing.csproj`:16/16PASS,exit0,empty stderr. Uses fake scalar nodes and productionWorldPoseScope to show(1)busy frame can retain native pose/oldreticle and evade post-drivertrace,(2)preserved root does not roll back descendantlocal writes. Source guards confirm current busy-return/lock/eye-enable paths. **Not Unity rendering,not headset evidence,not incident reproduction/rootcause.**
- `video\video.csproj` sequential counts,self-distance anddense2160mapping checks runexit0,empty stderr. `Decode.ps1` live runsPASS; finalPowerShellAST validation recorded separately.
- ExistingShotScopes28/28 andWeaponRuntime67/67 rerunexit0,emptystderr. These arepolicy/fake-node/source tests,not a production fix ordamage/headsetacceptance.
- Offline native helper inheritsnet6.0EOL andnullablewarnings from the earlier analyzer;build0/run0. No productionframework upgraded orfullverifier run(the latter rebuildsreleaseNativeHost perTOOL-072).
- D-1320c9eebe,D-1333e572a3,D-13496168bd directly verified;initialworktreeclean. Freshwhole-stock121files/8dirs/7,726,223,158bytes exacthash/timePASS,exit0;stockstderrcontainsPowerShellmodule progressCLIXML,not empty/error-free output asserted. OldPIDs15320/72828absent;managedreleaseD3D6E972DD852FB4A85759F109641E07A383EA786FEBB7667FD7A9A2FAD6C82E/nativeD025204EB0640751383D56F6D7562223CAA222EEDAAD4C69D29E697389464E55 unchanged.
- Initialdirectdecodecommandtimedout30s andleftzero-byteoutput;replacedbyisolatedworkers with explicitexit/countreceipts. Two inspectioncommands had extra-parenthesisparseerrors,corrected andrerun. No inference from failed/blankcommands. All new artifacts remainignored/project-local;trackedreport preserves conclusions.
- Annotatedcontact/close-upimageextractionexits0,butstderrreportsFontconfigdefaultconfigurationmissing. ImagesandPTSlabelsvisuallyverified;do notclaimemptyimage-extractionstderr. No systemfont/configwriteused tosilencethewarning.

## Next discriminator, no new headset request
Before a fix, if runtime diagnostics are separately authorized with fresh build/test/preparation: primitive/reference-onlyper-frameReadHandsresult+seq,handTickapplied/skipped,eye-enable/hostphase,source+eyecameraIDs andrenderframeIDs;samplemount,WeaponBase,handRenderers/rootbones,muzzle,emitter immediatelybeforeeachcamera's render. Record actualmesh identity/ancestry andparticle space/velocity/projectileowner separately. Compare nativePlayer.Update before/after andsuccessful/skipLateUpdate. Distinguish(A)skippedcorrection,(B)descendantbone/modelwriter,(C)camera/rendercache,(D)particlevelocity/simulationframe.

No blockinglock,globalrotation,particle-spaceoverride,animationdisable,OBSwrite,install/launchorautomaticreuse ofconsumedd130a. Currentdeployment **STOCK,notREADY**. Q-D133-RAW answered for decodedclip mapping;Q-D134-MATCH retains matchedHMD/Unity-render-clock gap. Human action:none now.