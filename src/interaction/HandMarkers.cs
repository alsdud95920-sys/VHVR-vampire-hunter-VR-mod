using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Object = UnityEngine.Object;

// The shipped decompile predates nullable reference annotations: the whole file is analysed as oblivious.
#nullable disable



namespace VampireHuntersVR.Interaction;

// D-181: REPLACED with the shipped D-171 marker code (taken from the shipped DLL decompile). The D-178 source recovery had
// regenerated an OLDER revision of this file, which is why potion/chest arrows vanished. Behaviour = what the user had in 0.6.
internal sealed partial class HandAimDriver
{
    private sealed class MarkerSource
    {
        public Transform Target;
        public string Kind = "";
    }

    private sealed class MarkerQuad
    {
        public GameObject Go;
        public Renderer Renderer;
        public string Kind = "";
        public bool OffGaze;
    }

public bool MarkersEnabled;

public string MarkersMode = "off";

public string MarkerKindsCsv = "CHEST,HEALING,POWER_UP,BOSS_CHEST";

public string[] MarkerKinds = new string[1] { "CHEST" };

public float MarkerMaxDistance;

public int MarkerMaxCount = 32;

public int MarkerScans;

public int MarkerPickups;

public int MarkerChests;

public int MarkerCandidates;

public int MarkerShown;

public int MarkerOffGaze;

public int MarkerHealthPoints;

public int MarkerArrows;

public int MarkerArrowDamage;

public int MarkerArrowOther;

public int MarkerArrowNamed;

public int MarkerArrowUnnamed;

public int MarkerStatues;

public int MarkerStatuesEmpty;

public int MarkerPotionObjects;

public int MarkerChestTargets;

public int MarkerChestTargetsSkipped;

public int MarkerArrowStatueSkipped;

public int MarkerSourceDuplicates;

public int MarkerPickupObjects;

private readonly List<string> markerPickupSample = new List<string>();

public int MarkerPositionDuplicates;

public int MarkerHangingPotions;

// D-183: where MarkersPlace bailed out, so a "no arrows" sitting is diagnosable from the receipt alone.
public int MarkerPlaceCalls, MarkerPlaceNoHead, MarkerPlaceNoMaterial, MarkerScanSources;

private readonly List<string> markerStatueSample = new List<string>();

private readonly List<string> markerPotionSample = new List<string>();

private readonly List<string> markerChestSample = new List<string>();

private string markerCurrentPickup = "";

private bool markerZTestForced;

private bool markerBlendForced;

private readonly List<string> markerArrowSample = new List<string>();

private readonly HashSet<int> markerStatueIds = new HashSet<int>();

private readonly HashSet<int> markerSourceIds = new HashSet<int>();

public string MarkerError;

public string MarkerSample = "";

private readonly List<MarkerSource> markerSources = new List<MarkerSource>();

private readonly List<MarkerQuad> markerQuads = new List<MarkerQuad>();

private DateTime nextMarkerScan;

private Material markerMaterial;

private string markerShader = "";

private Mesh markerArrowMesh;


private void MarkersTick(Camera source)
{
    if (!MarkersEnabled)
    {
        return;
    }
    try
    {
        if (DateTime.UtcNow >= nextMarkerScan)
        {
            nextMarkerScan = DateTime.UtcNow.AddSeconds(0.5);
            MarkerScan(source);
        }
    }
    catch (Exception ex)
    {
        if (MarkerError == null)
        {
            MarkerError = ex.GetType().Name + ": " + ex.Message;
        }
    }
}

private void MarkerScan(Camera source)
{
    markerSources.Clear();
    MarkerPickups = 0;
    MarkerChests = 0;
    MarkerScans++;
    MarkerHealthPoints = 0;
    MarkerArrows = 0;
    MarkerArrowDamage = 0;
    MarkerArrowOther = 0;
    MarkerArrowNamed = 0;
    MarkerArrowUnnamed = 0;
    MarkerStatues = 0;
    MarkerStatuesEmpty = 0;
    MarkerPotionObjects = 0;
    MarkerChestTargets = 0;
    MarkerChestTargetsSkipped = 0;
    MarkerArrowStatueSkipped = 0;
    MarkerSourceDuplicates = 0;
    markerArrowSample.Clear();
    markerStatueIds.Clear();
    markerSourceIds.Clear();
    MarkerPickupObjects = 0;
    markerPickupSample.Clear();
    MarkerPositionDuplicates = 0;
    MarkerHangingPotions = 0;
    markerStatueSample.Clear();
    markerCurrentPickup = "";
    markerPotionSample.Clear();
    markerChestSample.Clear();
    foreach (HealthPoints item in Resources.FindObjectsOfTypeAll<HealthPoints>())
    {
        if ((Object)(object)item == (Object)null)
        {
            continue;
        }
        Il2CppReferenceArray<Transform> health_points_object = item.health_points_object;
        if (health_points_object != null)
        {
            int length = ((Il2CppArrayBase<Transform>)(object)health_points_object).Length;
            for (int i = 0; i < length; i++)
            {
                Transform val = ((Il2CppArrayBase<Transform>)(object)health_points_object)[i];
                if (!((Object)(object)val == (Object)null) && ((Component)val).gameObject.activeInHierarchy)
                {
                    MarkerStatues++;
                    markerStatueIds.Add(((Object)val).GetInstanceID());
                    MarkerHealthPoints++;
                    if (markerStatueSample.Count < 6)
                    {
                        markerStatueSample.Add(StatueSample(val));
                    }
                    if (!AddPotionAt(((Component)val).GetComponentInChildren<PickUp>()))
                    {
                        AddHangingPotion(val);
                    }
                }
            }
        }
        GameObject current_pickup = item.current_pickup;
        if ((Object)(object)current_pickup != (Object)null)
        {
            if (markerCurrentPickup.Length == 0)
            {
                markerCurrentPickup = ((Object)current_pickup).name + "#self=" + (current_pickup.activeSelf ? "y" : "n") + "#hier=" + (current_pickup.activeInHierarchy ? "y" : "n") + "#pickup=" + (((Object)(object)current_pickup.GetComponentInChildren<PickUp>() != (Object)null) ? "y" : "n");
            }
            if (current_pickup.activeInHierarchy)
            {
                AddPotionAt(current_pickup.GetComponentInChildren<PickUp>());
            }
        }
    }
    foreach (PickUp item2 in Resources.FindObjectsOfTypeAll<PickUp>())
    {
        if (!((Object)(object)item2 == (Object)null))
        {
            string text = ((object)item2.type/*cast due to constrained. prefix*/).ToString();
            MarkerPickupObjects++;
            if (markerPickupSample.Count < 12)
            {
                List<string> list = markerPickupSample;
                string text2 = text;
                string text3;
                if (((Component)item2).gameObject.activeInHierarchy)
                {
                    text3 = "@live";
                }
                else
                {
                    text3 = (((Component)item2).gameObject.activeSelf ? "@self" : "@off");
                }
                list.Add(text2 + text3);
            }
            if (((Component)item2).gameObject.activeInHierarchy)
            {
                AddPickupSource(item2);
            }
        }
    }
    foreach (TargetHUDArrow item3 in Resources.FindObjectsOfTypeAll<TargetHUDArrow>())
    {
        if ((Object)(object)item3 == (Object)null || !((Component)item3).gameObject.activeInHierarchy)
        {
            continue;
        }
        string text4 = UiPathOf(((Component)item3).transform);
        if (text4.Contains("DemageArrows", StringComparison.Ordinal) || text4.Contains("DamageArrows", StringComparison.Ordinal))
        {
            MarkerArrowDamage++;
            continue;
        }
        if (!text4.Contains("POIArrows", StringComparison.Ordinal))
        {
            MarkerArrowOther++;
            continue;
        }
        Transform target = item3.target;
        if ((Object)(object)target == (Object)null)
        {
            continue;
        }
        MarkerArrows++;
        string text5 = MarkerKindOf(target);
        if (text5.Length == 0)
        {
            MarkerArrowStatueSkipped++;
            continue;
        }
        if (text5 == "POI")
        {
            MarkerArrowUnnamed++;
        }
        else
        {
            MarkerArrowNamed++;
        }
        if (markerArrowSample.Count < 12)
        {
            markerArrowSample.Add(text5 + "@" + text4);
        }
        if (!WorldMarkerPolicy.Wants(MarkerKinds, text5))
        {
            continue;
        }
        PickUp componentInParent = ((Component)target).GetComponentInParent<PickUp>();
        if ((Object)(object)componentInParent != (Object)null)
        {
            AddPickupSource(componentInParent);
            continue;
        }
        if (text5 == "CHEST" && markerChestSample.Count < 8)
        {
            markerChestSample.Add(ChestSample("poi", text5, target));
        }
        AddSource(target, text5);
    }
    foreach (PoolingSystem item4 in Resources.FindObjectsOfTypeAll<PoolingSystem>())
    {
        if ((Object)(object)item4 == (Object)null)
        {
            continue;
        }
        var active_pickups = item4.active_pickups;
        if (active_pickups == null)
        {
            continue;
        }
        int count = active_pickups.Count;
        for (int j = 0; j < count; j++)
        {
            PickUp val2 = active_pickups[j];
            if (!((Object)(object)val2 == (Object)null) && ((Component)val2).gameObject.activeInHierarchy)
            {
                MarkerPickups++;
                AddPickupSource(val2);
            }
        }
    }
    if (!WorldMarkerPolicy.Wants(MarkerKinds, "CHEST") && !WorldMarkerPolicy.Wants(MarkerKinds, "BOSS_CHEST"))
    {
        return;
    }
    foreach (RewardBox item5 in Resources.FindObjectsOfTypeAll<RewardBox>())
    {
        if (!((Object)(object)item5 == (Object)null) && ((Component)item5).gameObject.activeInHierarchy)
        {
            MarkerChests++;
            if (markerChestSample.Count < 4)
            {
                markerChestSample.Add(ChestSample("rewardbox", "CHEST", ((Component)item5).transform));
            }
            if (WorldMarkerPolicy.Wants(MarkerKinds, "CHEST"))
            {
                AddSource(((Component)item5).transform, "CHEST");
            }
        }
    }
    foreach (MiniMapTargets item6 in Resources.FindObjectsOfTypeAll<MiniMapTargets>())
    {
        if ((Object)(object)item6 == (Object)null)
        {
            continue;
        }
        var mini_map_targets_queue = item6.mini_map_targets_queue;
        if (mini_map_targets_queue == null)
        {
            continue;
        }
        int count2 = mini_map_targets_queue.Count;
        for (int k = 0; k < count2; k++)
        {
            var val3 = mini_map_targets_queue[k];
            Transform target_transform = val3.target_transform;
            if ((Object)(object)target_transform == (Object)null || !((Component)target_transform).gameObject.activeInHierarchy)
            {
                continue;
            }
            string text6 = WorldMarkerPolicy.ChestKindFromTargetType(((object)val3.type/*cast due to constrained. prefix*/).ToString());
            if (text6.Length == 0)
            {
                MarkerChestTargetsSkipped++;
                continue;
            }
            MarkerChestTargets++;
            if (markerChestSample.Count < 4)
            {
                markerChestSample.Add(ChestSample("minimap", text6, target_transform));
            }
            if (WorldMarkerPolicy.Wants(MarkerKinds, text6))
            {
                AddSource(target_transform, text6);
            }
        }
    }

    // D-183: the scan source count, so an empty scan is distinguishable from a place-step bail-out.
    MarkerScanSources = markerSources.Count;
}

private Vector3 MarkerWorldOf(Transform target)
{
    return target.position + Vector3.up * 0.35f;
}

private void AddSource(Transform target, string kind)
{
    if (((Component)target).gameObject.activeInHierarchy && WorldMarkerPolicy.Wants(MarkerKinds, kind))
    {
        if (!markerSourceIds.Add(((Object)target).GetInstanceID()))
        {
            MarkerSourceDuplicates++;
            return;
        }
        markerSources.Add(new MarkerSource
        {
            Target = target,
            Kind = kind
        });
    }
}

private bool AddPotionAt(PickUp potion)
{
    bool flag = (Object)(object)potion != (Object)null;
    string text = (flag ? ((object)potion.type/*cast due to constrained. prefix*/).ToString() : null);
    bool flag2 = flag && ((Component)potion).gameObject.activeInHierarchy;
    if (!flag || !flag2 || !WorldMarkerPolicy.IsPotionKind(text))
    {
        return false;
    }
    if (!WorldMarkerPolicy.CanMarkPotion(flag, flag2, MarkerKinds, text))
    {
        return true;
    }
    Transform transform = ((Component)potion).transform;
    if ((Object)(object)transform == (Object)null || !((Component)transform).gameObject.activeInHierarchy)
    {
        return false;
    }
    MarkerPotionObjects++;
    if (markerPotionSample.Count < 12)
    {
        markerPotionSample.Add(ChestSample("confirmed-pickup", text, transform) + "#parent=" + (((Object)(object)transform.parent != (Object)null) ? ((Object)transform.parent).name : "none"));
    }
    AddSource(transform, text);
    return true;
}

private void AddHangingPotion(Transform statue)
{
    Transform val = HangingPotionOf(statue);
    if ((Object)(object)val == (Object)null)
    {
        MarkerStatuesEmpty++;
        return;
    }
    MarkerHangingPotions++;
    if (markerPotionSample.Count < 12)
    {
        markerPotionSample.Add(ChestSample("hanging-model", "HEALING", val) + "#holder=" + ((Object)statue).name);
    }
    AddSource(val, "HEALING");
}

private static Transform HangingPotionOf(Transform statue)
{
    Il2CppArrayBase<Renderer> componentsInChildren = ((Component)statue).GetComponentsInChildren<Renderer>();
    for (int i = 0; i < componentsInChildren.Length; i++)
    {
        Renderer val = componentsInChildren[i];
        if (!((Object)(object)val == (Object)null) && !((Object)(object)((Component)val).transform == (Object)(object)statue) && WorldMarkerPolicy.IsHangingPotion(((Object)((Component)val).gameObject).name, ((Component)val).gameObject.activeInHierarchy, val.enabled))
        {
            return ((Component)val).transform;
        }
    }
    return null;
}

private void AddPickupSource(PickUp pickup)
{
    string text = ((object)pickup.type/*cast due to constrained. prefix*/).ToString();
    if (WorldMarkerPolicy.IsPotionKind(text))
    {
        AddPotionAt(pickup);
    }
    else if (((Component)pickup).gameObject.activeInHierarchy && WorldMarkerPolicy.Wants(MarkerKinds, text))
    {
        Transform transform = ((Component)pickup).transform;
        if (text == "BOSS_CHEST" && markerChestSample.Count < 8)
        {
            markerChestSample.Add(ChestSample("pickup", text, transform));
        }
        AddSource(transform, text);
    }
}

private static string ChestSample(string source, string kind, Transform target)
{
    int num = 0;
    try
    {
        Il2CppArrayBase<Renderer> componentsInChildren = ((Component)target).GetComponentsInChildren<Renderer>();
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            if ((Object)(object)componentsInChildren[i] != (Object)null && componentsInChildren[i].enabled && ((Component)componentsInChildren[i]).gameObject.activeInHierarchy)
            {
                num++;
            }
        }
    }
    catch (Exception)
    {
    }
    Vector3 position = target.position;
    return source + "#" + kind + "#at=" + ((Object)target).name + "#pos=" + position.x.ToString("F1") + "," + position.y.ToString("F1") + "," + position.z.ToString("F1") + "#r=" + num;
}

