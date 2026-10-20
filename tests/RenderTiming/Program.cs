using System.Text.Json;
using VampireHuntersVR.Interaction;

int passed = 0;
void Check(string name, bool ok) { if (!ok) throw new InvalidOperationException(name); Console.WriteLine("PASS " + name); passed++; }
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string dir = Path.Combine(root, "artifacts", "render-timing-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
string path = Path.Combine(dir, "normal.jsonl");
var normal = new BoundedDiagnosticWriter(path, 32);
for (int i = 0; i < 20; i++) Check("queued-" + i, normal.TryRecord(JsonSerializer.Serialize(new { frame = i, read = i % 2 == 0 ? 6 : 0 })));
normal.Complete();
Check("drains", normal.WaitForDrain(5000));
Check("normal-count-and-no-error", normal.Written == 20 && normal.Error == null && normal.Dropped == 0);
Check("completed-rejects", !normal.TryRecord("{}"));
string[] lines = File.ReadAllLines(path);
Check("utf8-no-bom", !File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
Check("row-order", lines.Select((s, i) => JsonDocument.Parse(s).RootElement.GetProperty("frame").GetInt32() == i).All(x => x));
Check("busy-preserved", lines.Count(s => JsonDocument.Parse(s).RootElement.GetProperty("read").GetInt32() == 6) == 10);
var rowLimit = new BoundedDiagnosticWriter(Path.Combine(dir, "rows.jsonl"), 4, 2);
Check("row-limit-first", rowLimit.TryRecord("{}")); Check("row-limit-second", rowLimit.TryRecord("{}"));
Check("row-limit-rejects", !rowLimit.TryRecord("{}") && rowLimit.Dropped == 1);
rowLimit.Complete(); Check("row-limit-drain", rowLimit.WaitForDrain(5000) && rowLimit.Written == 2);
var byteLimit = new BoundedDiagnosticWriter(Path.Combine(dir, "bytes.jsonl"), 4, 10, 5);
byteLimit.TryRecord("{}"); byteLimit.TryRecord("{}"); byteLimit.Complete();
Check("byte-limit-drain", byteLimit.WaitForDrain(5000));
Check("byte-limit-bounded", byteLimit.Bytes <= 5 && byteLimit.Dropped == 1 && byteLimit.Written == 1);
var overflow = new BoundedDiagnosticWriter(Path.Combine(dir, "queue.jsonl"), 1);
int rejected = 0; for (int i = 0; i < 20000; i++) if (!overflow.TryRecord(new string('x', 2048))) rejected++;
overflow.Complete(); Check("queue-overflow-drains", overflow.WaitForDrain(5000));
Check("queue-overflow-explicit", rejected > 0 && overflow.Dropped == rejected);
Check("queue-accounting", overflow.Accepted == overflow.Written);
var giant = new BoundedDiagnosticWriter(Path.Combine(dir, "large.jsonl"));
Check("oversize-row-rejected", !giant.TryRecord(new string('x', 65537)) && giant.Dropped == 1);
giant.Complete(); Check("oversize-drains", giant.WaitForDrain(5000));
var fail = new BoundedDiagnosticWriter(Path.Combine(dir, "missing", "data.jsonl"));
Check("io-failure-drain", fail.WaitForDrain(5000));
Check("io-failure-explicit", fail.Error != null && !fail.TryRecord("{}")); fail.Complete();
var exists = new BoundedDiagnosticWriter(path); Check("never-overwrites-old-evidence", exists.WaitForDrain(5000) && exists.Error != null && File.ReadAllLines(path).Length == 20); exists.Complete();
try { _ = new BoundedDiagnosticWriter("relative.jsonl"); Check("absolute-required", false); }
catch (ArgumentException) { Check("absolute-required", true); }
string Read(string p) => File.ReadAllText(Path.Combine(root, p));
string driver = Read("src/interaction/HandAimDriver.cs"), timing = Read("src/interaction/HandRenderTiming.cs"), host = Read("src/core/OpenXrHost.cs"), hostConfig = Read("src/core/OpenXrHostConfig.cs"), bridge = Read("src/interaction/WeaponRuntimeBridge.cs");
Check("observes-before-busy-return", driver.IndexOf("ObserveHandRead(frame, read, status);", StringComparison.Ordinal) < driver.IndexOf("if (read == 6) { ReapplyLastHandPose();", StringComparison.Ordinal) && driver.IndexOf("if (read == 6) { ReapplyLastHandPose();", StringComparison.Ordinal) > 0); // D-141: busy path now re-applies the last hand pose, but still observes first
Check("applied-only-after-fire-tick", driver.IndexOf("ObserveHandApplied();", StringComparison.Ordinal) > driver.IndexOf("FireTick(player, source)", StringComparison.Ordinal));
Check("release-closes-diagnostics", driver.Contains("ReleaseRenderTiming();") && timing.Contains("renderTiming?.Dispose(); renderTiming = null;"));
Check("per-camera-pre-cull-and-pre-render", timing.Contains("\"FireOnPreCull\"") && timing.Contains("\"FireOnPreRender\"") && timing.Contains("camera != source && camera != eyeLeft && camera != eyeRight"));
Check("native-phase-samples", bridge.Contains("player-before-native-after-gaze") && bridge.Contains("player-after-native-before-gaze") && bridge.Contains("player-after-native-after-gaze"));
Check("no-value-type-hook-arguments", timing.Contains("BeforeCull(Camera __0)") && timing.Contains("BeforeRender(Camera __0)") && !timing.Contains("Vector3 __0") && !timing.Contains("Quaternion __0"));
Check("no-pose-animation-particle-mutation", !timing.Contains("SetPosition") && !timing.Contains("SetLocal") && !timing.Contains(".enabled =") && !timing.Contains(".rotation =") && !timing.Contains(".simulationSpace =") && !timing.Contains("GetParticles("));
Check("bounded-duration-and-inventory", timing.Contains("now - started >= 180") && timing.Contains("nodes.Count >= 24") && timing.Contains("renderers.Count >= 16") && timing.Contains("particles.Count >= 8"));
Check("drops-in-every-sample", timing.Contains("dropped = writer.Dropped") && timing.Contains("missing end/drops/errors invalidate complete coverage"));
// D-206 verification repair (BUG-205): the host and its settings type are two files since the D-178 recovery split
// OpenXrHostConfig.cs out of OpenXrHost.cs, so the flag's own default is asserted in the file that declares it and the
// guard is still asserted in the host that must read it. Both literals are unchanged.
Check("flag-default-and-guard", hostConfig.Contains("public bool RenderTimingDiagnostics { get; set; } = true;") && host.Contains("if(Config.RenderTimingDiagnostics)"));
Check("disable-option-and-installer-validation", Read("tests/headset-handaim.ps1").Contains("NoRenderTimingDiagnostics") && Read("src/core/Install-Stage0.ps1").Contains("'RenderTimingDiagnostics'"));
Check("schema-switch", Read("config/schema.json").Contains("\"RenderTimingDiagnostics\""));
Check("writer-no-unbounded-queue-or-mainthread-wait", Read("src/interaction/BoundedDiagnosticWriter.cs").Contains("queue.TryAdd(line)") && Read("src/interaction/BoundedDiagnosticWriter.cs").Contains("public void Dispose() => Complete();"));
Check("explicit-velocity-evidence-gap", timing.Contains("particleVelocityMeasured = false"));
Check("incompatible-particle-space-getter-never-called", !timing.Contains("p.simulationSpace") && !timing.Contains(".main.simulationSpace") && !timing.Contains("#pragma warning disable CS0618"));
Check("particle-space-gap-in-header-and-samples", timing.Split("particleSpaceMeasured = false").Length == 3 && timing.Contains("D138_UNAVAILABLE_D136A_MAINMODULE_GETTER_MISSING"));
Console.WriteLine($"RESULT {passed} render timing fixtures; real bounded writer + source guards, NOT_UNITY_RUNTIME_NOT_HEADSET_ACCEPTANCE");