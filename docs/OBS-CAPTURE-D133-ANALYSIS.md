# D-133 — OBS Game Capture and recording-path evidence
Date: 2026-10-11 | Mode: agentic | Stages 6B/8 triage | No production or OBS setting change

## Scope and privacy
- User identifies OBS Game Capture, custom FFmpeg recording, keyframe interval 250, and authorizes reading OBS settings.
- Read only selected capture/video/recording settings, recording encoder JSON, OBS log excerpts, and the nearby raw recording's metadata/header. Do not read service credentials or copy raw OBS profiles/scene collections into the repository.
- OBS 32.2.2, process 21628, executable `C:\Program Files\obs-studio\bin\64bit\obs64.exe`; normal configuration root `%APPDATA%\obs-studio`.

## Confirmed capture path
- Active profile and scene collection: `제목 없음`. Current/program scene `모니터1` contains the visible source `게임 캡쳐 2`, type `game_capture`, capture_mode `window`, target `Vampire Hunters:UnityWndClass:Vampire Hunters.exe`, priority 1. No source filters or crops were present in the inspected scene. Scene name does NOT imply monitor/display capture.
- Current log `%APPDATA%\obs-studio\logs\2026-10-11 10-20-21.txt`: 14:58:08.848 D3D11 shared texture capture successful; DXGI swap chain 2560x1440, windowed. OBS acquired the game's presented output, not the separately submitted OpenXR eye textures or a headset/compositor mirror.
- The mod requires the original gameplay camera enabled with targetTexture null; runtime eye cameras render into separate textures. Distinct desktop/HMD paths are now grounded in both code and OBS evidence. Their existence does not prove the cause of flicker or identify a camera-specific weapon writer.
- `limit_framerate` is omitted for the Vampire Hunters source; OBS 32.2.2 default is false. This controls capture copying, not game rendering; implementation uses half the OBS frame interval when unchecked. No speculative hook-rate/compatibility/anti-cheat changes.

## Current recording settings versus historical recordings
| Setting | Confirmed value |
|---|---|
| Mode / recording type | Advanced / `RecType=FFmpeg`, `FFOutputToFile=true` |
| Canvas / output / rate | 2560x1440 / 1280x720 / 30 fps |
| Custom FFmpeg target bitrate / GOP | 6000 kbps / `FFVGOPSize=250` |
| Custom format / video encoder | blank defaults; not an explicit NVENC selection |
| Color | NV12, Rec.709, limited range |
| Saved standard recording encoder | NVENC AV1, CQP 24, keyint_sec 2 — inactive in custom FFmpeg mode |

- 10:20, 10:21, and 12:26 recordings used NVENC AV1/CQP24/keyint60 with `adv_file_output`. 13:58 and 14:59 recordings used `adv_ffmpeg_output`. Do not apply current FFmpeg settings to every previous recording.
- Nearby raw recording `<Recordings>\2026-10-11_14-59-17.mp4`: Windows metadata 1280x720, 30 fps, 109.12 s, H.264; x264 encoder header confirms `keyint=250`, `keyint_min=25`, `scenecut=40`, `rc=abr`, `bitrate=6000`, `threads=22`, `bframes=3`. This recording uses CPU x264, NOT the saved standard-mode NVENC AV1 encoder.
- OBS 32.2.2 maps FFVGOPSize directly to AVCodecContext.gop_size. 250 is frames: at 30 fps the maximum configured interval is about 8.33 s; scene-cut keyframes may occur earlier. Not 250 seconds or necessarily a fixed flicker period.
- Long GOP makes seeking/editing and recovery after damaged data less convenient; it does not ordinarily cause repeated missing frames or relocate a correctly decoded hand/flame relative to a stable reticle. CPU x264 adds CPU work and is a possible load contributor, not demonstrated overload. Corrupted decoding remains separate and is not excluded by metadata.