private static string StatueSample(Transform statue)
{
    int num = 0;
    float num2 = 0f;
    try
    {
        Il2CppArrayBase<Renderer> componentsInChildren = ((Component)statue).GetComponentsInChildren<Renderer>();
        bool flag = false;
        Bounds val = default;
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            Renderer val2 = componentsInChildren[i];
            if (!((Object)(object)val2 == (Object)null) && val2.enabled && ((Component)val2).gameObject.activeInHierarchy)
            {
                num++;
                if (!flag)
                {
                    val = val2.bounds;
                    flag = true;
                }
                else
                {
                    val.Encapsulate(val2.bounds);
                }
            }
        }
        if (flag)
        {
            num2 = val.max.y - statue.position.y;
        }
    }
    catch (Exception)
    {
    }
    string text = "";
    try
    {
        text = (((Object)(object)((Component)statue).GetComponentInChildren<PickUp>() != (Object)null) ? "y" : "n");
    }
    catch (Exception)
    {
    }
    StringBuilder stringBuilder = new StringBuilder();
    try
    {
        for (int j = 0; j < statue.childCount && j < 32; j++)
        {
            Transform child = statue.GetChild(j);
            if (!((Object)(object)child == (Object)null))
            {
                stringBuilder.Append(((Object)child).name).Append(((Component)child).gameObject.activeInHierarchy ? "#y" : "#n").Append("@d=")
                    .Append(Vector3.Distance(child.position, statue.position).ToString("F1"))
                    .Append(' ');
            }
        }
    }
    catch (Exception)
    {
    }
    StringBuilder stringBuilder2 = new StringBuilder();
    int remaining = 96;
    try
    {
        AppendHolderNodes(statue, "root", 0, stringBuilder2, ref remaining);
    }
    catch (Exception ex4)
    {
        stringBuilder2.Append("#error=").Append(ex4.GetType().Name);
    }
    Vector3 position = statue.position;
    return ((Object)statue).name + "#id=" + ((Object)statue).GetInstanceID() + "#c=" + statue.childCount + "#r=" + num + "#top=" + num2.ToString("F2") + "#pickup=" + text + "#pos=" + position.x.ToString("F2") + "," + position.y.ToString("F2") + "," + position.z.ToString("F2") + " kids[" + stringBuilder.ToString().Trim() + "] nodes[" + stringBuilder2?.ToString() + "]#nodeBudgetRemaining=" + remaining;
}

