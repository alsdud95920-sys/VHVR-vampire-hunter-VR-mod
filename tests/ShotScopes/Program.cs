using VampireHuntersVR.Interaction;

int passed = 0;
void Check(string name, bool value)
{
    if (!value) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name); passed++;
}
var parent = new Node();
var left = new Node { Parent = parent, Local = new Pose(2, 60) };
var right = new Node { Parent = parent, Local = new Pose(-3, -90) };
var proxy = new Node { Local = new Pose(2, 60) };
var original = new[] { parent.World, left.World, right.World };
var writes = new List<string>();
IEnumerable<(Func<Pose>, Action<Pose>)> Roots() => new[]
{
    ((Func<Pose>)(() => left.World), (Action<Pose>)(p => { writes.Add("left"); left.World = p; })),
    ((Func<Pose>)(() => right.World), (Action<Pose>)(p => { writes.Add("right"); right.World = p; }))
};
WorldPoseScope<Pose> Enter(Pose aim) => WorldPoseScope<Pose>.Enter(() => parent.World,
    p => { writes.Add("parent"); parent.World = p; }, Roots(), aim);
using (var outer = Enter(proxy.World))
{
    Check("parent-uses-independent-aim", parent.World == proxy.World);
    Check("left-world-origin-and-direction-preserved", left.World == original[1]);
    Check("right-world-origin-and-direction-preserved", right.World == original[2]);
    Check("detached-proxy-is-not-feedback-child", proxy.World == new Pose(2, 60));
    var outerParent = parent.World;
    using (var inner = Enter(original[2]))
    {
        Check("nested-other-hand-camera-aim", parent.World == original[2]);
        Check("nested-left-world-stays-fixed", left.World == original[1]);
        Check("nested-right-world-stays-fixed", right.World == original[2]);
    }
    Check("nested-restores-outer-camera-not-head", parent.World == outerParent);
    Check("nested-restores-both-world-roots", left.World == original[1] && right.World == original[2]);
    writes.Clear();
}
Check("outer-restores-all-world-poses", parent.World == original[0] && left.World == original[1] && right.World == original[2]);
Check("restore-parent-then-reverse-roots", writes.SequenceEqual(new[] { "parent", "right", "left" }));
try { using (Enter(new Pose(8, 120))) throw new ApplicationException("game threw"); }
catch (ApplicationException) { }
Check("original-exception-restores-all", parent.World == original[0] && left.World == original[1] && right.World == original[2]);
var twice = Enter(new Pose(7, 35)); twice.Dispose(); int count = writes.Count; twice.Dispose();
Check("dispose-idempotent", writes.Count == count);
int n = 0;
try
{
    WorldPoseScope<Pose>.Enter(() => parent.World, p => parent.World = p,
        new[] { ((Func<Pose>)(() => left.World), (Action<Pose>)(p => { if (++n == 1) throw new ApplicationException(); left.World = p; })),
            ((Func<Pose>)(() => right.World), (Action<Pose>)(p => right.World = p)) }, new Pose(99, 99));
    throw new InvalidOperationException("entry should fail");
}
catch (ApplicationException) { }
Check("failed-child-entry-rolls-back-parent-and-all-roots", parent.World == original[0] && left.World == original[1] && right.World == original[2]);
int parentCalls = 0;
try
{
    WorldPoseScope<Pose>.Enter(() => parent.World, p => { parent.World = p; if (++parentCalls == 1) throw new ApplicationException(); }, Roots(), new Pose(5, 90));
    throw new InvalidOperationException("parent entry should fail");
}
catch (ApplicationException) { }
Check("partial-parent-write-failure-rolls-back", parent.World == original[0] && left.World == original[1] && right.World == original[2]);
int captureWrites = 0;
try
{
    WorldPoseScope<Pose>.Enter(() => parent.World, _ => captureWrites++,
        new[] { ((Func<Pose>)(() => throw new ApplicationException()), (Action<Pose>)(_ => captureWrites++)) }, new Pose(9, 90));
}
catch (ApplicationException) { }
Check("capture-failure-makes-no-writes", captureWrites == 0);
var order = new List<string>();
var broken = WorldPoseScope<Pose>.Enter(() => new Pose(), _ => order.Add("parent"),
    new[] { ((Func<Pose>)(() => new Pose()), (Action<Pose>)(_ => order.Add("left"))),
        ((Func<Pose>)(() => new Pose()), (Action<Pose>)(_ => { order.Add("right"); if (order.Count > 3) throw new ApplicationException(); })) }, new Pose());
