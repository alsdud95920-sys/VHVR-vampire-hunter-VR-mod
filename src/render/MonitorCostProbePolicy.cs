namespace VampireHuntersVR.Render;

// D-148: measurement only, off by default. The native host writes one "timing" line per 120 submitted frames, so this
// probe advances one phase per timing window and lets the two logs be compared window for window:
//   0 = baseline, the game renders what it always renders (monitor view included)
//   1 = the gameplay source camera disabled, so only the two eye cameras render
//   2 = restored
// After phase 2 the probe is done and the source camera stays enabled. Nothing here changes gameplay state, and the
// caller re-enables the source camera on every exit path.
public static class MonitorCostProbePolicy
{
    // Must stay equal to the native timing window (Host.cpp RecordTiming).
    public const int FramesPerPhase = 120;
    public const int Phases = 3;
    public const long TotalFrames = (long)FramesPerPhase * Phases;
    public static bool Active(long frames) => frames >= 0 && frames < TotalFrames;
    public static int Phase(long frames) => !Active(frames) ? Phases : (int)(frames / FramesPerPhase);
    // Only the middle window disables the monitor view, so "source disabled" is a property of the phase, not of a flag.
    public static bool SourceDisabled(long frames) => Phase(frames) == 1;
    public static long FramesIntoPhase(long frames) => Active(frames) ? frames - (long)FramesPerPhase * Phase(frames) : 0;
    public static string Name(int phase) => phase switch
    {
        0 => "baseline-source-on",
        1 => "source-off",
        2 => "restored-source-on",
        _ => "done"
    };
}