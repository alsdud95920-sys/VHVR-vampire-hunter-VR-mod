using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-127 (TOOL-071): read-only rotation trace for the user-reported left-weapon spin (a regression: it was not present in
// the d114a sitting). It ONLY reads a transform's forward/up/position/name, the aim proxy and the driver's own last written
// rotation, so it can never rotate or move anything. It creates no object, calls no weapon/fire/effect entry point, and
// touches no firing, aim, range or damage state. A throw is contained so one odd frame cannot abort the other samples.
// tests/ShotScopes asserts this file contains no write pattern.
internal sealed class PoseTrace
{
    internal sealed class Node
    {
        public string Name = "";
        public Transform? Target;
        public Vector3 Forward, Up;
        public bool Seeded;
        public float MaxStepFwdDeg = float.NaN, MaxStepUpDeg = float.NaN;
        public int StepsOver5Deg, StepsOver15Deg, StepsOver30Deg, RollSteps;
    }

    private const float FwdAnomalyDeg = 30f;   // one-frame swing of the axis
    private const float RollAnomalyDeg = 15f;  // one-frame swing of the up axis = a roll about the barrel
    private const float RollOnlyFwdDeg = 5f;   // ...while the axis itself barely moved
    private const int MaxAnomalyLines = 400;

    public readonly Node Container = new() { Name = "container" };
    public readonly Node Weapon = new() { Name = "weapon" };
    public readonly Node Muzzle = new() { Name = "muzzle" };
    public readonly Node Camera = new() { Name = "camera" };
    public Transform? WeaponTransform, MuzzleTransform, AimTransform;
    public long Samples;
    public float MaxWeaponVsContainerDeg = float.NaN, MaxMuzzleVsAimDeg = float.NaN, MaxContainerVsWrittenDeg = float.NaN;
    public int OverwriteSamples, Anomalies, Errors;
    public string? Error, LastAnomaly;
    private DateTime nextSummary = DateTime.UtcNow.AddSeconds(5);

    public void Observe(string hand, Transform? container, Transform? camera, Quaternion lastWritten, bool hasWritten,
        int scopeDepth, bool driving)
    {
        if (!driving || container == null)
        {
            // Re-seed on the next real sample so a pause in driving cannot be read as one huge step.
            Container.Seeded = false; Weapon.Seeded = false; Muzzle.Seeded = false; Camera.Seeded = false;
            return;
        }
        try
        {
            Samples++;
            float dFwd = Step(Container, container, false);
            float dUp = Step(Container, container, true);
            float wFwd = Step(Weapon, WeaponTransform, false);
            float wUp = Step(Weapon, WeaponTransform, true);
            float mFwd = Step(Muzzle, MuzzleTransform, false);
            float mUp = Step(Muzzle, MuzzleTransform, true);
            Step(Camera, camera, false);
            Step(Camera, camera, true);

            float weaponVsContainer = WeaponTransform == null ? float.NaN : Vector3.Angle(WeaponTransform.forward, container.forward);
            Keep(weaponVsContainer, ref MaxWeaponVsContainerDeg);
            float muzzleVsAim = MuzzleTransform == null || AimTransform == null ? float.NaN : Vector3.Angle(MuzzleTransform.forward, AimTransform.forward);
            Keep(muzzleVsAim, ref MaxMuzzleVsAimDeg);
            float vsWritten = hasWritten ? Quaternion.Angle(container.rotation, lastWritten) : float.NaN;
            Keep(vsWritten, ref MaxContainerVsWrittenDeg);
            if (float.IsFinite(vsWritten) && vsWritten > 1f) OverwriteSamples++;

            bool roll = dUp > RollAnomalyDeg && dFwd < RollOnlyFwdDeg;
            if (roll) Container.RollSteps++;
            if (dFwd > FwdAnomalyDeg || dUp > RollAnomalyDeg)
            {
                Anomalies++;
                LastAnomaly = $"{hand} frame={Time.frameCount} dFwd={Fmt(dFwd)} dUp={Fmt(dUp)} roll={roll} vsWritten={Fmt(vsWritten)}";
                if (Anomalies <= MaxAnomalyLines)
                    Log($"[POSE_TRACE] hand={hand} frame={Time.frameCount} scopeDepth={scopeDepth} dFwd={Fmt(dFwd)} dUp={Fmt(dUp)} wFwd={Fmt(wFwd)} " +
                        $"wUp={Fmt(wUp)} mFwd={Fmt(mFwd)} mUp={Fmt(mUp)} weaponVsContainer={Fmt(weaponVsContainer)} muzzleVsAim={Fmt(muzzleVsAim)} " +
                        $"containerVsWritten={Fmt(vsWritten)} container=[{Describe(container)}] weapon=[{Describe(WeaponTransform)}] " +
                        $"muzzle=[{Describe(MuzzleTransform)}] aim=[{Describe(AimTransform)}] camera=[{Describe(camera)}]");
            }
            if (DateTime.UtcNow >= nextSummary)
            {
                nextSummary = DateTime.UtcNow.AddSeconds(5);
                Log($"[POSE_TRACE_SUM] hand={hand} samples={Samples} container={Summary(Container)} weapon={Summary(Weapon)} muzzle={Summary(Muzzle)} " +
                    $"camera={Summary(Camera)} maxWeaponVsContainer={Fmt(MaxWeaponVsContainerDeg)} maxMuzzleVsAim={Fmt(MaxMuzzleVsAimDeg)} " +
                    $"maxContainerVsWritten={Fmt(MaxContainerVsWrittenDeg)} overwriteSamples={OverwriteSamples} anomalies={Anomalies}");
            }
        }
        catch (Exception e) { Errors++; Error = e.GetType().Name + ": " + e.Message; }
    }

    // One axis of one node. Returns the angle this axis moved since the previous sample of the SAME transform (NaN when
    // there is nothing to compare), and records the running worst case and the step-rate counters.
    private static float Step(Node node, Transform? target, bool useUp)
    {
        if (target == null) { node.Target = null; node.Seeded = false; return float.NaN; }
        Vector3 axis = useUp ? target.up : target.forward;
        Vector3 previous = useUp ? node.Up : node.Forward;
        float step = node.Seeded && node.Target == target ? Vector3.Angle(axis, previous) : float.NaN;
        if (float.IsFinite(step))
        {
            if (useUp) Keep(step, ref node.MaxStepUpDeg); else Keep(step, ref node.MaxStepFwdDeg);
            if (step > 5f) node.StepsOver5Deg++;
            if (step > 15f) node.StepsOver15Deg++;
            if (step > 30f) node.StepsOver30Deg++;
        }
        if (useUp) node.Up = axis; else node.Forward = axis;
        node.Target = target; node.Seeded = true;
        return step;
    }

    public object Receipt() => new
    {
        samples = Samples, anomalies = Anomalies, overwriteSamples = OverwriteSamples, errors = Errors, error = Error,
        lastAnomaly = LastAnomaly,
        maxWeaponVsContainerDeg = Finite(MaxWeaponVsContainerDeg),
        maxMuzzleVsAimDeg = Finite(MaxMuzzleVsAimDeg),
        maxContainerVsWrittenDeg = Finite(MaxContainerVsWrittenDeg),
        container = NodeReceipt(Container), weapon = NodeReceipt(Weapon), muzzle = NodeReceipt(Muzzle), camera = NodeReceipt(Camera),
        note = "Read-only rotation trace for TOOL-071: transform rotation/position/name only; no transform, object, " +
               "ParticleSystem, field or weapon/effect entry point is written by this sampler."
    };

    private static object NodeReceipt(Node node) => new
    {
        maxStepFwdDeg = Finite(node.MaxStepFwdDeg), maxStepUpDeg = Finite(node.MaxStepUpDeg),
        stepsOver5Deg = node.StepsOver5Deg, stepsOver15Deg = node.StepsOver15Deg, stepsOver30Deg = node.StepsOver30Deg,
        rollSteps = node.RollSteps
    };

    private static string Summary(Node node) =>
        $"(fwd={Fmt(node.MaxStepFwdDeg)} up={Fmt(node.MaxStepUpDeg)} over5={node.StepsOver5Deg} over15={node.StepsOver15Deg} " +
        $"over30={node.StepsOver30Deg} roll={node.RollSteps})";

    private static string Describe(Transform? t) => t == null ? "null" :
        $"id={t.GetInstanceID()} parent={(t.parent == null ? "none" : t.parent.name + ":" + t.parent.GetInstanceID())} " +
        $"fwd={FmtV(t.forward)} up={FmtV(t.up)}";

    private static string FmtV(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
    private static string Fmt(float v) => float.IsFinite(v) ? v.ToString("F1") : "n/a";
    private static float? Finite(float v) => float.IsFinite(v) ? v : (float?)null;
    private static void Keep(float value, ref float max)
    {
        if (float.IsFinite(value) && (!float.IsFinite(max) || value > max)) max = value;
    }
    private static void Log(string line) => VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(line);
}