private static void AppendHolderNodes(Transform node, string path, int depth, StringBuilder text, ref int remaining)
{
    if (remaining <= 0)
    {
        return;
    }
    remaining--;
    Vector3 position = node.position;
    PickUp component = ((Component)node).GetComponent<PickUp>();
    Renderer component2 = ((Component)node).GetComponent<Renderer>();
    StringBuilder stringBuilder = text.Append(path).Append('/').Append(((Object)node).name)
        .Append("#id=")
        .Append(((Object)node).GetInstanceID())
        .Append("#self=")
        .Append(((Component)node).gameObject.activeSelf)
        .Append("#live=")
        .Append(((Component)node).gameObject.activeInHierarchy)
        .Append("#pos=")
        .Append(position.x.ToString("F2"))
        .Append(',')
        .Append(position.y.ToString("F2"))
        .Append(',')
        .Append(position.z.ToString("F2"))
        .Append("#pickup=")
        .Append(((Object)(object)component != (Object)null) ? ((object)component.type/*cast due to constrained. prefix*/).ToString() : "none")
        .Append("#renderer=");
    string value;
    if ((Object)(object)component2 != (Object)null)
    {
        value = (component2.enabled ? "on" : "off");
    }
    else
    {
        value = "none";
    }
    stringBuilder.Append(value).Append(';');
    if (depth >= 4)
    {
        if (node.childCount > 0)
        {
            text.Append("#depthTruncated;");
        }
        return;
    }
    for (int i = 0; i < node.childCount; i++)
    {
        if (remaining <= 0)
        {
            break;
        }
        AppendHolderNodes(node.GetChild(i), path + "/" + i, depth + 1, text, ref remaining);
    }
}

