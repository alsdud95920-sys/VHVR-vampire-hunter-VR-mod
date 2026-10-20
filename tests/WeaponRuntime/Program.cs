using VampireHuntersVR.Interaction;
int passed=0;
void Check(string name,bool ok) { if(!ok) throw new InvalidOperationException(name); Console.WriteLine("PASS "+name); passed++; }
Check("start-on-press",HandFirePolicy.StartThisTick(TriggerEdge.Pressed,true,true));
Check("no-restart-while-held",!HandFirePolicy.StartThisTick(TriggerEdge.None,true,true));
Check("release-never-starts",!HandFirePolicy.StartThisTick(TriggerEdge.Released,false,true));
Check("compatibility-explicit",HandFirePolicy.StartThisTick(TriggerEdge.None,true,false));
foreach(int fps in new[]{30,60,90,120,240})
{
    bool held=false; int starts=0, shots=0; double timer=0; bool shooting=false;
    // Native-path surrogate: start sets shooting and resets timer; update consumes timer.
    // This checks policy invariants, not native game RPM/damage or full automatic-weapon compatibility.
    for(int i=0;i<fps*3;i++)
    {
        var edge=HandFirePolicy.Next(held,1,true,out held);
        if(HandFirePolicy.StartThisTick(edge,held,true)) { starts++; shots++; shooting=true; timer=0; }
        if(shooting) { timer+=1.0/fps; while(timer+1e-9>=0.1) { shots++; timer-=0.1; } }
    }
    Check($"{fps}fps-one-start",starts==1);
    Check($"{fps}fps-stock-surrogate-timer-preserved",shots==31);
    var release=HandFirePolicy.Next(held,0,true,out held);
    Check($"{fps}fps-release-edge",release==TriggerEdge.Released&&!held);
    var restart=HandFirePolicy.Next(held,1,true,out held);
    Check($"{fps}fps-restart-once",HandFirePolicy.StartThisTick(restart,held,true));
}
bool primary=false,secondary=true;
WorldPoseScope<(bool,bool)> Held(bool h)=>WorldPoseScope<(bool,bool)>.Enter(()=> (primary,secondary),
    p=>{primary=p.Item1;secondary=p.Item2;},Array.Empty<(Func<(bool,bool)>,Action<(bool,bool)>)>(),(h,h));
using(Held(true))
{
    Check("owned-left-sees-held",primary&&secondary);
    using(Held(false)) Check("nested-right-does-not-inherit-left",!primary&&!secondary);
    Check("nested-right-restores-left",primary&&secondary);
}
Check("restores-real-input-after-weapon",!primary&&secondary);
try { using(Held(true)) throw new ApplicationException(); } catch(ApplicationException) { }
Check("original-exception-restores-input",!primary&&secondary);
int calls=0;
try { WorldPoseScope<(bool,bool)>.Enter(()=> (primary,secondary),p=>{primary=p.Item1;if(++calls==1)throw new ApplicationException();secondary=p.Item2;},
    Array.Empty<(Func<(bool,bool)>,Action<(bool,bool)>)>(),(true,true)); } catch(ApplicationException) { }
