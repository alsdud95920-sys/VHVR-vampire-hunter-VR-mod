# OpenXR simulator run configurations (SP5 M2, TOOL-011)

These are the Meta XR Simulator configurations the probe runs were measured with. They exist so the runs can be
reproduced after `artifacts/` is cleaned - `artifacts/` is git-ignored, and these three files are the only record of
how the headless/batch variants were driven.

They are selected with an environment variable, so the shipped files under `<MetaXRSimDir>\config` are never edited
(found by extracting strings from `SIMULATOR.dll`; the runtime logs
`Configuration path not set, default value will be used: <MetaXRSimDir>\config\sim_core_configuration.json` when it is
absent):

```powershell
$env:META_XRSIM_CONFIG_JSON = '<RepoRoot>\config\openxr-sim\sim-batch2.json'
```

| file | what it is | measured result |
|---|---|---|
| `sim-batch2.json` | Meta's own CI setting (`use_batch_mode: true`, as in the shipped `sim_core_configuration_ci.json`), no frontend GUI at all, tracking paths absolutised | session reaches READY, `xrWaitFrame` still blocks, runtime prints `RPC failed: Deadline Exceeded` |
| `sim-jpg.json` | the shipped GUI configuration with `ses_texture_format: jpg` (its own comment says gpu_handle is the fast path and jpg "for stability") | READY, blocked |
| `sim-refresh.json` | the shipped GUI configuration plus `default_refresh_rate: 90` (present in the shipped spatial config, absent from the GUI one) | READY, blocked |

Two things learned the hard way and worth keeping:

1. **The runtime resolves relative paths in a configuration against that configuration's own directory.** These files
   live outside `<MetaXRSimDir>\config`, so every `tracking/…` and `anchors/…` path here is absolute; with relative paths
   the runtime logs `Failed to load hand data from tracking/…` and the tracking service comes up empty. Runs R4-R6 were
   invalidated by exactly that.
2. **The paths are machine-specific** (`<MetaXRSimDir>/config/…`, and the probe's own absolute `--loader` path), like the
   rest of this project's configuration.

The full measured matrix (R1-R11) and the conclusion are in `docs/BUGS.md` TOOL-011; the verdict is in
`docs/VERIFICATION.md` (SP5 / M2) and the decision in `docs/DECISIONS.md` D-065.