private string MarkerKindOf(Transform target)
{
    try
    {
        PickUp componentInParent = ((Component)target).GetComponentInParent<PickUp>();
        if ((Object)(object)componentInParent != (Object)null)
        {
            return ((object)componentInParent.type/*cast due to constrained. prefix*/).ToString();
        }
    }
    catch (Exception)
    {
    }
    try
    {
        if ((Object)(object)((Component)target).GetComponentInParent<RewardBox>() != (Object)null)
        {
            return "CHEST";
        }
    }
    catch (Exception)
    {
    }
    if (IsUnderStatue(target))
    {
        return "";
    }
    return "POI";
}

private bool IsUnderStatue(Transform target)
{
    Transform val = target;
    while ((Object)(object)val != (Object)null)
    {
        if (markerStatueIds.Contains(((Object)val).GetInstanceID()))
        {
            return true;
        }
        val = val.parent;
    }
    return false;
}

private void MarkersPlace()
{
    // D-183 diagnostics: the last run had candidates=0 / shader="" / error="" - the place step bailed out BEFORE
    // touching anything, silently. These counters say exactly which gate bailed (called / no-head / no-material).
    MarkerPlaceCalls++;
    if (!MarkersEnabled)
    {
        MarkersHide();
        return;
    }
    if (!hasHead)
    {
        MarkerPlaceNoHead++;
        MarkersHide();
        return;
    }
    if (!EnsureMarkerMaterial())
    {
        MarkerPlaceNoMaterial++;
        MarkersHide();
        return;
    }
    try
    {
        List<(Transform, string, bool, int, float, Vector3)> list = new List<(Transform, string, bool, int, float, Vector3)>();
        for (int i = 0; i < markerSources.Count; i++)
        {
            Transform target = markerSources[i].Target;
            if (!((Object)(object)target == (Object)null) && ((Component)target).gameObject.activeInHierarchy)
            {
                Vector3 val = MarkerWorldOf(target);
                Vector3 val2 = Quaternion.Inverse(headRot) * (val - headPos);
                float magnitude = val2.magnitude;
                if (WorldMarkerPolicy.WithinDistance(magnitude, MarkerMaxDistance))
                {
                    bool item = WorldMarkerPolicy.IsOffGaze(new float[3] { val2.x, val2.y, val2.z });
                    list.Add((target, markerSources[i].Kind, item, WorldMarkerPolicy.Priority(markerSources[i].Kind), magnitude, val));
                }
            }
        }
        list.Sort(((Transform Target, string Kind, bool OffGaze, int Priority, float Distance, Vector3 World) a, (Transform Target, string Kind, bool OffGaze, int Priority, float Distance, Vector3 World) b) => WorldMarkerPolicy.Compare(a.OffGaze, a.Priority, a.Distance, b.OffGaze, b.Priority, b.Distance));
        List<Vector3> list2 = new List<Vector3>();
        List<(Transform, string, bool, int, float, Vector3)> list3 = new List<(Transform, string, bool, int, float, Vector3)>();
        int num = 0;
        for (int num2 = 0; num2 < list.Count; num2++)
        {
            (Transform, string, bool, int, float, Vector3) item2 = list[num2];
            bool flag = false;
            for (int num3 = 0; num3 < list2.Count; num3++)
            {
                if (WorldMarkerPolicy.IsSameSpot(item2.Item6.x, item2.Item6.y, item2.Item6.z, list2[num3].x, list2[num3].y, list2[num3].z))
                {
                    flag = true;
                    break;
                }
            }
            if (flag)
            {
                num++;
                continue;
            }
            list2.Add(item2.Item6);
            list3.Add(item2);
            if (list3.Count >= MarkerMaxCount)
            {
                break;
            }
        }
        MarkerCandidates = list.Count;
        MarkerPositionDuplicates = num;
        int count = list3.Count;
        int num4 = 0;
        StringBuilder stringBuilder = new StringBuilder();
        for (int num5 = 0; num5 < count; num5++)
        {
            (Transform, string, bool, int, float, Vector3) tuple = list3[num5];
            if (tuple.Item3)
            {
                num4++;
            }
            MarkerQuad markerQuad = MarkerAt(num5);
            if ((Object)(object)markerQuad.Go == (Object)null || (Object)(object)markerQuad.Renderer == (Object)null)
            {
                continue;
            }
            Vector3 val3 = tuple.Item6 - headPos;
            float num6 = WorldMarkerPolicy.DrawDistance(tuple.Item5);
            Vector3 val4 = ((val3.sqrMagnitude > 1E-08f) ? (headPos + val3.normalized * num6) : tuple.Item6);
            Vector3 val5 = val4;
            float num7 = WorldMarkerPolicy.SizeFor(num6);
            Vector3 val6 = headPos - val5;
            Quaternion val9;
            if (tuple.Item3)
            {
                Vector3 val7 = Quaternion.Inverse(headRot) * (tuple.Item6 - headPos);
                float[] array = WorldMarkerPolicy.ClampToVisor(new float[3] { val7.x, val7.y, val7.z });
                val5 = headPos + headRot * new Vector3(array[0], array[1], array[2]) * 2f;
                num7 = 0.12f;
                val6 = headPos - val5;
                Vector3 val8 = headRot * new Vector3(array[0], array[1], 0f);
                if (val8.sqrMagnitude < 1E-08f)
                {
                    val8 = headRot * Vector3.up;
                }
                val9 = ((val6.sqrMagnitude > 1E-08f) ? Quaternion.LookRotation(val6, val8.normalized) : headRot);
            }
            else
            {
                val9 = ((val6.sqrMagnitude > 1E-08f) ? (Quaternion.LookRotation(val6) * Quaternion.Euler(0f, 0f, 180f)) : headRot);
                val5 = val4 - val9 * Vector3.up * (0.5f * num7);
            }
            markerQuad.Go.transform.position = val5;
            markerQuad.Go.transform.rotation = val9;
            markerQuad.Go.transform.localScale = Vector3.one * num7;
            markerQuad.Renderer.sharedMaterial.color = MarkerColor(tuple.Item2, tuple.Item3);
            if (!markerQuad.Go.activeSelf)
            {
                markerQuad.Go.SetActive(true);
            }
            markerQuad.Kind = tuple.Item2;
            markerQuad.OffGaze = tuple.Item3;
            if (stringBuilder.Length < 160)
            {
                stringBuilder.Append(tuple.Item2).Append(tuple.Item3 ? "@ring" : "@world").Append(' ');
            }
        }
        for (int num8 = count; num8 < markerQuads.Count; num8++)
        {
            MarkerQuad markerQuad2 = markerQuads[num8];
            if ((Object)(object)markerQuad2.Go != (Object)null && markerQuad2.Go.activeSelf)
            {
                markerQuad2.Go.SetActive(false);
            }
        }
        MarkerCandidates = list.Count;
        MarkerShown = count;
        MarkerOffGaze = num4;
        MarkerSample = stringBuilder.ToString().Trim();
    }
    catch (Exception ex)
    {
        if (MarkerError == null)
        {
            MarkerError = ex.GetType().Name + ": " + ex.Message;
        }
        MarkersHide();
    }
}