Check("partial-entry-failure-rolls-back",!primary&&secondary);
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
string Read(string path)=>File.ReadAllText(Path.Combine(root,path));
string bridge=Read("src/interaction/WeaponRuntimeBridge.cs"),fire=Read("src/interaction/HandFireTrigger.cs"),host=Read("src/core/OpenXrHost.cs");
Check("production-uses-tested-edge-policy",fire.Contains("HandFirePolicy.StartThisTick(edge, held, StockFireCadence)"));
Check("native-update-not-manually-invoked",!bridge.Contains(".update(")&&!bridge.Contains("shoot_parent(")&&!bridge.Contains("current_ammo ="));
Check("safe-prefix-no-value-type-argument",bridge.Contains("BeforeWeaponUpdate(Weapon __instance, out HeldInvocation? __state)")&&!bridge.Contains("float __0")&&!bridge.Contains("Vector3 __0"));
Check("stock-held-projection-production-transaction",bridge.Contains("WorldPoseScope<(bool, bool, bool, bool)>.Enter")&&bridge.Contains("input.firePressing = p.Item1")&&bridge.Contains("input.fire2Pressing = p.Item2"));
Check("no-primary-input-events-or-global-held-write",!bridge.Contains("FirePressedInput(")&&!bridge.Contains("Fire2PressedInput("));
Check("input-ownership-scoped-no-passive-top",bridge.Contains("if (slot == null) return;")&&bridge.Contains("__instance.transform.IsChildOf(d.runtimeSource.transform)"));
Check("finalizer-restores-input",bridge.Contains("AfterWeaponUpdate(HeldInvocation? __state)")&&bridge.Contains("__state?.Dispose()"));
Check("head-gaze-hand-world-preserved",bridge.Contains("SetPositionAndRotation(headPos, headRot)")&&bridge.Contains("p.Root.SetPositionAndRotation(p.Position, p.Rotation)"));
Check("gaze-keeps-native-animation-no-reparent",!bridge.Contains("SetParent(")&&!bridge.Contains("localRotation =")&&!bridge.Contains("-90"));
Check("gaze-before-after-player-and-outside-shot",bridge.Contains("ShotScopeDepth != 0")&&bridge.Contains("AfterPlayerUpdate(PlayerController __instance)")&&bridge.Split("d.ApplyAutonomousGaze(d.runtimeSource);").Length == 3);
Check("teardown-restores-local-camera-and-unpatches",bridge.Contains("SetLocalPositionAndRotation(sourceRestPosition, sourceRestRotation)")&&bridge.Contains("weaponRuntimeHarmony?.UnpatchSelf()"));
// D-181 (TOOL-095): the host's option properties live in OpenXrHostConfig.cs since the D-178 source recovery, and the
// recovered host spells the InstallWeaponRuntime call without the original null-forgiving "!" - same call, same arguments.
string hostConfig=Read("src/core/OpenXrHostConfig.cs");
Check("flags-default-on-and-wired",hostConfig.Contains("public bool StockFireCadence { get; set; } = true")&&hostConfig.Contains("public bool AutonomousHeadAim { get; set; } = true")&&host.Contains("handAim.InstallWeaponRuntime(source, player)"));
Check("native-held-update-keeps-independent-shot-scope",bridge.Contains("driver.BeginShotScope(driver.runtimeSource!, aim)")&&bridge.Contains("finally { scope.Dispose(); }"));
// Fake-node head-parent transfer: top/passive retain local animated offsets; controllers retain world poses.
foreach(float yaw in new[]{-90f,0f,90f})
{
    float head=0,leftWorld=35,rightWorld=-42,topLocal=12,passiveLocal=-18;
    float leftLocal=leftWorld-head,rightLocal=rightWorld-head;
    head=yaw; leftLocal=leftWorld-head; rightLocal=rightWorld-head;
    Check($"gaze-{yaw}-hands-world-invariant",head+leftLocal==35&&head+rightLocal==-42);
    Check($"gaze-{yaw}-autonomous-local-offset-follows-head",head+topLocal==yaw+12&&head+passiveLocal==yaw-18);
}
// D-112 fake-parent translation demonstrates the acquisition poison, without claiming Unity execution.
float authoredLocal = 0.4f, flatCamera = 100f, newHead = 120f;
float oldPreservedLocal = flatCamera + authoredLocal - newHead;
Check("new-hand-old-world-preserve-reproduces-rejection", !HandAimMath.RestPlausible(oldPreservedLocal, 0, 0, HandAimPolicy.MaxRestLocalDistance));
float inheritedLocal = HandAimPolicy.PreserveWorldForGaze(false) ? oldPreservedLocal : authoredLocal;
Check("new-hand-inherits-head-with-valid-authored-rest", HandAimPolicy.ShouldCaptureRest(false, false, inheritedLocal, 0, 0));
float drivenWorld = 135f, liveDrivenLocal = drivenWorld - newHead;
Check("driven-local-is-not-authored-rest", !HandAimMath.RestPlausible(liveDrivenLocal, 0, 0, HandAimPolicy.MaxRestLocalDistance) && HandAimMath.RestPlausible(authoredLocal, 0, 0, HandAimPolicy.MaxRestLocalDistance));
Check("production-gaze-only-preserves-written-roots", bridge.Contains("root == left.Container && HandAimPolicy.PreserveWorldForGaze(left.HasWritten)") && bridge.Contains("root == right.Container && HandAimPolicy.PreserveWorldForGaze(right.HasWritten)") && bridge.Contains("root.parent == source.transform && driven"));
// D-113: input-only projection reproduces a stale player-held reload branch.
bool inputPrimary=false,inputSecondary=false,playerPrimary=true,playerSecondary=true;
(bool,bool,bool,bool) CaptureHeld()=> (inputPrimary,inputSecondary,playerPrimary,playerSecondary);
void WriteHeld((bool,bool,bool,bool) p)
{
    try { inputPrimary=p.Item1; }
    finally { try { inputSecondary=p.Item2; }
        finally { try { playerPrimary=p.Item3; } finally { playerSecondary=p.Item4; } } }
}
var stockHeld=CaptureHeld();
using(WorldPoseScope<(bool,bool)>.Enter(()=> (inputPrimary,inputSecondary),
    p=>{inputPrimary=p.Item1;inputSecondary=p.Item2;},Array.Empty<(Func<(bool,bool)>,Action<(bool,bool)>)>(),(false,false)))
    Check("input-only-reproduces-stale-player-reload-input",!inputPrimary&&!inputSecondary&&playerPrimary&&playerSecondary);
