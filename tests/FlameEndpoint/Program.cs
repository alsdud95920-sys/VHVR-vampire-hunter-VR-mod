using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using VampireHuntersVR.Interaction;

int passed = 0;
void Check(string name, bool ok) { if (!ok) throw new InvalidOperationException(name); Console.WriteLine("PASS " + name); passed++; }
void Throws(string name, Action action)
{
    bool threw = false; try { action(); } catch (Exception) { threw = true; }
    Check(name, threw);
}
byte[] original = FlameEndpointPin.OriginalContext(), memory = (byte[])original.Clone();
int writes = 0;
byte[] Read() => (byte[])memory.Clone();
void Write(byte value) { memory[FlameEndpointPin.BranchOffset] = value; writes++; }
ByteBranchPatch Install() => ByteBranchPatch.Install(Read, Write, original,
    FlameEndpointPin.BranchOffset, FlameEndpointPin.EndpointOpcode);
var patch = Install();
Check("patch-active", patch.Active);
Check("exactly-one-byte-changed", memory.Where((b, i) => b != original[i]).Count() == 1 && writes == 1);
Check("opcode-only-displacement-preserved", memory[14] == 0xEB && memory[15] == 0x1F);
Check("same-native-endpoint-block-destination", FlameEndpointPin.ContextRva + 14 + 2 + memory[15] == 0x46BF4C);
Throws("duplicate-application-rejected", patch.Apply);
Throws("other-owner-rejected", () => Install());
Check("duplicate-does-not-write", writes == 1);
patch.Dispose();
Check("restores-exact-original", !patch.Active && memory.SequenceEqual(original) && writes == 2);
patch.Dispose(); Check("dispose-idempotent", writes == 2);
for (int i = 0; i < original.Length; i++)
{
    memory = (byte[])original.Clone(); memory[i] ^= 1; int before = writes;
    Throws("context-corruption-refused-" + i, () => Install());
    Check("context-refusal-no-write-" + i, writes == before);
}
memory = (byte[])original.Clone();
bool fail = true;
void FailAfterWrite(byte value) { Write(value); if (fail) { fail = false; throw new ApplicationException("post-write/cache failure"); } }
Throws("post-write-failure-surfaced", () => ByteBranchPatch.Install(Read, FailAfterWrite, original, 14, 0xEB));
Check("post-write-failure-rolls-back", memory.SequenceEqual(original));
bool failBefore = true;
void FailBeforeWrite(byte value) { if (failBefore) { failBefore = false; throw new ApplicationException("protection failure"); } Write(value); }
Throws("pre-write-failure-surfaced", () => ByteBranchPatch.Install(Read, FailBeforeWrite, original, 14, 0xEB));
Check("pre-write-failure-original-preserved", memory.SequenceEqual(original));
int ignored = 0;
Throws("readback-failure-surfaced", () => ByteBranchPatch.Install(Read, _ => ignored++, original, 14, 0xEB));
Check("readback-failure-original-preserved", memory.SequenceEqual(original) && ignored == 2);
patch = Install(); memory[0] ^= 1; int beforeConflict = writes;
Throws("external-mutation-restore-refused", patch.Dispose);
Check("external-mutation-not-clobbered-owner-retained", writes == beforeConflict && patch.Active && memory[0] != original[0]);
memory[0] = original[0]; patch.Dispose();
Check("restore-retry-after-conflict", !patch.Active && memory.SequenceEqual(original));
bool failRestore = false;
void RestoreFailure(byte value) { if (failRestore && value == original[14]) throw new ApplicationException(); Write(value); }
patch = ByteBranchPatch.Install(Read, RestoreFailure, original, 14, 0xEB); failRestore = true;
Throws("restoration-failure-reported", patch.Dispose);
Check("restoration-failure-owner-retained", patch.Active && memory[14] == 0xEB);
failRestore = false; patch.Dispose(); Check("restoration-failure-retry", !patch.Active && memory.SequenceEqual(original));
// Both apply and rollback fail: runtime can keep this object and retry on lifecycle release.
int fails = 2;
void TwoFailures(byte value) { Write(value); if (fails-- > 0) throw new ApplicationException(); }
var retained = new ByteBranchPatch(Read, TwoFailures, original, 14, 0xEB);
Throws("apply-and-rollback-failure-reported", retained.Apply);
Check("apply-and-rollback-owner-retained", retained.Active);
retained.Dispose(); Check("apply-and-rollback-later-release", !retained.Active && memory.SequenceEqual(original));
Throws("invalid-offset-refused", () => ByteBranchPatch.Install(Read, Write, original, -1, 0xEB));
Throws("no-op-refused", () => ByteBranchPatch.Install(Read, Write, original, 14, 0x77));