private void MarkersHide()
{
    foreach (MarkerQuad markerQuad in markerQuads)
    {
        if ((Object)(object)markerQuad.Go != (Object)null && markerQuad.Go.activeSelf)
        {
            markerQuad.Go.SetActive(false);
        }
    }
    MarkerShown = 0;
    MarkerOffGaze = 0;
}

private MarkerQuad MarkerAt(int index)
{
    while (markerQuads.Count <= index)
    {
        markerQuads.Add(new MarkerQuad());
    }
    MarkerQuad markerQuad = markerQuads[index];
    if ((Object)(object)markerQuad.Go != (Object)null)
    {
        return markerQuad;
    }
    GameObject val = new GameObject("VHVR_Marker_" + index);
    val.AddComponent<MeshFilter>().sharedMesh = MarkerArrowMesh();
    markerQuad.Renderer = (Renderer)(object)val.AddComponent<MeshRenderer>();
    markerQuad.Renderer.sharedMaterial = new Material(markerMaterial);
    markerQuad.Renderer.shadowCastingMode = (ShadowCastingMode)0;
    markerQuad.Renderer.receiveShadows = false;
    val.SetActive(false);
    markerQuad.Go = val;
    return markerQuad;
}

private Mesh MarkerArrowMesh()
{
    if ((Object)(object)markerArrowMesh != (Object)null)
    {
        return markerArrowMesh;
    }
    float[] arrowVertices = WorldMarkerPolicy.ArrowVertices;
    Vector3[] array = new Vector3[arrowVertices.Length / 3];
    for (int i = 0; i < array.Length; i++)
    {
        array[i] = new Vector3(arrowVertices[i * 3], arrowVertices[i * 3 + 1], arrowVertices[i * 3 + 2]);
    }
    Mesh val = new Mesh
    {
        name = "VHVR_MarkerArrow"
    };
    val.vertices = array;
    val.triangles = WorldMarkerPolicy.ArrowTriangles;
    val.RecalculateBounds();
    markerArrowMesh = val;
    return val;
}