WorldPoseScope<(bool,bool,bool,bool)> OwnedHeld(bool held)=>WorldPoseScope<(bool,bool,bool,bool)>.Enter(
    CaptureHeld,WriteHeld,Array.Empty<(Func<(bool,bool,bool,bool)>,Action<(bool,bool,bool,bool)>)>(),(held,held,held,held));
using(OwnedHeld(true))
{
    Check("owned-right-all-native-readers-held",CaptureHeld()==(true,true,true,true));
    using(OwnedHeld(false)) Check("nested-unheld-left-all-native-readers-released",CaptureHeld()==(false,false,false,false));
    Check("nested-left-restores-right-native-state",CaptureHeld()==(true,true,true,true));
    using(OwnedHeld(true)) Check("both-held-owned-nested-state",CaptureHeld()==(true,true,true,true));
}
Check("owned-update-restores-all-four-stock-values",CaptureHeld()==stockHeld);
try { using(OwnedHeld(false)) throw new ApplicationException("native weapon threw"); } catch(ApplicationException) { }
Check("native-exception-restores-all-four-values",CaptureHeld()==stockHeld);
for(int failAt=0;failAt<4;failAt++)
{
    WriteHeld(stockHeld); bool failedOnce=false;
    void Set(int index,Action setter) { setter(); if(index==failAt&&!failedOnce) {failedOnce=true;throw new ApplicationException();} }
    void FailWrite((bool,bool,bool,bool) p)
    {
        try { Set(0,()=>inputPrimary=p.Item1); }
        finally { try { Set(1,()=>inputSecondary=p.Item2); }
            finally { try { Set(2,()=>playerPrimary=p.Item3); } finally { Set(3,()=>playerSecondary=p.Item4); } } }
    }
    try { WorldPoseScope<(bool,bool,bool,bool)>.Enter(CaptureHeld,FailWrite,
        Array.Empty<(Func<(bool,bool,bool,bool)>,Action<(bool,bool,bool,bool)>)>(),(false,false,false,false)); }
    catch(ApplicationException) { }
    Check("partial-four-state-entry-rollback-"+failAt,failedOnce&&CaptureHeld()==stockHeld);
}
Check("production-player-held-projection-and-restoration",bridge.Contains("player.is_holding_down_shoot_button = p.Item3")&&bridge.Contains("player.is_holding_down_seccond_shoot_button = p.Item4")&&bridge.Contains("(held, secondary, held, secondary)"));
Check("production-passes-owned-player-to-transaction",bridge.Contains("new HeldInvocation(d, d.runtimePlayer, input, held, secondary, d.AimFor(__instance))"));
string shot=Read("src/interaction/HandShot.cs"),diagnostics=Read("src/interaction/HandShotDiagnostics.cs"),driver=Read("src/interaction/HandAimDriver.cs");
Check("actual-shots-record-hand-held-without-blanket-suppression",shot.Contains("s.LeftShotsWithoutHeld++")&&shot.Contains("s.RightShotsWithoutHeld++")&&!shot.Contains("if (!held) return false")&&diagnostics.Contains("heldL=")&&diagnostics.Contains("heldR=")&&driver.Contains("leftShotsWithoutHeld = Shot.LeftShotsWithoutHeld"));
Console.WriteLine($"RESULT {passed} weapon runtime fixtures; policy/transaction/surrogate/source only, NOT_HEADSET_ACCEPTANCE");