try { broken.Dispose(); }
catch (AggregateException e) { Check("restore-failure-reported", e.InnerExceptions.Count == 1); }
Check("restore-failure-does-not-skip-other-root", order.TakeLast(3).SequenceEqual(new[] { "parent", "right", "left" }));

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string frames = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandShotFrames.cs"));
string shot = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandShot.cs")).Replace("\r\n", "\n");
string fire = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandFireTrigger.cs"));
string driver = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandAimDriver.cs"));
Check("source-frames-published-after-both-hands-before-fire", driver.IndexOf("Update(right,") < driver.IndexOf("PublishShotFrames(source)") && driver.IndexOf("PublishShotFrames(source)") < driver.IndexOf("FireTick(player"));
Check("source-controller-rotation-and-weapon-origin-not-live-child-in-prefix", frames.Contains("aim.Rotation = slot.AimRotation") && frames.Contains("aim.Position = weapon.muzzle_flash_spawn.position") && shot.Contains("s.driver?.AimFor(__instance)") && !shot.Contains("LookRotation(muzzle.forward"));
Check("source-frame-ownership-freshness-and-cleanup", frames.Contains("HandAimPolicy.FreshTicks") && frames.Contains("weapon.transform.IsChildOf(slot.Container)") && driver.Contains("ReleaseShotFrames();"));
Check("source-fire-and-shot-share-production-scope", fire.Contains("BeginShotScope(source, aim!)") && shot.Contains("BeginShotScope(s.source!, aim)") && frames.Contains("WorldPoseScope<(Vector3, Quaternion)>.Enter"));
Check("source-finalizer-not-gated-by-active-or-ownership", shot.Contains("var s = __state.Owner") && shot.Contains("finally\n        {\n            try { __state.Scope.Dispose(); }"));
string diagnostics = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandShotDiagnostics.cs")).Replace("\r\n", "\n");
Check("source-flame-fx-reads-vfx-and-pool-state", diagnostics.Contains("[FLAME_FX]") && diagnostics.Contains("system.particleCount") && diagnostics.Contains("flame.is_vfx_playing") && diagnostics.Contains("flame.projectile_t") && diagnostics.Contains("[FLAME_POOL]") && diagnostics.Contains(".FlameVFX"));
Check("source-flame-fx-writes-nothing", !diagnostics.Contains(".Play(") && !diagnostics.Contains("SetPositionAndRotation") && !diagnostics.Contains("is_vfx_playing =") && !diagnostics.Contains("already_firing ="));
string poseTrace = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandPoseTrace.cs")).Replace("\r\n", "\n");
string poseHook = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandAimDriverPoseTrace.cs")).Replace("\r\n", "\n");
Check("source-pose-trace-reads-rotation-and-aim", poseTrace.Contains("[POSE_TRACE]") && poseTrace.Contains("target.up") && poseTrace.Contains("target.forward") && poseTrace.Contains("Vector3.Angle") && poseTrace.Contains("[POSE_TRACE_SUM]"));
Check("source-pose-trace-writes-nothing", !poseTrace.Contains("SetPositionAndRotation") && !poseTrace.Contains(".rotation =") && !poseTrace.Contains(".Play(") &&
    !poseTrace.Contains("SetParent") && !poseTrace.Contains("new GameObject") && !poseTrace.Contains("Instantiate") &&
    !poseHook.Contains("SetPositionAndRotation") && !poseHook.Contains(".rotation =") && !poseHook.Contains("new GameObject"));
Check("source-pose-trace-hooked-after-both-hands", driver.Contains("TracePose(source);") && driver.IndexOf("Update(right,") < driver.IndexOf("TracePose(source);"));
Console.WriteLine($"RESULT {passed} shot scope fixtures; production transaction/fake nodes/source guards, NOT_GAME_OR_HEADSET_ACCEPTANCE");

readonly record struct Pose(float Position, float Yaw);
sealed class Node
{
    public Node? Parent;
    public Pose Local;
    public Pose World
    {
        get => new((Parent?.World.Position ?? 0) + Local.Position, (Parent?.World.Yaw ?? 0) + Local.Yaw);
        set => Local = new(value.Position - (Parent?.World.Position ?? 0), value.Yaw - (Parent?.World.Yaw ?? 0));
    }
}