private static Color MarkerColor(string kind, bool offGaze)
{
    Color result = kind switch
    {
        "BOSS_CHEST" => new Color(1f, 0.55f, 0.1f, 1f), 
        "CHEST" => new Color(1f, 0.85f, 0.2f, 1f), 
        "HEALING" => new Color(0.3f, 1f, 0.4f, 1f), 
        "POWER_UP" => new Color(0.3f, 0.7f, 1f, 1f), 
        "RELICS" => new Color(0.8f, 0.4f, 1f, 1f), 
        _ => new Color(0.9f, 0.9f, 0.9f, 1f), 
    };
    if (offGaze)
    {
        result.r *= 0.6f;
        result.g *= 0.6f;
        result.b *= 0.6f;
    }
    result.a = (offGaze ? 0.35f : 0.5f);
    return result;
}

private bool EnsureMarkerMaterial()
{
    if ((Object)(object)markerMaterial != (Object)null)
    {
        return true;
    }
    string[] array = new string[3] { "Hidden/Internal-Colored", "UI/Default", "Sprites/Default" };
    Shader val = null;
    string text = "";
    string[] array2 = array;
    foreach (string text2 in array2)
    {
        Shader val2 = Shader.Find(text2);
        if (!((Object)(object)val2 == (Object)null))
        {
            if ((Object)(object)val == (Object)null)
            {
                val = val2;
                text = text2;
            }
            Material val3 = new Material(val2);
            bool flag = val3.HasProperty("_ZTest");
            Object.DestroyImmediate((Object)val3);
            if (flag)
            {
                val = val2;
                text = text2;
                break;
            }
        }
    }
    if ((Object)(object)val == (Object)null)
    {
        if (!EnsureReticleMaterial())
        {
            if (MarkerError == null)
            {
                MarkerError = "no marker shader found";
            }
            return false;
        }
        markerMaterial = new Material(reticleMaterial);
        markerShader = reticleShader + " (reticle fallback)";
    }
    else
    {
        markerMaterial = new Material(val);
        markerShader = text;
    }
    if (markerMaterial.HasProperty("_Color"))
    {
        markerMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
    }
    markerZTestForced = markerMaterial.HasProperty("_ZTest");
    if (markerZTestForced)
    {
        markerMaterial.SetInt("_ZTest", 8);
    }
    markerBlendForced = markerMaterial.HasProperty("_SrcBlend") && markerMaterial.HasProperty("_DstBlend");
    if (markerMaterial.HasProperty("_SrcBlend"))
    {
        markerMaterial.SetInt("_SrcBlend", 5);
    }
    if (markerMaterial.HasProperty("_DstBlend"))
    {
        markerMaterial.SetInt("_DstBlend", 10);
    }
    if (markerMaterial.HasProperty("_ZWrite"))
    {
        markerMaterial.SetInt("_ZWrite", 0);
    }
    if (markerMaterial.HasProperty("_Cull"))
    {
        markerMaterial.SetInt("_Cull", 0);
    }
    markerMaterial.renderQueue = 5000;
    if (!markerZTestForced && MarkerError == null)
    {
        MarkerError = "marker shader " + markerShader + " has no _ZTest: walls may hide the arrows";
    }
    return true;
}