## Recording observations and limits
- Latest recording: 14:59:17.134 to 15:01:06.361 KST (05:59:17 to 06:01:06 UTC); 3277 drawn frames, 3276 output frames. Earlier FFmpeg recording: 6283 drawn / 6281 output.
- No explicit lagged/skipped/dropped-frame line found in inspected current-session log. Near-equal counts do NOT prove zero capture-content duplicates or smooth source cadence: OBS can repeatedly encode the latest game texture with normal output counts. Counts do not identify the cause of the one-frame difference or justify calling it a dropped frame.
- Latest capture connects before recording; no logged reconnect during it. At 15:01:00.466 target window disappears and capture stops; recording continues about 5.9 s. This can explain only unavailable/stale/blank source at the tail, not continual flicker earlier. Disappearance cause unknown.
- Recording overlaps d130a's wall-clock interval, but d130a cleanup finishes later, at 15:03:44 KST. Do not equate OBS window disappearance with the mod's scene ownership failure without further evidence.
- Edited incident clip `<Recordings>\편집후\이상한점.mp4` remains 1920x1080/36 s with different Lavc/Lavf header. Exact derivation/time mapping to raw recording unverified; metadata/header inspection is not full decode or frame-accurate comparison.

## Updated hypotheses and recommendation
1. Highest priority: desktop presentation/camera/model timing versus separately rendered HMD output. OBS's desktop-game capture path is confirmed; render-time transform mismatches remain hypothetical.
2. Possible extra judder: 30-fps recording versus 90-Hz HMD and d130a's approximately 39.7 application submissions/s. Different clocks; smooth perceived HMD output does not establish desktop-camera state or active runtime reprojection.
3. Possible CPU encoder/capture load: x264 confirmed, severe overload unsupported by log. Do not claim changing GOP will fix the visual bug.
4. Hand/model–flame mismatch is distinct from encoding cadence. If also visible in HMD, recording-only explanation is insufficient; investigate native/late/per-camera model writes and actual particle velocity/simulation frame while preserving accepted firing/damage direction. Projectile ownership separately OPEN.
- Recommended future baseline, NOT applied: standard recording, NVENC H.264, CQP approximately 20–23, 2-second keyframes, MKV, initially retain 30 fps to avoid adding load. At 30 fps, custom FFmpeg GOP 60 is the comparable 2-second setting, but shortening GOP is not a diagnosed fix.
- Future genuine HMD-mirror recording is the path discriminator; monitor capture of the same game window would still show desktop output. No new sitting, launch, setting write, hook change, or deployment.

## Validation and references
- Fresh independent `<RepoRoot>\artifacts\devtools\Check-Stock.ps1` PASS: 121 files / 8 directories / 7,726,223,158 bytes, exact hashes/write times. Old d130a PIDs 15320/72828 absent; saved uninstall0/cleanupError null/stock true. Current game STOCK, not preinstalled READY.
- Release plugin/native unchanged: D3D6E972DD852FB4A85759F109641E07A383EA786FEBB7667FD7A9A2FAD6C82E / D025204EB0640751383D56F6D7562223CAA222EEDAAD4C69D29E697389464E55. No production build/test or install required for documentation-only diagnosis.
- Exact installed OBS tag 32.2.2 source checked: https://raw.githubusercontent.com/obsproject/obs-studio/32.2.2/frontend/utility/AdvancedOutput.cpp (mode/GOP mapping); https://raw.githubusercontent.com/obsproject/obs-studio/32.2.2/plugins/obs-ffmpeg/obs-ffmpeg-output.c (gop_size/default encoder); https://raw.githubusercontent.com/obsproject/obs-studio/32.2.2/plugins/win-capture/game-capture.c (capture defaults). Research only; no GPL OBS code vendored.
- Baseline guide: https://obsproject.com/kb/advanced-recording-settings-guide . Initial Game Capture/performance KB fetches failed; no claims from unavailable pages. Live log initially appeared length 0 in listing; shared-read access successfully read contents.