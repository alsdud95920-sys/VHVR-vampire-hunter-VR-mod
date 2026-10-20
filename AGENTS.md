# Vampire Hunters VR — deployment and launch contract

Project root: `<RepoRoot>`. Read the VR wizard skill and the five living documents before resuming.

## User requirement (reaffirmed 2026-10-11, D-130)

- The user already wears the headset when sending an execution command. Never defer slow installation or full-tree hashing until that command.
- After a monitored game session ends, uninstall transactionally and verify exact stock restoration. Do not immediately reinstall merely because the game ended.
- After production code changes, finish builds/tests first, then install the validated payload reversibly before reporting ready. Installation is already authorized; do not ask again. Verify installed files/config and record the fresh tag, transaction, hashes, and exact launch arguments.
- Do not launch automatically after coding or installation. A user execution command authorizes one launch using the recorded fresh tag and `-UsePreinstalled`; keep headset connectivity and payload/config guards, monitoring, and automatic stock restoration.
- Check readiness when resuming. If stock or a stale payload is found, prepare it BEFORE waiting for the execution command. If preparation fails, report not ready; never pretend installation is complete.
- Build/test BEFORE installation. The full verifier rebuilds the native DLL (TOOL-072); never run it after ready installation unless reinstalling the final payload afterward.
- Never rerun completed tag `d127a`. Read the current status board for the next prepared tag. Do not change runtime, scene policy, accepted firing direction, or enable the crashing Vector3 VFX detour to accelerate readiness.

## Current pointer

`<RepoRoot>\docs\PHASE_PLAN.md` is the authoritative human-readable readiness board. `<RepoRoot>\artifacts\transactions\current.txt` points to the transaction, but a pointer alone does not prove it is installed: inspect the journal and verify live payload/config. A prepared tag is consumed after its monitored run and cleanup.