private void MarkersRelease()
{
    foreach (MarkerQuad markerQuad in markerQuads)
    {
        if ((Object)(object)markerQuad.Go != (Object)null)
        {
            Object.Destroy((Object)(object)markerQuad.Go);
        }
    }
    markerQuads.Clear();
    markerSources.Clear();
    if ((Object)(object)markerMaterial != (Object)null)
    {
        Object.Destroy((Object)(object)markerMaterial);
    }
    markerMaterial = null;
    if ((Object)(object)markerArrowMesh != (Object)null)
    {
        Object.Destroy((Object)(object)markerArrowMesh);
    }
    markerArrowMesh = null;
    MarkerShown = 0;
    MarkerOffGaze = 0;
}

private object MarkerReceipt()
{
    return new
    {
        enabled = MarkersEnabled,
        mode = MarkersMode,
        kinds = MarkerKindsCsv,
        wantedKinds = MarkerKinds,
        maxDistance = MarkerMaxDistance,
        maxCount = MarkerMaxCount,
        scans = MarkerScans,
        livePickups = MarkerPickups,
        liveChests = MarkerChests,
        liveHealthPoints = MarkerHealthPoints,
        liveArrows = MarkerArrows,
        arrowDamageIgnored = MarkerArrowDamage,
        arrowOtherIgnored = MarkerArrowOther,
        arrowNamedKinds = MarkerArrowNamed,
        arrowUnnamedKinds = MarkerArrowUnnamed,
        arrowSample = markerArrowSample.ToArray(),
        statues = MarkerStatues,
        statuesWithoutPotion = MarkerStatuesEmpty,
        potionObjects = MarkerPotionObjects,
        chestTargets = MarkerChestTargets,
        chestTargetsSkipped = MarkerChestTargetsSkipped,
        arrowTargetsSkippedAsStatue = MarkerArrowStatueSkipped,
        duplicateSourcesDropped = MarkerSourceDuplicates,
        livePickupObjects = MarkerPickupObjects,
        pickupSample = markerPickupSample.ToArray(),
        shape = "arrow",
        arrowVertices = WorldMarkerPolicy.ArrowVertices.Length / 3,
        arrowTriangles = WorldMarkerPolicy.ArrowTriangles.Length / 3,
        alpha = 0.5f,
        ringAlpha = 0.35f,
        distanceLimited = WorldMarkerPolicy.IsDistanceLimited(MarkerMaxDistance),
        zTestForced = markerZTestForced,
        blendForced = markerBlendForced,
        positionDuplicatesDropped = MarkerPositionDuplicates,
        placeCalls = MarkerPlaceCalls, placeSkippedNoHead = MarkerPlaceNoHead, placeSkippedNoMaterial = MarkerPlaceNoMaterial,
        scanSources = MarkerScanSources,
        heightOffset = 0.35f,
        hangingPotions = MarkerHangingPotions,
        maxDrawDistance = 100f,
        sizeCapped = false,
        statueSample = markerStatueSample.ToArray(),
        currentPickup = markerCurrentPickup,
        potionSample = markerPotionSample.ToArray(),
        chestSample = markerChestSample.ToArray(),
        candidates = MarkerCandidates,
        shown = MarkerShown,
        offGaze = MarkerOffGaze,
        quads = markerQuads.Count,
        shader = markerShader,
        sample = MarkerSample,
        error = MarkerError,
        note = "World-anchored target arrows: a billboarded, SEMI-TRANSPARENT arrow (D-161, alpha " + 0.5f.ToString("0.00") + ") at the target's own world position, depth test always on (a wall does not hide it), and the same arrow clamped onto a ring 40 degrees off the gaze when the target is outside the view - there the tip points the way to turn, in view it points down at the target. D-166: fixed 0.35 m lift; all typed pickup sources use Component.transform and configured kinds. D-168: a gargoyle is marked only through its live, rendered Gargoyle/HealthPoint/Potion model (hangingPotions), at the model itself; an empty gargoyle (HealthPoint inactive) has no arrow. statuesWithoutPotion counts those empty gargoyles. The arrow size is angular with no upper cap and a far target is drawn along its direction at <= maxDrawDistance, so distance never shrinks or clips it. Shader _ZTest enforcement is reported, not visual acceptance. Every configured chest and potion is marked regardless of distance (maxDistance 0 = no limit). Sources: confirmed HealthPoints child pickups, HealthPoints.current_pickup, every active PickUp, typed POI targets, PoolingSystem.active_pickups, RewardBox and the game's own minimap chest targets. The damage / enemy-close arrows are never read for markers and never hidden. Only a transform position, an enum/type name and renderer bounds are read; nothing of the game is written. The arrow is a colour, not game art, and no marker has been accepted on a headset."
    };
}
}