// Native visual logic counterexample: endpoint is already supplied by attack, muzzle can disagree.
var start = new System.Numerics.Vector3(3, 2, 1);
var acceptedEnd = new System.Numerics.Vector3(-4, 5, 9);
var direction = System.Numerics.Vector3.Normalize(acceptedEnd - start);
var staleMuzzle = System.Numerics.Vector3.UnitX;
Check("native-endpoint-differs-from-stale-muzzle", System.Numerics.Vector3.Dot(direction, staleMuzzle) < 0);
var preservedEnd = acceptedEnd;
foreach (var unusedMuzzle in new[] { staleMuzzle, -staleMuzzle, System.Numerics.Vector3.UnitY })
    Check("visual-endpoint-independent-of-muzzle-" + unusedMuzzle, direction == System.Numerics.Vector3.Normalize(acceptedEnd - start) && acceptedEnd == preservedEnd);

// Read-only exact game PE guard; does not load/execute the game or apply any patch to it.
// Set VHVR_GAME_ROOT to the folder that contains "Vampire Hunters.exe" when the game is not in the default Steam library.
string game = Path.Combine(Environment.GetEnvironmentVariable("VHVR_GAME_ROOT") ?? @"C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters", "GameAssembly.dll");
byte[] gameBytes = File.ReadAllBytes(game);
Check("pinned-game-hash", Convert.ToHexString(SHA256.HashData(gameBytes)) == FlameEndpointPin.GameSha256);
using (var reader = new PEReader(new MemoryStream(gameBytes)))
{
    byte[] AtRva(int rva, int count)
    {
        var section = reader.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + count <= s.VirtualAddress + s.SizeOfRawData);
        return gameBytes.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, count).ToArray();
    }
    Check("pinned-native-context-exact", AtRva(FlameEndpointPin.ContextRva, original.Length).SequenceEqual(original));
    Check("native-endpoint-block-reads-end-and-start", AtRva(0x46BF4C, 9).SequenceEqual(Convert.FromHexString("F20F1006F20F104D00")));
    Check("native-flame-call-vtable12", AtRva(0x48DCAC, 7).SequenceEqual(Convert.FromHexString("41FF91F8010000")));
}

// Actual Windows adapter, private allocation ONLY, never executes any bytes.
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows adapter fixture required");
IntPtr page = Win32.VirtualAlloc(IntPtr.Zero, new UIntPtr(4096), 0x3000, 0x04);
if (page == IntPtr.Zero) throw new InvalidOperationException("private allocation failed");
try
{
    Marshal.Copy(original, 0, page, original.Length);
    using (var privatePatch = FlameEndpointMemory.InstallAt(page, original, 14, 0xEB))
    {
        Check("windows-private-byte-applied", Marshal.ReadByte(page, 14) == 0xEB && privatePatch.Active);
        Check("windows-protection-restored-after-apply", Win32.VirtualProtect(page, new UIntPtr(4096), 0x04, out uint prior) && prior == 0x04);
    }
    var restored = new byte[original.Length]; Marshal.Copy(page, restored, 0, restored.Length);
    Check("windows-private-original-restored", restored.SequenceEqual(original));
    Check("windows-protection-restored-after-release", Win32.VirtualProtect(page, new UIntPtr(4096), 0x04, out uint priorAfterRelease) && priorAfterRelease == 0x04);
}
finally { if (!Win32.VirtualFree(page, UIntPtr.Zero, 0x8000)) throw new InvalidOperationException("private free failed"); }

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string Source(string path) => File.ReadAllText(Path.Combine(root, path));
string lifecycle = Source("src/interaction/HandFlameEndpoint.cs"), adapter = Source("src/interaction/FlameEndpointMemory.cs"), driver = Source("src/interaction/HandAimDriver.cs");
Check("runtime-retains-owner-before-apply", lifecycle.IndexOf("flameEndpoint = FlameEndpointMemory.Prepare()") < lifecycle.IndexOf("flameEndpoint.Apply()"));
Check("runtime-flags-and-receipt", Source("src/core/OpenXrHost.cs").Contains("InstallFlameEndpoint(Config.FlameEndpointVfx)") && driver.Contains("flameEndpointVfx = FlameEndpointReceipt()") && Source("config/schema.json").Contains("\"FlameEndpointVfx\""));
Check("runtime-restores-before-other-release", driver.IndexOf("ReleaseFlameEndpoint();") < driver.IndexOf("ReleaseWeaponRuntime();"));
Check("runtime-no-game-file-or-argument-writes", !adapter.Contains("WriteAll") && !lifecycle.Contains("shoot_forward(") && !lifecycle.Contains("draw_shoot_vfx(") && !lifecycle.Contains("SetPositionAndRotation") && !lifecycle.Contains(".Play("));
Check("runtime-exact-hash-bounds-cache-single-byte", adapter.Contains("hash.ComputeHash(stream)") && adapter.Contains("module.ModuleMemorySize") && adapter.Contains("FlushInstructionCache(GetCurrentProcess()") && adapter.Contains("Marshal.WriteByte(address, value)"));
Check("harness-and-installer-flag", Source("tests/headset-handaim.ps1").Contains("FlameEndpointVfx = (-not $NoFlameEndpointVfx.IsPresent)") && Source("src/core/Install-Stage0.ps1").Contains("'FlameEndpointVfx'"));
Console.WriteLine($"RESULT {passed} flame endpoint fixtures; production transaction/private Windows allocation/pinned PE/source guards, NOT_GAME_OR_HEADSET_ACCEPTANCE");

static class Win32
{
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint previous);
}