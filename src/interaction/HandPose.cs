using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

// Recovered 2026-10-14 (D-178) from the shipped VampireHuntersVR.Stage0.dll (D-171) with ilspycmd: these
// HandAimDriver partial members were missing from this source tree (and from every on-disk recovery snapshot):
// near-head haptics, face-press reach logging, the stick deadzone, hand-body hold bookkeeping and the
// game-backdrop mode the host sets. Behaviour is the shipped D-171 behaviour, not new work.
internal sealed partial class HandAimDriver
{
    public string GameBackdropMode = "authored";

    public int HapticMilliseconds = 60;
    public int HapticPulsesLeft;
    public int HapticPulsesRight;
    public int HapticRejected;
    public string? HapticError;

    private bool hapticLeftNear;
    private bool hapticRightNear;
    private bool hapticFailed;
    private float hapticLeftAt = -100f;
    private float hapticRightAt = -100f;

    private int handPoseHeldBeforeParticles;
    private int beforeParticlesCalls;
    private readonly Dictionary<HandSlot, Vector3> writtenBodyPosition = new();

    // D-103: a short pulse on the controller that just entered the 0.30 m reach beside the headset (the reload
    // zone), so the hand can feel the zone without looking. Edge only, per hand, never while paused.
    private void HapticTick(bool available)
    {
        if (!HapticEnabled || hapticFailed) return;
        try
        {
            bool alive = available && Time.timeScale > 0.01f;
            float unscaledTime = Time.unscaledTime;
            HapticHand(left, 0, alive, unscaledTime, ref hapticLeftNear, ref hapticLeftAt, ref HapticPulsesLeft);
            HapticHand(right, 1, alive, unscaledTime, ref hapticRightNear, ref hapticRightAt, ref HapticPulsesRight);
        }
        catch (Exception ex)
        {
            hapticFailed = true;
            HapticError = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void HapticHand(HandSlot slot, int index, bool alive, float now, ref bool wasNear, ref float lastAt, ref int pulses)
    {
        float distance = (alive && slot.SampleActive) ? HeadDistance(slot) : float.PositiveInfinity;
        bool pulse = HeadHapticPolicy.Next(wasNear, distance, now - lastAt, out bool near);
        wasNear = near;
        if (!pulse) return;
        int result = HandAimNative.RequestHaptic(index, HeadHapticPolicy.ClampAmplitude(HapticAmplitude), HeadHapticPolicy.ClampMilliseconds(HapticMilliseconds));
        switch (result)
        {
            case 0:
                lastAt = now;
                pulses++;
                return;
            case 6:
                wasNear = false;
                return;
        }
        HapticRejected++;
        if (result == 1)
        {
            hapticFailed = true;
            HapticError = Marshal.PtrToStringAnsi(HandAimNative.ReadHapticError());
        }
    }

    // D-101: head-to-controller distance at each upper-face-button press (tuning data for the reload reach).
    private void NoteFacePress(float distance)
    {
        FacePresses++;
        if (!float.IsFinite(distance)) return;
        LastFacePressHeadDistance = distance;
        if (float.IsNaN(MinFacePressHeadDistance) || distance < MinFacePressHeadDistance) MinFacePressHeadDistance = distance;
    }

    private static Vector2 ApplyDeadzone(Vector2 v)
    {
        if (v.magnitude < 0.18f) return Vector2.zero;
        return v.normalized * Mathf.Min(1f, (v.magnitude - 0.18f) / 0.82f);
    }

    // D-143: remembers the player body position at the frame(s) a hand was driven, so the flame-emitter pin can
    // hold the hand in world space across the body's own movement before particles emit. Recovered 2026-10-14
    // (D-178) from the shipped D-171 build.
    private void NoteHandBody(PlayerController? player, int leftDrivenBefore, int rightDrivenBefore)
    {
        if (player == null) return;
        Vector3 position = player.transform.position;
        if (left.DrivenFrames > leftDrivenBefore) writtenBodyPosition[left] = position;
        if (right.DrivenFrames > rightDrivenBefore) writtenBodyPosition[right] = position;
    }

    // D-193: hold the last written hand pose (carried by the body's movement since it was written) and pin the
    // emitter immediately after PlayerController.Update, before subsequent Unity particle work. Exact game-side
    // emission ordering remains a runtime/headset verification item.
    private void HoldHandsBeforeParticles(PlayerController player)
    {
        if (emitterPin == null || ShotScopeDepth != 0) return;
        try
        {
            Vector3 position = player.transform.position;
            foreach (var slot in new[] { left, right })
            {
                if (slot.Container == null || !slot.HasWritten ||
                    !FlameEmitterPinPolicy.HandOwnsMount(slot.State == HandAimState.Driving, slot.State == HandAimState.HoldingLastPose)) continue;
                var written = slot.LastWrittenPosition;
                bool hasDelta = writtenBodyPosition.TryGetValue(slot, out var body);
                var delta = hasDelta ? position - body : Vector3.zero;
                var held = FlameEmitterPinPolicy.HoldPosition(written.x, written.y, written.z, delta.x, delta.y, delta.z, hasDelta);
                slot.Container.SetPositionAndRotation(new Vector3(held.X, held.Y, held.Z), slot.LastWrittenRotation);
                handPoseHeldBeforeParticles++;
            }
            emitterPin.Pin(FlameEmitterStage.BeforeParticles);
            beforeParticlesCalls++;
        }
        catch (Exception e)
        {
            emitterPinError ??= "before-particles: " + e.GetType().Name + ": " + e.Message;
        }
    }
}
