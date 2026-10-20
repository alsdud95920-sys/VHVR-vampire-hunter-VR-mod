// Recovered 2026-10-14 (D-178) from the shipped VampireHuntersVR.Stage0.dll (D-171) with ilspycmd: OpenXrHost.cs
// was a bad merge of two revisions (duplicated field/Update/Initialize blocks, missing braces). The decompiled
// form is the authoritative D-171 behaviour. Only the decompiler's nullable-diagnostic noise is silenced here;
// every other file in the project stays nullable-aware.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8620, CS8625, CS8632
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VampireHuntersVR.Interaction;
using VampireHuntersVR.Render;
// Restored 2026-10-14: resolve the decompiler's `(Object)` casts (and `Object.Destroy`) to UnityEngine.Object
// instead of the ambiguous System.Object.
using Object = UnityEngine.Object;

namespace VampireHuntersVR.Stage0;

public sealed class OpenXrHost : MonoBehaviour
{
	public static OpenXrHostConfig Config = new OpenXrHostConfig();

	public static ManualLogSource? Logger;

	private readonly Camera[] eyes = new Camera[2];

	private readonly RenderTexture[] targets = new RenderTexture[2];

	private int eyeSizeWidth = 2064;

	private int eyeSizeHeight = 2208;

	private Camera? source;

	private HandAimDriver? handAim;

	private bool nativeHandsEnabledEarly;

	private PlayerController? player;

	private Vector3 playerAnchor;

	private string sourceScene = "";

	private object? gameplayIdentity;

	private DateTime sessionStarted;

	private DateTime lastFrameProgress;

	private DateTime lastWaitingReceipt;

	private DateTime nextGameplayScan;

	private Vector3 anchorPosition;

	private Quaternion anchorRotation;

	private Vector3 headLocal;

	private bool hasHeadLocal;

	// D-202: the hand/aim composition frame. "Anchor" = the pre-D-202 behaviour, "Head" = the eye frame (D-195).
	// Resolved once in Initialize from Config.HandPoseFrame, which refuses any other value there.
	private bool handPoseAnchorFrame;

	// D-202: one log line per session when a snap turn arrives while the view is composed in the head frame.
	private bool handSnapIgnoredLogged;

	private IntPtr callback;

	private int phase;

	private int renderUntil;

	private int lastSubmitted = -1;

	private int prepareDispatchedFrame = -1;

	private bool done;

	private bool configured;

	private bool closing;

	private string endReason = "running";

	private bool runReset;

	private string output = "";

	private readonly List<object> evidence = new List<object>();

	private double msUpdate;

	private double msLate;

	private double msPrepare;

	private double msSave;

	private double msWorstSave;

	private double unityDeltaSum;

	private double unityDeltaMax;

	private int perfFrames;

	private int saveCalls;

	private long saveBytes;

	private object? qualityProbe;

	private object? perfProfile;

	private long monitorProbeOrigin = -1L;

	private int monitorProbePhase = -1;

	private int monitorProbeDisabledFrames;

	private int monitorProbeAppliedWindows;

	private bool monitorProbeApplied;

	private bool monitorProbeDisabled;

	private readonly DateTime started = DateTime.UtcNow;

	// D-198: startup readiness gate state. `startupReady` only exists so the one-shot STARTUP_READY log line
	// is written exactly once; `startupWaitReason` carries the human reason of the current wait (no `out`
	// parameter, so no extra interop surface on a class that is injected into IL2CPP).
	private bool startupReady;

	private string startupWaitReason = "";

	private DateTime lastStartupWaitReceipt;

	// D-198: seconds from host construction to the frame the gate first passed, so the receipt can prove how
	// long the old fixed 15 s warm-up was actually costing on this machine. 0 means the gate never had to wait.
	private double startupReadySeconds;

	// D-198: the last reason the gate was not ready, kept after the gate passes so host.json says *what* the
	// old 15 s was standing in for (e.g. "no main camera yet scene='MainMenu_2'"). Empty means never waited.
	private string startupWaitSummary = "";

	// D-200: scene-gate state. `startupSceneName`/`startupSceneSince` measure how long the active scene has been
	// the same, so the gate can tell "the menu is up" from "a scene that is still being loaded over"; `startupSkipLogged`
	// keeps the skip notice to a single log line per run.
	private string startupSceneName = "";

	private DateTime startupSceneSince = DateTime.UtcNow;

	private bool startupSkipLogged;

	private int captureId = -1;

	private NativeHostStatus captureStatus;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		IncludeFields = true
	};

	private const double OwnershipGraceSeconds = 300.0;

	private const double FrameStallSeconds = 60.0;

	private double ownershipLostSince = -1.0;

	private static double MsSince(long ticks)
	{
		return (double)(Stopwatch.GetTimestamp() - ticks) * 1000.0 / (double)Stopwatch.Frequency;
	}

	private static Matrix4x4 Matrix(float[] values)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		Matrix4x4 result = default;
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				result[i, j] = values[i * 4 + j];
			}
		}
		return result;
	}

	[HideFromIl2Cpp]
	private string OwnershipDiagnosis()
	{
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			List<string> list = new List<string>();
			if ((Object)(object)source == (Object)null)
			{
				list.Add("source-destroyed");
			}
			if ((Object)(object)player == (Object)null)
			{
				list.Add("player-destroyed");
			}
			if ((Object)(object)source != (Object)null && (Object)(object)player != (Object)null)
			{
				if ((Object)(object)player.fp_camera != (Object)(object)source)
				{
					list.Add("player.fp_camera-changed");
				}
				if ((Object)(object)Camera.main != (Object)(object)source)
				{
					list.Add("Camera.main-changed");
				}
				if (!((Behaviour)source).enabled && !monitorProbeDisabled)
				{
					list.Add("source-disabled");
				}
				if (!((Component)source).gameObject.activeInHierarchy)
				{
					list.Add("source-inactive");
				}
				if (!((Component)player).gameObject.activeInHierarchy)
				{
					list.Add("player-inactive");
				}
				if (!((Behaviour)player).enabled)
				{
					list.Add("player-component-disabled");
				}
				if ((Object)(object)((Component)source).transform.parent != (Object)(object)((Component)player).transform)
				{
					list.Add("source-reparented");
				}
				if ((Object)(object)source.targetTexture != (Object)null)
				{
					list.Add("source-has-targetTexture");
				}
				if (source.orthographic)
				{
					list.Add("source-orthographic");
				}
			}
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != sourceScene)
			{
				string text = sourceScene;
				activeScene = SceneManager.GetActiveScene();
				list.Add("scene:" + text + "->" + activeScene.name);
			}
			return (list.Count == 0) ? "none-at-diagnosis-time" : (string.Join(",", list) + " timeScale=" + Time.timeScale);
		}
		catch (Exception ex)
		{
			return "diagnosis-failed:" + ex.GetType().Name;
		}
	}

	private bool OwnershipHeld()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)source == (Object)null) && !((Object)(object)player == (Object)null))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(activeScene.name != sourceScene))
			{
				if ((Object)(object)player.fp_camera == (Object)(object)source && (Object)(object)Camera.main == (Object)(object)source && (((Behaviour)source).enabled || monitorProbeDisabled) && ((Component)source).gameObject.activeInHierarchy && ((Component)player).gameObject.activeInHierarchy && ((Behaviour)player).enabled && (Object)(object)((Component)source).transform.parent == (Object)(object)((Component)player).transform && (Object)(object)source.targetTexture == (Object)null && !source.orthographic)
				{
					ownershipLostSince = -1.0;
					return true;
				}
				if (ownershipLostSince < 0.0)
				{
					ownershipLostSince = Time.unscaledTime;
					ManualLogSource? logger = Logger;
					if (logger != null)
					{
						logger.LogInfo((object)("[OPENXR_HOST] OWNERSHIP_SOFT_LOSS grace=" + 300.0 + "s failed=" + OwnershipDiagnosis()));
					}
				}
				return (double)Time.unscaledTime - ownershipLostSince <= 300.0;
			}
		}
		return false;
	}

	private void Update()
	{
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_0718: Unknown result type (might be due to invalid IL or missing references)
		//IL_071d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0724: Unknown result type (might be due to invalid IL or missing references)
		//IL_0729: Unknown result type (might be due to invalid IL or missing references)
		//IL_073f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0749: Unknown result type (might be due to invalid IL or missing references)
		//IL_0753: Unknown result type (might be due to invalid IL or missing references)
		//IL_0763: Unknown result type (might be due to invalid IL or missing references)
		//IL_076d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0777: Unknown result type (might be due to invalid IL or missing references)
		//IL_0787: Unknown result type (might be due to invalid IL or missing references)
		//IL_0791: Unknown result type (might be due to invalid IL or missing references)
		//IL_079b: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a5: Unknown result type (might be due to invalid IL or missing references)
		if (done)
		{
			if (closing && HostNative.CanReleaseSources() != 0)
			{
				Cleanup();
				closing = false;
			}
		}
		else
		{
			// D-198: the fixed 15 s warm-up is gone. The host used to sit on a flat elapsed-time threshold before it
			// ever called Initialize, so a player who had the main menu up after three seconds still waited
			// until fifteen for VR. What the host needs is not elapsed time but readiness: the only transient
			// precondition Initialize has is a main camera ("no main camera" while the menu scene has none),
			// so that single check now runs every frame and the wait is bounded by Config.GameplayWaitSeconds
			// instead of being open-ended. A permanent problem (non-D3D11 device, invalid headset config) is
			// deliberately NOT waited on here: the gate passes it straight through so Initialize still raises
			// its own precise error instead of being reported as a startup timeout.
			if (!configured && !StartupReady())
			{
				if (!ReportStartupWait(startupWaitReason))
				{
					// D-198: the gate timed out before Initialize ever ran, so `output` is still empty and
					// Fail() would write no receipt at all. Point it at the same host.json path Initialize
					// uses so the timeout is provable on disk (waiting.json keeps the per-second reason).
					EnsureOutputPath();
					Fail(new TimeoutException("startup wait expired after " + (DateTime.UtcNow - started).TotalSeconds.ToString("F1") + "s: " + startupWaitReason));
				}
				return;
			}
			if (!configured && !startupReady)
			{
				startupReady = true;
				startupReadySeconds = (DateTime.UtcNow - started).TotalSeconds;
				ManualLogSource? startupLogger = Logger;
				if (startupLogger != null)
				{
					startupLogger.LogInfo((object)("[OPENXR_HOST] STARTUP_READY elapsed=" + startupReadySeconds.ToString("F1") + "s scene='" + SceneManager.GetActiveScene().name + "' camera=" + SafeName((Object?)(object)Camera.main, "none") + " waited='" + startupWaitSummary + "' (D-198/D-200: readiness + scene gate, no fixed warm-up)"));
				}
			}
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				if (!configured)
				{
					Initialize();
					return;
				}
				// D-178: menu-first. While the host is up in the main menu there is no player yet; the moment a
				// stage starts, acquire the gameplay camera and install hand/weapon integration exactly once.
				if (Config.MenuFirst && player == null)
				{
					// D-181: a destroyed player (the stage scene unloaded back to a menu) means the stage ended.
					// This check MUST run before anything below tries to work with the dead gameplay camera:
					// in the frozen-menu-return run the early return below swallowed the whole hand-back path
					// (the "returned to menu" log never appeared), which is exactly the freeze the user saw.
					if (handAim != null && ReturnedToMenu())
					{
						LeaveGameplayForMenu();
						return;
					}
					// The menu camera is destroyed the instant a stage loads, and the render schedule below calls
					// PrepareCameras every cycle - which throws "camera lost" on a dead source and fails the whole
					// host (observed 2026-10-15). Force an immediate scan, and while the gameplay camera is not up
					// yet, skip the schedule for this frame instead of scheduling against a dead camera.
					TryEnterGameplay((Object)(object)source == (Object)null);
					// D-200: adopt BEFORE the null-source return. D-181 had this call after that return, so the
					// moment the held camera was destroyed - which is exactly what the game does when it loads
					// the next scene - the early return skipped the adoption for ever and the host submitted
					// from a dead source (the 0.9.3 black screen). Right after a successful TryEnterGameplay this
					// is a no-op: the gameplay source IS Camera.main, so the reference comparison below exits.
					// D-181: back in a menu scene (returned from a stage) the camera we hold may be a leftover from the
					// scene that was just unloaded. Re-adopt the scene's current main camera once it exists.
					AdoptMenuCameraIfNeeded();
					if ((Object)(object)source == (Object)null)
					{
						return;
					}
				}

				perfFrames++;
				double num = (double)Time.unscaledDeltaTime * 1000.0;
				unityDeltaSum += num;
				if (num > unityDeltaMax)
				{
					unityDeltaMax = num;
				}
				if (Config.Headset)
				{
					if (Config.HeadsetSeconds > 0 && DateTime.UtcNow - sessionStarted > TimeSpan.FromSeconds(Config.HeadsetSeconds + 30))
					{
						throw new TimeoutException("host wall-time watchdog; close requested, external harness remains backstop");
					}
					if (DateTime.UtcNow - lastFrameProgress > TimeSpan.FromSeconds(60.0))
					{
						throw new TimeoutException("host render stall watchdog: no submitted frame for " + 60.0 + "s; close requested, external harness remains backstop");
					}
				}
				else if (DateTime.UtcNow - sessionStarted > TimeSpan.FromSeconds(90.0))
				{
					throw new TimeoutException("host wall-time watchdog; close requested, external harness remains backstop");
				}
				// D-181: menu-first return trip. A stage that ends and drops back to the main menu used to be treated as
				// "gameplay ownership lost": the host either kept rendering the dead gameplay camera (black screen) or
				// closed the VR session. In menu-first mode the host must instead hand back to the menu.
				if (Config.MenuFirst && handAim != null && ReturnedToMenu())
				{
					LeaveGameplayForMenu();
					return;
				}
				if (Config.GameplayOnly && !OwnershipHeld())
				{
					if (!((Object)(object)source == (Object)null) && !((Object)(object)player == (Object)null) && !((Object)(object)player.fp_camera != (Object)(object)source) && !((Object)(object)Camera.main != (Object)(object)source) && ((Behaviour)source).enabled && ((Component)source).gameObject.activeInHierarchy && ((Component)player).gameObject.activeInHierarchy && ((Behaviour)player).enabled && !((Object)(object)((Component)source).transform.parent != (Object)(object)((Component)player).transform) && !((Object)(object)source.targetTexture != (Object)null) && !source.orthographic)
					{
						Scene activeScene = SceneManager.GetActiveScene();
						if (!(activeScene.name != sourceScene))
						{
							goto IL_0290;
						}
					}
					if (StageEnded())
					{
						HostNative.ReadStatus(out var status);
						EndGameplay(status);
						return;
					}
					throw new InvalidOperationException("gameplay camera/scene ownership lost; no menu fallback; failed=" + OwnershipDiagnosis());
				}
				goto IL_0290;
				IL_0290:
				HostNative.ReadStatus(out var status2);
				if (status2.State == 6)
				{
					return;
				}
				if (status2.State == -1)
				{
					throw new InvalidOperationException(Marshal.PtrToStringAnsi(HostNative.ReadError()));
				}
				if (Config.MonitorCostProbe && status2.State == 1)
				{
					TickMonitorCostProbe(status2);
				}
				if (Config.Headset && File.Exists(Path.Combine(Config.Output, "stop.request")))
				{
					endReason = "stop-request";
					done = true;
					closing = true;
					try
					{
						handAim?.Release();
					}
					catch (Exception)
					{
					}
					handAim = null;
					Camera[] array = eyes;
					foreach (Camera val in array)
					{
						if ((Object)(object)val != (Object)null)
						{
							((Behaviour)val).enabled = false;
						}
					}
					GL.IssuePluginEvent(callback, 3);
					Save(status2, "HEADSET_STOP_REQUESTED_NOT_ACCEPTED");
					ManualLogSource? logger = Logger;
					if (logger != null)
					{
						logger.LogInfo((object)"[OPENXR_HOST] STOP_REQUESTED by stop.request");
					}
					return;
				}
				if (status2.State == 3)
				{
					endReason = "session-ended";
					done = true;
					Save(status2, Config.Headset ? "HEADSET_SESSION_ENDED_NOT_ACCEPTED" : "TRANSFER_COMPLETE_NOT_M4B_ACCEPTED");
					Cleanup();
					ManualLogSource? logger2 = Logger;
					if (logger2 != null)
					{
						logger2.LogInfo((object)("[OPENXR_HOST] DONE frames=" + status2.Frames + " hold=" + status2.HoldSeconds + " ended=" + status2.Ended));
					}
					return;
				}
				if (status2.Frames != lastSubmitted)
				{
					if (captureId >= 0 && status2.Frames > captureStatus.Frames)
					{
						File.WriteAllText(output + ".capture" + captureId + ".json", JsonSerializer.Serialize(new
						{
							captureId = captureId,
							submittedFrames = status2.Frames,
							capturedStatus = captureStatus,
							updatedUtc = DateTime.UtcNow.ToString("o"),
							evidence = evidence
						}, JsonOptions));
						captureId = -1;
					}
					lastSubmitted = status2.Frames;
					lastFrameProgress = DateTime.UtcNow;
					if (!Config.Headset || status2.Frames % 45 == 0)
					{
						Save(status2, "RUNNING_NOT_ACCEPTED");
						if (status2.Frames % 45 == 0)
						{
							LogPerf(status2);
						}
					}
				}
				if (phase == 3 && status2.State == 2)
				{
					prepareDispatchedFrame = Time.frameCount;
					phase = 1;
				}
				else if (phase == 3 && status2.State == 4)
				{
					phase = 0;
				}
				if (phase == 0 && (status2.State == 1 || status2.State == 4))
				{
					switch (HostNative.RequestPrepare())
					{
					case 6:
						return;
					default:
						throw new InvalidOperationException("prepare dispatch rejected");
					case 0:
						break;
					}
					GL.IssuePluginEvent(callback, 1);
					phase = 1;
				}
				else if (phase == 1 && status2.State == 4)
				{
					phase = 0;
				}
				else if (phase == 1 && status2.State == 2)
				{
					long timestamp2 = Stopwatch.GetTimestamp();
					PrepareCameras(status2);
					msPrepare += MsSince(timestamp2);
					renderUntil = Time.frameCount + Math.Clamp(Config.RenderFrames, 1, 3);
					phase = 2;
				}
				else if (phase == 2 && Time.frameCount >= renderUntil)
				{
					Camera[] array = eyes;
					for (int i = 0; i < array.Length; i++)
					{
						((Behaviour)array[i]).enabled = false;
					}
					string path = Path.Combine(Config.Output, "capture.request");
					if (File.Exists(path))
					{
						if (!int.TryParse(File.ReadAllText(path), out var result))
						{
							throw new InvalidOperationException("capture request invalid");
						}
						switch (HostNative.RequestCapture(result))
						{
						case 6:
							return;
						default:
							throw new InvalidOperationException("capture request rejected");
						case 0:
							break;
						}
						captureId = result;
						captureStatus = status2;
						File.Delete(path);
						for (int j = 0; j < 2; j++)
						{
							Camera obj = eyes[j];
							NativeEyePose pose = ((j == 0) ? status2.Left : status2.Right);
							Vector3 position = ((Component)obj).transform.position;
							Quaternion rotation = ((Component)obj).transform.rotation;
							evidence.Add(new
							{
								eye = j,
								captureId = result,
								pose = pose,
								position = new float[3] { position.x, position.y, position.z },
								view = StereoMath.UnityView(new float[3] { position.x, position.y, position.z }, new float[4] { rotation.x, rotation.y, rotation.z, rotation.w }),
								projection = StereoMath.Projection(pose.Left, pose.Right, pose.Up, pose.Down, source.nearClipPlane, source.farClipPlane),
								frame = Time.frameCount
							});
						}
					}
					GL.IssuePluginEvent(callback, 2);
					phase = 3;
				}
				else if (phase == 3 && status2.State == 1)
				{
					phase = 0;
				}
				if (phase == 0 && Config.RenderFrames < 3 && (status2.State == 1 || status2.State == 4) && prepareDispatchedFrame != Time.frameCount && HostNative.RequestPrepare() == 0)
				{
					GL.IssuePluginEvent(callback, 1);
					phase = 1;
					prepareDispatchedFrame = Time.frameCount;
				}
			}
			catch (Exception e)
			{
				Fail(e);
			}
			finally
			{
				msUpdate += MsSince(timestamp);
			}
		}
	}

	private void LateUpdate()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (handAim == null || done || closing || !configured || (Object)(object)source == (Object)null || (Object)(object)player == (Object)null)
		{
			return;
		}
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			// D-202 (BUG-202): the hand pose must be composed in the frame the EYE cameras use. D-195 composes the eyes
			// with identity (val2 = anchorPosition + eyePos, val3 = eyeRot) and left the hands multiplying the game
			// camera rotation in. That camera rotation is the player's facing on this game (+-90 deg), so the weapon,
			// the reticle and the fire direction all came out 90 deg from where the player points. HandPoseFrame=Anchor
			// restores the pre-D-202 behaviour for a single A/B sitting without a rebuild.
			Quaternion handPoseBase = (handPoseAnchorFrame ? anchorRotation : Quaternion.identity);
			// D-136/D-138, restored (BUG-205): hand the managed phase machine's own numbers to the read-only probe.
			handAim.SetRenderTimingPhase(phase, lastSubmitted, renderUntil);
			handAim.Tick(source, anchorPosition + (((Component)player).transform.position - playerAnchor), handPoseBase, sourceScene, player, Config.HandFire);
			ApplySnap(handAim.TakeSnap());
		}
		catch (Exception ex)
		{
			ManualLogSource? logger = Logger;
			if (logger != null)
			{
				logger.LogError((object)("[OPENXR_HOST] HAND_AIM disabled: " + ex));
			}
			handAim.Release();
			handAim = null;
		}
		finally
		{
			msLate += MsSince(timestamp);
		}
	}

	private void ApplySnap(float degrees)
	{
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		// D-202 (BUG-202): in the head frame a snap turn cannot be done by yawing the anchor. The eye cameras stopped
		// using anchorRotation in D-195, so the legacy path only translated the view (a sideways jolt with no turn).
		// The head-frame snap (a view-yaw offset in the eye composition, plus the eye offsets rotated about the head)
		// is not implemented yet, so the request is refused loudly and the anchor is left untouched rather than moving
		// the picture without turning it. HandPoseFrame=Anchor still exercises the legacy path for comparison.
		if (degrees != 0f && !handPoseAnchorFrame)
		{
			if (!handSnapIgnoredLogged)
			{
				handSnapIgnoredLogged = true;
				ManualLogSource? snapLogger = Logger;
				if (snapLogger != null)
				{
					snapLogger.LogWarning((object)"[OPENXR_HOST] SNAP ignored on the head frame: the view-yaw offset is not implemented yet (D-202 / BUG-202). The anchor was left alone so the picture cannot jolt; the request is still counted in the receipt.");
				}
			}
			return;
		}
		if (degrees != 0f && hasHeadLocal)
		{
			HandAimMath.SnapAnchor(new float[3] { anchorPosition.x, anchorPosition.y, anchorPosition.z }, new float[4] { anchorRotation.x, anchorRotation.y, anchorRotation.z, anchorRotation.w }, new float[3] { headLocal.x, headLocal.y, headLocal.z }, degrees, out float[] newPosition, out float[] newRotation);
			anchorPosition = new Vector3(newPosition[0], newPosition[1], newPosition[2]);
			anchorRotation = new Quaternion(newRotation[0], newRotation[1], newRotation[2], newRotation[3]);
		}
	}

	private static bool IsWeaponTwistValid(float degrees)
	{
		if (float.IsFinite(degrees))
		{
			return MathF.Abs(degrees) <= 180f;
		}
		return false;
	}

	// D-198: readiness gate that replaces the old flat 15 s elapsed-time warm-up.
	//
	// The old gate measured elapsed time, not readiness: Time.unscaledTime is documented as "the time in seconds
	// since the start of the game" (not since the scene loaded), so every run waited a flat 15 s before the host
	// even looked at the game - on a fast machine the main menu was already up and VR still had not started.
	//
	// Initialize() is the only definition of "ready" that matters, because it is what throws when the game is
	// not ready yet: on the menu-first / render-only path it takes the `source = Camera.main` branch and throws
	// "no main camera" while the menu scene has no camera, and it refuses a non-D3D11 graphics device. Only the
	// missing-camera case is transient and worth waiting for; a bad device or a bad config must still surface as
	// its own precise error instead of being reported as a startup timeout.
	[HideFromIl2Cpp]
	private bool StartupReady()
	{
		// The known init-exception config (headset without GameplayOnly/MenuFirst) must reach Initialize
		// untouched so it throws its own ArgumentException (D-189 / BUGS TOOL-091) rather than a wait timeout.
		if (Config.Headset && !Config.GameplayOnly && !Config.MenuFirst)
		{
			startupWaitReason = "";
			return true;
		}
		// Gameplay-only: Initialize owns this wait (it rescans every second and writes WAITING_FOR_GAMEPLAY_CAMERA
		// with the same GameplayWaitSeconds budget), so the gate must not duplicate it.
		if (Config.GameplayOnly && !Config.MenuFirst)
		{
			startupWaitReason = "";
			return true;
		}
		// D-200: the intro/splash scene must never supply the eye source. In 0.9.3 the gate passed at 0.6 s in
		// scene 'Intro' (its Main Camera satisfied the readiness check), the game then loaded the menu and
		// destroyed that camera, and the host went on submitting from a dead source: black headset, window
		// frames frozen, XR loop still advancing. The wait below stays inside the same bounded
		// GameplayWaitSeconds budget ReportStartupWait enforces, so a game that never leaves the intro still
		// fails loudly with a stated reason instead of waiting for ever.
		string scene = SceneManager.GetActiveScene().name;
		if (StartupSceneSkipped(scene))
		{
			if (!startupSkipLogged)
			{
				startupSkipLogged = true;
				ManualLogSource? skipLogger = Logger;
				if (skipLogger != null)
				{
					skipLogger.LogInfo((object)("[OPENXR_HOST] STARTUP_SKIP scene='" + scene + "' StartupSkipScenes=" + Config.StartupSkipScenes + " (D-200: VR starts once the game leaves the intro)"));
				}
			}
			startupWaitReason = "intro scene '" + scene + "' (D-200: waiting for the main menu)";
			return false;
		}
		if (!StartupSceneSettled(scene))
		{
			startupWaitReason = "scene '" + scene + "' not settled yet (D-200 settle gate)";
			return false;
		}
		Camera main = Camera.main;
		if ((Object)(object)main == (Object)null)
		{
			startupWaitReason = "no main camera yet scene='" + SceneManager.GetActiveScene().name + "'";
			return false;
		}
		startupWaitReason = "";
		return true;
	}

	// D-200: StartupSkipScenes is a comma-separated, case-insensitive scene-name list. A missing/blank value
	// skips nothing, so the key can only ever shorten this wait, never wedge startup.
	[HideFromIl2Cpp]
	private bool StartupSceneSkipped(string scene)
	{
		if (string.IsNullOrWhiteSpace(Config.StartupSkipScenes) || string.IsNullOrWhiteSpace(scene))
		{
			return false;
		}
		string[] tokens = Config.StartupSkipScenes.Split(',');
		for (int i = 0; i < tokens.Length; i++)
		{
			string name = tokens[i].Trim();
			if (name.Length != 0 && scene.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	// D-200: the active scene must have been stable for Config.StartupSettleSeconds before its camera may be
	// adopted, which covers transient scenes beyond the ones named in StartupSkipScenes. The value is clamped
	// here (never trusted straight from the file) so a bad setting can only shorten the wait; Initialize still
	// refuses an out-of-range value with its own error instead of silently accepting it.
	[HideFromIl2Cpp]
	private bool StartupSceneSettled(string scene)
	{
		string name = scene ?? "";
		if (!name.Equals(startupSceneName, StringComparison.Ordinal))
		{
			startupSceneName = name;
			startupSceneSince = DateTime.UtcNow;
			return false;
		}
		double settle = Config.StartupSettleSeconds;
		if (double.IsNaN(settle) || settle < 0.0)
		{
			settle = 0.0;
		}
		else if (settle > 10.0)
		{
			settle = 10.0;
		}
		return (DateTime.UtcNow - startupSceneSince).TotalSeconds >= settle;
	}

	// D-198: bounded, evidenced wait. Returns false only when the budget is spent, so the caller turns the wait
	// into an ordinary host failure with the reason attached instead of silently returning for ever.
	[HideFromIl2Cpp]
	private bool ReportStartupWait(string reason)
	{
		if (DateTime.UtcNow - started > TimeSpan.FromSeconds(Config.GameplayWaitSeconds))
		{
			return false;
		}
		if (DateTime.UtcNow - lastStartupWaitReceipt <= TimeSpan.FromSeconds(1.0))
		{
			return true;
		}
		lastStartupWaitReceipt = DateTime.UtcNow;
		if (!string.IsNullOrEmpty(reason))
		{
			startupWaitSummary = reason;
		}
		double elapsed = (DateTime.UtcNow - started).TotalSeconds;
		try
		{
			if (!string.IsNullOrEmpty(Config.Output) && Path.IsPathRooted(Config.Output))
			{
				Directory.CreateDirectory(Config.Output);
				File.WriteAllText(Path.Combine(Config.Output, "waiting.json"), JsonSerializer.Serialize(new
				{
					verdict = "WAITING_FOR_STARTUP_CAMERA",
					reason = reason,
					processId = Environment.ProcessId,
					scene = SceneManager.GetActiveScene().name,
					startedUtc = started.ToString("o"),
					updatedUtc = lastStartupWaitReceipt.ToString("o"),
					elapsedSeconds = elapsed,
					budgetSeconds = Config.GameplayWaitSeconds
				}, JsonOptions));
			}
		}
		catch (Exception ex)
		{
			ManualLogSource? receiptLogger = Logger;
			if (receiptLogger != null)
			{
				receiptLogger.LogWarning((object)("[OPENXR_HOST] STARTUP_WAIT receipt failed: " + ex.Message));
			}
		}
		ManualLogSource? logger = Logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[OPENXR_HOST] STARTUP_WAIT " + reason + " elapsed=" + elapsed.ToString("F1") + "s budget=" + Config.GameplayWaitSeconds + "s (D-198)"));
		}
		return true;
	}

	// D-198: Initialize() is what normally sets `output`; if the startup gate times out first, the failure
	// receipt would silently never be written. This mirrors Initialize's own output setup (absolute rooted
	// path, directory created) without any of its other side effects.
	[HideFromIl2Cpp]
	private bool EnsureOutputPath()
	{
		if (string.IsNullOrEmpty(Config.Output) || !Path.IsPathRooted(Config.Output))
		{
			return false;
		}
		Directory.CreateDirectory(Config.Output);
		output = Path.Combine(Config.Output, "host.json");
		return true;
	}

	private void Initialize()
	{
		//IL_0758: Unknown result type (might be due to invalid IL or missing references)
		//IL_075d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0790: Unknown result type (might be due to invalid IL or missing references)
		//IL_0795: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_085e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0865: Expected Obj, but got Unknown
		//IL_08b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_08cb: Expected Obj, but got Unknown
		//IL_0538: Unknown result type (might be due to invalid IL or missing references)
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ec: Invalid comparison between Unknown and I4
		//IL_094d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0952: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a86: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aeb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0af0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0604: Unknown result type (might be due to invalid IL or missing references)
		//IL_0618: Unknown result type (might be due to invalid IL or missing references)
		//IL_062c: Unknown result type (might be due to invalid IL or missing references)
		if (!Path.IsPathRooted(Config.Output) || (!string.IsNullOrEmpty(Config.Runtime) && !File.Exists(Config.Runtime)) || Config.Frames < 120 || Config.Frames > 1000)
		{
			throw new ArgumentException("invalid opt-in host config");
		}
		if (Config.Headset && ((!Config.GameplayOnly && !Config.MenuFirst) || !Config.HandAim || Config.HeadsetSeconds < 0 || (Config.HeadsetSeconds > 0 && (Config.HeadsetSeconds < 60 || Config.HeadsetSeconds > 900))))
		{
			throw new ArgumentException("invalid headset config: needs GameplayOnly (or MenuFirst), HandAim and HeadsetSeconds 0 (unlimited) or 60-900");
		}
		// D-178: menu-first starts with no gameplay camera yet, so the gameplay-only ownership path must stay off
		// (it would otherwise end the host as soon as it saw player == null). TryEnterGameplay turns it back on
		// the moment a stage actually starts.
		if (Config.MenuFirst)
		{
			Config.GameplayOnly = false;
		}
		if (!RenderResolutionPolicy.IsValid(Config.EyePreset))
		{
			throw new ArgumentException("invalid eye preset: low|normal|high|custom");
		}
		if (!RenderResolutionPolicy.IsValidScale(Config.EyeScale))
		{
			throw new ArgumentException("invalid eye scale: 0.5..2.0");
		}
		if (!PerfProfilePolicy.IsValid(Config.PerfProfile))
		{
			throw new ArgumentException("invalid perf profile: off|light|strong");
		}
		if (!GameBackdropPolicy.IsValid(Config.GameBackdropMode))
		{
			throw new ArgumentException("invalid game backdrop mode: authored|dim|hide");
		}
		if (!WorldMarkerPolicy.IsValidMode(Config.WorldMarkers))
		{
			throw new ArgumentException("invalid world markers mode: off|on");
		}
		if (!WorldMarkerPolicy.TryParseKinds(Config.MarkerKinds, out string[] kinds))
		{
			throw new ArgumentException("invalid marker kinds: exact Pickup_Type names plus CHEST, comma separated");
		}
		if (!float.IsFinite(Config.MarkerMaxDistance) || (Config.MarkerMaxDistance != 0f && (Config.MarkerMaxDistance < 5f || Config.MarkerMaxDistance > 200f)))
		{
			throw new ArgumentException("invalid marker max distance: 0 (no limit) or 5..200 metres");
		}
		if (Config.MarkerMaxCount < 1 || Config.MarkerMaxCount > 64)
		{
			throw new ArgumentException("invalid marker max count: 1..64");
		}
		if (!IsWeaponTwistValid(Config.LeftWeaponTwistDeg) || !IsWeaponTwistValid(Config.RightWeaponTwistDeg))
		{
			throw new ArgumentException("invalid weapon twist degrees: finite and within -180..180");
		}
		ManualLogSource? logger = Logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[OPENXR_HOST] RUNTIME " + (string.IsNullOrEmpty(Config.Runtime) ? "auto: machine ActiveRuntime / loader default" : ("config: " + Config.Runtime))));
		}
		if (Marshal.SizeOf<NativeEyePose>() != 44 || Marshal.SizeOf<NativeHostStatus>() != 136)
		{
			throw new InvalidOperationException("native status ABI mismatch");
		}
		if (Config.GameplayWaitSeconds < 30 || Config.GameplayWaitSeconds > (Config.Headset ? 600 : 180))
		{
			throw new ArgumentException("invalid gameplay wait budget");
		}
		// D-200: the startup scene gate clamps these values so it can never hang, but a file outside the
		// documented range is still refused here rather than silently accepted.
		if (double.IsNaN(Config.StartupSettleSeconds) || Config.StartupSettleSeconds < 0.0 || Config.StartupSettleSeconds > 10.0)
		{
			throw new ArgumentException("invalid startup settle seconds: 0..10");
		}
		if (Config.StartupSkipScenes != null && Config.StartupSkipScenes.Length > 256)
		{
			throw new ArgumentException("invalid startup skip scene list: at most 256 characters");
		}
		// D-202: exactly two composition frames exist, and an unknown value is refused here rather than defaulted -
		// a silent fallback is what produced a hand 90 deg off the view (BUG-202).
		handPoseAnchorFrame = Config.HandPoseFrame != null && Config.HandPoseFrame.Equals("Anchor", StringComparison.OrdinalIgnoreCase);
		bool handPoseHeadFrame = Config.HandPoseFrame != null && Config.HandPoseFrame.Equals("Head", StringComparison.OrdinalIgnoreCase);
		if (!handPoseAnchorFrame && !handPoseHeadFrame)
		{
			throw new ArgumentException("invalid hand pose frame: Head|Anchor");
		}
		ManualLogSource? handFrameLogger = Logger;
		if (handFrameLogger != null)
		{
			handFrameLogger.LogInfo((object)("[OPENXR_HOST] HAND_FRAME " + (handPoseAnchorFrame ? "Anchor (pre-D-202 A/B: the game camera rotation is multiplied into the hand/aim pose)" : "Head (D-202: hand/aim composed in the eye frame, identity - the same frame D-195 gave the eyes)") + "; the reticle, the muzzle alignment, the fire path and the snap anchor all follow this one value"));
		}
		// D-206 (BUG-204): the turret build-mode grip cycle has three candidate game surfaces plus an off switch, and the
		// sitting A/Bs them, so an unknown value is refused here instead of defaulting to a surface nobody chose.
		string turretCycle = Config.TurretCycle ?? "";
		bool turretCycleKnown = turretCycle.Equals("Stick", StringComparison.OrdinalIgnoreCase) || turretCycle.Equals("Dpad", StringComparison.OrdinalIgnoreCase) ||
			turretCycle.Equals("Special", StringComparison.OrdinalIgnoreCase) || turretCycle.Equals("Off", StringComparison.OrdinalIgnoreCase);
		if (!turretCycleKnown)
		{
			throw new ArgumentException("invalid turret cycle source: Stick|Dpad|Special|Off");
		}
		ManualLogSource? turretGripLogger = Logger;
		if (turretGripLogger != null)
		{
			turretGripLogger.LogInfo((object)("[OPENXR_HOST] TURRET_GRIPS cycle=" + turretCycle + " (D-206/BUG-204: in the game's own turret build mode the left grip steps the blueprint back, the right grip steps it forward, both grips hold the game's Fire action = build; grips stay unbound everywhere else, D-057)"));
		}
		Directory.CreateDirectory(Config.Output);
		output = Path.Combine(Config.Output, "host.json");
		ResetRunFolder();
		endReason = (Config.GameplayOnly ? "waiting-for-gameplay" : "running");
		if (Config.GameplayOnly && !Config.MenuFirst)
		{
			if (DateTime.UtcNow < nextGameplayScan)
			{
				return;
			}
			nextGameplayScan = DateTime.UtcNow.AddSeconds(1.0);
			Il2CppArrayBase<PlayerController> val = Resources.FindObjectsOfTypeAll<PlayerController>();
			List<GameplayCameraCandidate> list = new List<GameplayCameraCandidate>();
			List<PlayerController> list2 = new List<PlayerController>();
			Scene activeScene = SceneManager.GetActiveScene();
			string name = activeScene.name;
			Camera main = Camera.main;
			foreach (PlayerController item in val)
			{
				if (!((Object)(object)item == (Object)null) && !((Object)(object)item.fp_camera == (Object)null))
				{
					Camera fp_camera = item.fp_camera;
					list.Add(new GameplayCameraCandidate(name, SafeName((Object?)(object)fp_camera), (Object)(object)fp_camera == (Object)(object)main, ((Component)fp_camera).gameObject.activeInHierarchy, ((Behaviour)fp_camera).enabled, !fp_camera.orthographic, (Object)(object)fp_camera.targetTexture != (Object)null, ((Component)item).gameObject.activeInHierarchy, ((Behaviour)item).enabled, (Object)(object)((Component)fp_camera).transform.parent == (Object)(object)((Component)item).transform));
					list2.Add(item);
				}
			}
			int num = GameplayCameraPolicy.Select(list);
			if (num < 0)
			{
				if (DateTime.UtcNow - started > TimeSpan.FromSeconds(Config.GameplayWaitSeconds))
				{
					throw new TimeoutException("gameplay camera wait expired; no menu fallback");
				}
				if (DateTime.UtcNow - lastWaitingReceipt > TimeSpan.FromSeconds(1.0))
				{
					lastWaitingReceipt = DateTime.UtcNow;
					File.WriteAllText(Path.Combine(Config.Output, "waiting.json"), JsonSerializer.Serialize(new
					{
						verdict = "WAITING_FOR_GAMEPLAY_CAMERA",
						processId = Environment.ProcessId,
						scene = name,
						startedUtc = started.ToString("o"),
						updatedUtc = lastWaitingReceipt.ToString("o"),
						candidates = list
					}, JsonOptions));
				}
				return;
			}
			player = list2[num];
			source = player.fp_camera;
			playerAnchor = ((Component)player).transform.position;
			sourceScene = name;
			Il2CppArrayBase<Weapon> val2 = Resources.FindObjectsOfTypeAll<Weapon>();
			List<object> list3 = new List<object>();
			int num2 = 0;
			foreach (Weapon item2 in val2)
			{
				if (!((Object)(object)item2 == (Object)null) && ((Component)item2).gameObject.activeInHierarchy)
				{
					num2++;
					if (list3.Count < 128)
					{
						Transform muzzle_flash_spawn = item2.muzzle_flash_spawn;
						list3.Add(new
						{
							name = SafeName((Object?)(object)((Component)item2).gameObject),
							slot = ((object)item2.current_weapon_slot/*cast due to constrained. prefix*/).ToString(),
							path = ObjectPath(((Component)item2).transform),
							muzzlePath = (((Object)(object)muzzle_flash_spawn == (Object)null) ? null : ObjectPath(muzzle_flash_spawn)),
							weaponPosition = new float[3]
							{
								((Component)item2).transform.position.x,
								((Component)item2).transform.position.y,
								((Component)item2).transform.position.z
							},
							note = "active-object read-only inventory; slot ownership/shot frame not gameplay-aim verified"
						});
					}
				}
			}
			gameplayIdentity = new
			{
				policy = "active-player-fp-camera-direct-child-v1",
				scene = name,
				sourceCamera = SafeName((Object?)(object)source, "none"),
				capturedUtc = DateTime.UtcNow.ToString("o"),
				processId = Environment.ProcessId,
				timeScale = Time.timeScale,
				cameraPath = ObjectPath(((Component)source).transform),
				playerPath = ObjectPath(((Component)player).transform),
				playerPosition = new float[3] { playerAnchor.x, playerAnchor.y, playerAnchor.z },
				activeWeaponObjects = num2,
				inventoryTruncated = (num2 > list3.Count),
				weapons = list3
			};
			File.WriteAllText(Path.Combine(Config.Output, "gameplay-camera.json"), JsonSerializer.Serialize(gameplayIdentity, JsonOptions));
			endReason = "running";
		}
		else
		{
			source = Camera.main;
			if ((Object)(object)source == (Object)null)
			{
				throw new InvalidOperationException("no main camera");
			}
		}
		if (!((object)SystemInfo.graphicsDeviceType/*cast due to constrained. prefix*/).ToString().Equals("Direct3D11", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("requires game D3D11");
		}
		anchorPosition = ((Component)source).transform.position;
		anchorRotation = ((Component)source).transform.rotation;
		// D-187: the host no longer touches QualitySettings. The game's own saved choice is used as-is, so the
		// GRAPHICS_QUALITY log lines and the ApplyGraphicsQuality() helper are gone too.
		(int, int) tuple = RenderResolutionPolicy.EyeSize(Config.EyeWidth, Config.EyeHeight, Config.EyePreset, Config.EyeScale);
		eyeSizeWidth = tuple.Item1;
		eyeSizeHeight = tuple.Item2;
		ManualLogSource? logger2 = Logger;
		if (logger2 != null)
		{
			logger2.LogInfo((object)("[OPENXR_HOST] RESOLUTION " + RenderResolutionPolicy.Describe(Config.EyeWidth, Config.EyeHeight, Config.EyePreset, Config.EyeScale) + " (D-171)"));
		}
		for (int i = 0; i < 2; i++)
		{
			GameObject val3 = new GameObject("VHVR_RuntimeEye_" + i);
			Object.DontDestroyOnLoad((Object)(object)val3);
			eyes[i] = val3.AddComponent<Camera>();
			eyes[i].CopyFrom(source);
			((Behaviour)eyes[i]).enabled = false;
			targets[i] = new RenderTexture(eyeSizeWidth, eyeSizeHeight, 24, (RenderTextureFormat)0, (RenderTextureReadWrite)2)
			{
				antiAliasing = Config.EyeMsaa is 1 or 2 or 4 or 8 ? Config.EyeMsaa : 4,
				useMipMap = false
			};
			if (!targets[i].Create())
			{
				throw new InvalidOperationException("camera target creation failed");
			}
			if ((int)QualitySettings.activeColorSpace == 1 && !targets[i].sRGB)
			{
				throw new InvalidOperationException("Linear project must encode sRGB camera target");
			}
			eyes[i].targetTexture = targets[i];
		}
		ManualLogSource? logger3 = Logger;
		if (logger3 != null)
		{
			logger3.LogInfo((object)("[OPENXR_HOST] QUALITY aniso=" + ((object)QualitySettings.anisotropicFiltering/*cast due to constrained. prefix*/).ToString() + " gameAntiAliasing=" + QualitySettings.antiAliasing + " lodBias=" + QualitySettings.lodBias + " vSync=" + QualitySettings.vSyncCount + " eye=" + ((Texture)targets[0]).width + "x" + ((Texture)targets[0]).height + " eyeAntiAliasing=" + targets[0].antiAliasing));
		}
		if (Config.Anisotropic)
		{
			QualitySettings.anisotropicFiltering = (AnisotropicFiltering)2;
			Texture.SetGlobalAnisotropicFilteringLimits(16, 16);
			ManualLogSource? logger4 = Logger;
			if (logger4 != null)
			{
				logger4.LogInfo((object)"[OPENXR_HOST] anisotropic ForceEnable 16 (D-104)");
			}
		}
		// D-186-FovScale: push the per-axis render-FOV scales to the native host before Configure so the four
		// angles the runtime reports from xrLocateViews get multiplied by (Config.FovScaleX, Config.FovScaleY)
		// before both the managed-side projection (StereoMath.Projection above) and the compositor submit
		// (Host::Submit's v.fov). A 1.0 value is the shipped pass-through and produces no log noise. Anything
		// outside 0.50-1.00 is refused by the native side (returns non-zero) and logged; we keep going on the
		// shipped 1.0 so the host never starts narrower than the user asked for.
		if (HostNative.SetFovScaleX(Config.FovScaleX) != 0)
		{
			ManualLogSource? loggerFov = Logger;
			if (loggerFov != null)
			{
				loggerFov.LogWarning((object)("[OPENXR_HOST] FOV_SCALE_X rejected by native host: " + Config.FovScaleX + " (expected 0.50-1.00; falling back to 1.0)"));
			}
			if (HostNative.SetFovScaleX(1.0f) != 0)
			{
				throw new InvalidOperationException("native fov scale X 1.0 fallback rejected");
			}
		}
		if (HostNative.SetFovScaleY(Config.FovScaleY) != 0)
		{
			ManualLogSource? loggerFov = Logger;
			if (loggerFov != null)
			{
				loggerFov.LogWarning((object)("[OPENXR_HOST] FOV_SCALE_Y rejected by native host: " + Config.FovScaleY + " (expected 0.50-1.00; falling back to 1.0)"));
			}
			if (HostNative.SetFovScaleY(1.0f) != 0)
			{
				throw new InvalidOperationException("native fov scale Y 1.0 fallback rejected");
			}
		}
		if (Config.FovScaleX < 1.0f || Config.FovScaleY < 1.0f)
		{
			ManualLogSource? loggerFov = Logger;
			if (loggerFov != null)
			{
				float xS = Config.FovScaleX;
				float yS = Config.FovScaleY;
				float area = xS * yS;
				loggerFov.LogInfo((object)("[OPENXR_HOST] FOV_SCALE X=" + xS.ToString("0.00") + " Y=" + yS.ToString("0.00") + " applied; ~" + (100.0 * (1.0f - area)).ToString("0") + "% pixel reduction estimated (D-186-FovScale)"));
			}
		}
		ApplyPerfProfile();
		try
		{
			RenderPipelineAsset currentRenderPipeline = GraphicsSettings.currentRenderPipeline;
			qualityProbe = new
			{
				pipeline = (((Object)(object)currentRenderPipeline == (Object)null) ? "built-in (no ScriptableRenderPipeline)" : ((object)currentRenderPipeline).GetType().FullName),
				pipelineProbe = ProbeRenderPipeline(currentRenderPipeline),
				qualityAntiAliasing = QualitySettings.antiAliasing,
				anisotropicFiltering = ((object)QualitySettings.anisotropicFiltering/*cast due to constrained. prefix*/).ToString(),
				lodBias = QualitySettings.lodBias,
				vSyncCount = QualitySettings.vSyncCount,
				eyeRtAntiAliasing = targets[0].antiAliasing,
				sourceAllowMsaa = source.allowMSAA,
				eyeAllowMsaa = eyes[0].allowMSAA,
				sourceComponents = ComponentNames(((Component)source).gameObject),
				eyeComponents = ComponentNames(((Component)eyes[0]).gameObject),
				graphics = ((object)SystemInfo.graphicsDeviceType/*cast due to constrained. prefix*/).ToString()
			};
			ManualLogSource? logger5 = Logger;
			if (logger5 != null)
			{
				logger5.LogInfo((object)("[OPENXR_HOST] QUALITY_AA pipeline=" + ((object)currentRenderPipeline)?.GetType().FullName + " qualityMsaa=" + QualitySettings.antiAliasing + " eyeRtMsaa=" + targets[0].antiAliasing + " sourceAllowMsaa=" + source.allowMSAA + " eyeAllowMsaa=" + eyes[0].allowMSAA));
			}
			ManualLogSource? logger6 = Logger;
			if (logger6 != null)
			{
				logger6.LogInfo((object)("[OPENXR_HOST] QUALITY_AA pipelineProbe=" + ProbeRenderPipeline(currentRenderPipeline) + " | sourceComponents=" + ComponentNames(((Component)source).gameObject) + " | eyeComponents=" + ComponentNames(((Component)eyes[0]).gameObject)));
			}
		}
		catch (Exception ex)
		{
			qualityProbe = null;
			ManualLogSource? logger7 = Logger;
			if (logger7 != null)
			{
				logger7.LogWarning((object)("[OPENXR_HOST] QUALITY_AA probe skipped: " + ex.GetType().Name + ": " + ex.Message));
			}
		}
		NativeLibrary.Load(Path.Combine(Paths.PluginPath, "openxr_loader.dll"));
		if (Config.DisableVSync)
		{
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = -1;
			ManualLogSource? logger8 = Logger;
			if (logger8 != null)
			{
				logger8.LogInfo((object)"[OPENXR_HOST] vSync disabled (D-095)");
			}
		}
		IntPtr library = NativeLibrary.Load(Path.Combine(Paths.PluginPath, "VhvrNativeHost.dll"));
		NativeLibrary.SetDllImportResolver(typeof(HostNative).Assembly, (string text, Assembly assembly, DllImportSearchPath? path) => (!(text == "VhvrNativeHost.dll")) ? IntPtr.Zero : library);
		if (Config.Headset && HostNative.ConfigureHeadset(Config.HeadsetSeconds) != 0)
		{
			throw new InvalidOperationException("native headset opt-in rejected");
		}
		if (HostNative.Configure(((Texture)targets[0]).GetNativeTexturePtr(), ((Texture)targets[1]).GetNativeTexturePtr(), output, Config.Runtime, Config.Frames, Config.CaptureFirstFrames ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("native config rejected");
		}
		if (Config.DeferredGpuWait)
		{
			if (HostNative.SetDeferredGpuWait(1) != 0)
			{
				throw new InvalidOperationException("native deferred gpu wait rejected");
			}
			ManualLogSource? logger9 = Logger;
			if (logger9 != null)
			{
				logger9.LogInfo((object)"[OPENXR_HOST] DEFERRED_GPU_WAIT on: the submit fence is proven one frame later (D-148b3)");
			}
		}
		if (Config.PrepareAhead)
		{
			if (HostNative.SetPrepareAhead(1) != 0)
			{
				throw new InvalidOperationException("native prepare ahead rejected");
			}
			ManualLogSource? logger10 = Logger;
			if (logger10 != null)
			{
				logger10.LogInfo((object)"[OPENXR_HOST] PREPARE_AHEAD on: the submit prepares the next XR frame (D-149)");
			}
		}
		if (Config.HandAim && Config.MenuFirst)
		{
			// D-180: native hand actions are created once, at session init, and EnableHands is only accepted
			// before that. MenuFirst defers the managed hand/weapon install to stage entry, which is too late,
			// so the native hands must be requested here or the menu laser can never have a controller pose.
			if (Marshal.SizeOf<NativeHandPose>() != 64 || Marshal.SizeOf<NativeHandsStatus>() != 144)
			{
				throw new InvalidOperationException("native hands ABI mismatch");
			}
			if (HandAimNative.EnableHands(1) != 0)
			{
				throw new InvalidOperationException("native hand input rejected (menu-first)");
			}
			nativeHandsEnabledEarly = true;
			ManualLogSource? menuHandsLogger = Logger;
			if (menuHandsLogger != null)
			{
				menuHandsLogger.LogInfo((object)"[OPENXR_HOST] MENU_FIRST native hands enabled before session init (D-180)");
			}
		}
		if (Config.HandAim && !Config.MenuFirst)
		{
			if (Marshal.SizeOf<NativeHandPose>() != 64 || Marshal.SizeOf<NativeHandsStatus>() != 144)
			{
				throw new InvalidOperationException("native hands ABI mismatch");
			}
			if (!Config.GameplayOnly)
			{
				throw new InvalidOperationException("HandAim requires GameplayOnly; menu cameras have no weapon slots");
			}
			if (Config.HandFire && !Config.HandAim)
			{
				throw new InvalidOperationException("HandFire requires HandAim");
			}
			if (Config.HandUi && !Config.HandAim)
			{
				throw new InvalidOperationException("HandUi requires HandAim");
			}
			if (Config.HandInput && !Config.HandAim)
			{
				throw new InvalidOperationException("HandInput requires HandAim");
			}
			if (Config.HandShot && !Config.HandAim)
			{
				throw new InvalidOperationException("HandShot requires HandAim");
			}
			if (HandAimNative.EnableHands(1) != 0)
			{
				throw new InvalidOperationException("native hand input rejected");
			}
			handAim = new HandAimDriver(Config.Output, JsonOptions);
			handAim.UiEnabled = Config.HandUi;
			handAim.InputEnabled = Config.HandInput;
			handAim.SnapDegrees = Math.Clamp(Config.SnapTurnDegrees, 0f, 180f);
			handAim.HideHudBackdrop = Config.HideHudBackdrop;
			handAim.UiVerticalOffset = HudLayoutPolicy.ClampVerticalOffset(Config.UiVerticalOffset);
			handAim.GameBackdropMode = Config.GameBackdropMode;
			handAim.MarkersEnabled = Config.WorldMarkers == "on";
			handAim.MarkersMode = Config.WorldMarkers;
			handAim.MarkerKindsCsv = Config.MarkerKinds;
			handAim.MarkerKinds = kinds;
			handAim.MarkerMaxDistance = WorldMarkerPolicy.ClampMaxDistance(Config.MarkerMaxDistance);
			handAim.MarkerMaxCount = WorldMarkerPolicy.ClampMaxCount(Config.MarkerMaxCount);
			ManualLogSource? logger11 = Logger;
			if (logger11 != null)
			{
				logger11.LogInfo((object)("[OPENXR_HOST] WORLD_MARKERS mode=" + Config.WorldMarkers + " kinds=" + Config.MarkerKinds + " maxDistance=" + (WorldMarkerPolicy.IsDistanceLimited(handAim.MarkerMaxDistance) ? handAim.MarkerMaxDistance.ToString("F1") : "none") + " maxCount=" + handAim.MarkerMaxCount + " (D-161; semi-transparent arrows, depth test always on, off-gaze targets on a 40 degree ring)"));
			}
			ManualLogSource? logger12 = Logger;
			if (logger12 != null)
			{
				logger12.LogInfo((object)("[OPENXR_HOST] GAME_BACKDROP mode=" + Config.GameBackdropMode + " (authored = the game's own backdrops; hide = pre-D-155 cull; D-155)"));
			}
			handAim.LeftWeaponTwistDeg = HandAimPolicy.ClampWeaponTwist(Config.LeftWeaponTwistDeg);
			handAim.RightWeaponTwistDeg = HandAimPolicy.ClampWeaponTwist(Config.RightWeaponTwistDeg);
			ManualLogSource? logger13 = Logger;
			if (logger13 != null)
			{
				logger13.LogInfo((object)("[OPENXR_HOST] WEAPON_TWIST left=" + handAim.LeftWeaponTwistDeg.ToString("F1") + " right=" + handAim.RightWeaponTwistDeg.ToString("F1") + " (0 = authored rest pose, D-153)"));
			}
			handAim.InstallUiRender(source, eyes[0], eyes[1]);
			if (handAim.UiRenderError != null)
			{
				ManualLogSource? logger14 = Logger;
				if (logger14 != null)
				{
					logger14.LogWarning((object)("[OPENXR_HOST] UI_RENDER " + handAim.UiRenderError));
				}
			}
			handAim.HapticEnabled = Config.HandHaptic;
			handAim.HapticAmplitude = Config.HapticAmplitude;
			handAim.HapticMilliseconds = Config.HapticMilliseconds;
			handAim.StockFireCadence = Config.HandFire && Config.StockFireCadence;
			handAim.AutonomousHeadAim = Config.AutonomousHeadAim;
			handAim.TurretCycle = string.IsNullOrEmpty(Config.TurretCycle) ? "Stick" : Config.TurretCycle; // D-206 (BUG-204)
			if(Config.RenderTimingDiagnostics) // D-136/D-138, restored (BUG-205)
			{
				handAim.InstallRenderTiming(source, eyes[0], eyes[1], Config.Output);
				ManualLogSource? renderTimingLogger = Logger;
				if (renderTimingLogger != null)
				{
					renderTimingLogger.LogInfo((object)("[OPENXR_HOST] RENDER_TIMING installed; read-only, 180s bounded; NOT_ROOTCAUSE_NOT_ACCEPTED"));
				}
			}
			handAim.InstallWeaponRuntime(source, player);
			if (Config.HandShot)
			{
				ShotAim shotAim = new ShotAim();
				bool flag = shotAim.Install(source, handAim, overrideArg: true, Config.HandShotTracer);
				handAim.Shot = shotAim;
				ManualLogSource? logger15 = Logger;
				if (logger15 != null)
				{
					logger15.LogInfo((object)("[OPENXR_HOST] SHOT_AIM patch installed=" + flag + " tracer=" + shotAim.TracerPatched + " error=" + shotAim.Error));
				}
			}
			handAim.InstallFlameEndpoint(Config.FlameEndpointVfx);
			handAim.InstallFlameEmitterPin(Config.FlameEmitterPin, eyes[0], eyes[1]);
			if (Config.HandFire)
			{
				ManualLogSource? logger16 = Logger;
				if (logger16 != null)
				{
					logger16.LogInfo((object)"[OPENXR_HOST] HAND_FIRE enabled: trigger calls the game per-slot fire path");
				}
			}
		}
		callback = HostNative.GetRenderEvent();
		configured = true;
		sessionStarted = DateTime.UtcNow;
		lastFrameProgress = sessionStarted;
		GL.IssuePluginEvent(callback, 0);
		ManualLogSource? logger17 = Logger;
		if (logger17 != null)
		{
			logger17.LogInfo((object)"[OPENXR_HOST] REQUESTED same-device render-thread bridge; not accepted");
		}
	}

	[HideFromIl2Cpp]
	private static string ObjectPath(Transform node)
	{
		string text = SafeName((Object?)(object)node);
		int num = 0;
		while ((Object)(object)node.parent != (Object)null && num < 32)
		{
			node = node.parent;
			text = SafeName((Object?)(object)node) + "/" + text;
			num++;
		}
		return text;
	}

	[HideFromIl2Cpp]
	private static string SafeName(Object? value, string fallback = "<destroyed>")
	{
		if (value == (Object)null)
		{
			return fallback;
		}
		try
		{
			return value.name;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	[HideFromIl2Cpp]
	private void PrepareCameras(NativeHostStatus status)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)source == (Object)null)
		{
			throw new InvalidOperationException("camera lost");
		}
		Vector3 val = anchorPosition;
		if (Config.GameplayOnly)
		{
			if ((Object)(object)player == (Object)null)
			{
				throw new InvalidOperationException("player lost");
			}
			val += ((Component)player).transform.position - playerAnchor;
		}
		for (int i = 0; i < 2; i++)
		{
			NativeEyePose pose = ((i == 0) ? status.Left : status.Right);
			float[] array = StereoMath.UnityPosition(pose.X, pose.Y, pose.Z);
			float[] array2 = StereoMath.UnityOrientation(pose.Qx, pose.Qy, pose.Qz, pose.Qw);
			// D-195 (regression after 0.9-final): the eye camera was rotated 180deg around Z because the OpenXR->Unity
			// axis conversion (StereoMath.UnityOrientation: -x, -y, z, w) was applied a SECOND time on top of
			// anchorRotation, which is already the Unity-space camera rotation. SteamVR rendered the world upside-
			// down/sideways and the menu level-up cards appeared to spin because their panel plane was parallel to
			// the headset, not face-normal. Anchor rotation is now identity for composition; the axis-flip already
			// happened inside StereoMath. Position composition still uses anchorPosition so the menu/gameplay offset
			// is preserved exactly as before.
			Vector3 val2 = val + new Vector3(array[0], array[1], array[2]);
			Quaternion val3 = new Quaternion(array2[0], array2[1], array2[2], array2[3]);
			float[] array3 = StereoMath.UnityView(new float[3] { val2.x, val2.y, val2.z }, new float[4] { val3.x, val3.y, val3.z, val3.w });
			// D-186-FovScale: the native host already multiplied (pose.Left, pose.Right) by Config.FovScaleX and
			// (pose.Up, pose.Down) by Config.FovScaleY when it wrote them into HostStatus, so the projection
			// matrix here sees the same narrowed frustum the compositor will submit (a one-sided narrowing on
			// either axis would show black bars at the edges). 1.0 = pass-through for both axes.
			float[] array4 = StereoMath.Projection(pose.Left, pose.Right, pose.Up, pose.Down, source.nearClipPlane, source.farClipPlane);
			((Component)eyes[i]).transform.SetPositionAndRotation(val2, val3);
			eyes[i].ResetWorldToCameraMatrix();
			Matrix4x4 worldToCameraMatrix = Matrix(array3);
			Matrix4x4 worldToCameraMatrix2 = eyes[i].worldToCameraMatrix;
			for (int j = 0; j < 4; j++)
			{
				for (int k = 0; k < 4; k++)
				{
					if (MathF.Abs(worldToCameraMatrix[j, k] - worldToCameraMatrix2[j, k]) > 0.001f)
					{
						throw new InvalidOperationException("runtime camera view differs from Unity transform");
					}
				}
			}
			eyes[i].worldToCameraMatrix = worldToCameraMatrix;
			eyes[i].projectionMatrix = Matrix(array4);
			((Behaviour)eyes[i]).enabled = true;
			if (status.Frames % 10 == 0 && evidence.Count < 256)
			{
				evidence.Add(new
				{
					eye = i,
					pose = pose,
					view = array3,
					projection = array4,
					position = new float[3] { val2.x, val2.y, val2.z },
					frame = Time.frameCount,
					timeScale = Time.timeScale,
					// D-196 debug: the menu ray still skews ~90deg after dropping anchorRotation (D-196
					// follow-up) because we are guessing what to substitute. Dump the live anchorRotation
					// so the next receipt can be matched against the ray, then the right compensation
					// quaternion (or the right code path) can be picked exactly. Removed once ray alignment
					// is verified head-on.
					anchorRotation = new float[4] { anchorRotation.x, anchorRotation.y, anchorRotation.z, anchorRotation.w }
				});
			}
		}
		float[] array5 = StereoMath.UnityPosition(status.Left.X, status.Left.Y, status.Left.Z);
		float[] array6 = StereoMath.UnityPosition(status.Right.X, status.Right.Y, status.Right.Z);
		headLocal = new Vector3((array5[0] + array6[0]) * 0.5f, (array5[1] + array6[1]) * 0.5f, (array5[2] + array6[2]) * 0.5f);
		hasHeadLocal = true;
		if (handAim != null && (Object)(object)eyes[0] != (Object)null && (Object)(object)eyes[1] != (Object)null)
		{
			handAim.SetHead((((Component)eyes[0]).transform.position + ((Component)eyes[1]).transform.position) * 0.5f, Quaternion.Slerp(((Component)eyes[0]).transform.rotation, ((Component)eyes[1]).transform.rotation, 0.5f));
		}
	}

	[HideFromIl2Cpp]
	private void Save(NativeHostStatus status, string verdict)
	{
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			string text = ReceiptJson(status, verdict);
			File.WriteAllText(output, text);
			saveBytes += Encoding.UTF8.GetByteCount(text);
		}
		catch (Exception ex)
		{
			ManualLogSource? logger = Logger;
			if (logger != null)
			{
				logger.LogWarning((object)("[OPENXR_HOST] receipt write skipped: " + ex.GetType().Name + ": " + ex.Message));
			}
			try
			{
				File.WriteAllText(output, JsonSerializer.Serialize(new
				{
					verdict = verdict,
					endReason = endReason,
					stage = "M4b",
					updatedUtc = DateTime.UtcNow.ToString("o"),
					receiptError = ex.GetType().Name
				}, JsonOptions));
			}
			catch (Exception)
			{
			}
		}
		finally
		{
			double num = MsSince(timestamp);
			msSave += num;
			if (num > msWorstSave)
			{
				msWorstSave = num;
			}
			saveCalls++;
		}
	}

	[HideFromIl2Cpp]
	private string ReceiptJson(NativeHostStatus status, string verdict)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		string text = endReason;
		bool inGameOpenXrHost = status.AdapterMatched == 1 && status.RenderThread != 0;
		string startedUtc = started.ToString("o");
		string updatedUtc = DateTime.UtcNow.ToString("o");
		int processId = Environment.ProcessId;
		Scene activeScene = SceneManager.GetActiveScene();
		string name = activeScene.name;
		string sourceCamera = SafeName((Object?)(object)source, "none");
		string? colorSpace = ((object)QualitySettings.activeColorSpace/*cast due to constrained. prefix*/).ToString();
		bool gameplayOnly = Config.GameplayOnly;
		object? obj = gameplayIdentity;
		string sessionStartedUtc = sessionStarted.ToString("o");
		float timeScale = Time.timeScale;
		RenderTexture obj2 = targets[0];
		return JsonSerializer.Serialize(new
		{
			verdict = verdict,
			endReason = text,
			stage = "M4b",
			inGameOpenXrHost = inGameOpenXrHost,
			eyeSource = "gameCamera",
			runtimePoseSource = "live-located-per-submission",
			renderScheduling = "clone-enabled-three-engine-frames-then-plugin-event",
			startedUtc = startedUtc,
			startupReadySeconds = startupReadySeconds,
			startupWait = startupWaitSummary,
			updatedUtc = updatedUtc,
			processId = processId,
			scene = name,
			sourceCamera = sourceCamera,
			colorSpace = colorSpace,
			gameplayOnly = gameplayOnly,
			gameplayIdentity = obj,
			sessionStartedUtc = sessionStartedUtc,
			timeScale = timeScale,
			targetSrgb = ((obj2 != null) ? new bool?(obj2.sRGB) : ((bool?)null)),
			encoding = "Gamma project display-coded bytes or Linear project explicitly sRGB encoded; runtime SRGB swapchain",
			eyePreset = Config.EyePreset,
			eyeScale = Config.EyeScale,
			eyeWidth = eyeSizeWidth,
			eyeHeight = eyeSizeHeight,
			captureFirstFrames = Config.CaptureFirstFrames,
			gameBackdropMode = Config.GameBackdropMode,
			hideHudBackdrop = Config.HideHudBackdrop,
			worldMarkers = Config.WorldMarkers,
			markerKinds = Config.MarkerKinds,
			markerMaxDistance = Config.MarkerMaxDistance,
			markerMaxCount = Config.MarkerMaxCount,
			quality = qualityProbe,
			perfProfile = perfProfile,
			perf = PerfSnapshot(),
			status = status,
			evidence = evidence,
			monitorProbe = MonitorProbeSnapshot(),
			note = "Runtime copies are not composed scene/geometry acceptance. Headset world scale and pacing not verified."
		}, JsonOptions);
	}

	[HideFromIl2Cpp]
	private object PerfSnapshot()
	{
		return new
		{
			windowFrames = perfFrames,
			updateMs = Math.Round(msUpdate, 2),
			lateMs = Math.Round(msLate, 2),
			prepareMs = Math.Round(msPrepare, 2),
			saveMs = Math.Round(msSave, 2),
			saveWorstMs = Math.Round(msWorstSave, 2),
			saveCalls = saveCalls,
			saveBytes = saveBytes,
			unityDeltaAvgMs = Math.Round(unityDeltaSum / (double)Math.Max(1, perfFrames), 2),
			unityDeltaMaxMs = Math.Round(unityDeltaMax, 2),
			evidence = evidence.Count,
			managedHeapMb = ManagedHeapMb(),
			gc = GcCounts()
		};
	}

	[HideFromIl2Cpp]
	private static double ManagedHeapMb()
	{
		try
		{
			return Math.Round((double)GC.GetTotalMemory(forceFullCollection: false) / 1048576.0, 2);
		}
		catch (Exception)
		{
			return -1.0;
		}
	}

	[HideFromIl2Cpp]
	private static PerfSettings ReadPerfSettings()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return new PerfSettings(QualitySettings.shadowDistance, QualitySettings.shadowCascades, ((object)QualitySettings.shadowResolution/*cast due to constrained. prefix*/).ToString(), QualitySettings.pixelLightCount, QualitySettings.lodBias, QualitySettings.particleRaycastBudget, QualitySettings.realtimeReflectionProbes, QualitySettings.softParticles, QualitySettings.antiAliasing);
	}

	[HideFromIl2Cpp]
	private static string ApplyPerfSettings(PerfSettings t)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		List<string> list = new List<string>();
		try
		{
			if (QualitySettings.shadowDistance != t.ShadowDistance)
			{
				QualitySettings.shadowDistance = t.ShadowDistance;
			}
		}
		catch (Exception ex)
		{
			list.Add("shadowDistance:" + ex.GetType().Name);
		}
		try
		{
			if (QualitySettings.shadowCascades != t.ShadowCascades)
			{
				QualitySettings.shadowCascades = t.ShadowCascades;
			}
		}
		catch (Exception ex2)
		{
			list.Add("shadowCascades:" + ex2.GetType().Name);
		}
		try
		{
			ShadowResolution val = (ShadowResolution)Enum.Parse(((object)QualitySettings.shadowResolution).GetType(), t.ShadowResolution);
			if (QualitySettings.shadowResolution != val)
			{
				QualitySettings.shadowResolution = val;
			}
		}
		catch (Exception ex3)
		{
			list.Add("shadowResolution:" + ex3.GetType().Name);
		}
		try
		{
			if (QualitySettings.pixelLightCount != t.PixelLightCount)
			{
				QualitySettings.pixelLightCount = t.PixelLightCount;
			}
		}
		catch (Exception ex4)
		{
			list.Add("pixelLightCount:" + ex4.GetType().Name);
		}
		try
		{
			if (QualitySettings.lodBias != t.LodBias)
			{
				QualitySettings.lodBias = t.LodBias;
			}
		}
		catch (Exception ex5)
		{
			list.Add("lodBias:" + ex5.GetType().Name);
		}
		try
		{
			if (QualitySettings.particleRaycastBudget != t.ParticleRaycastBudget)
			{
				QualitySettings.particleRaycastBudget = t.ParticleRaycastBudget;
			}
		}
		catch (Exception ex6)
		{
			list.Add("particleRaycastBudget:" + ex6.GetType().Name);
		}
		try
		{
			if (QualitySettings.realtimeReflectionProbes != t.RealtimeReflectionProbes)
			{
				QualitySettings.realtimeReflectionProbes = t.RealtimeReflectionProbes;
			}
		}
		catch (Exception ex7)
		{
			list.Add("realtimeReflectionProbes:" + ex7.GetType().Name);
		}
		try
		{
			if (QualitySettings.softParticles != t.SoftParticles)
			{
				QualitySettings.softParticles = t.SoftParticles;
			}
		}
		catch (Exception ex8)
		{
			list.Add("softParticles:" + ex8.GetType().Name);
		}
		try
		{
			if (QualitySettings.antiAliasing != t.AntiAliasing)
			{
				QualitySettings.antiAliasing = t.AntiAliasing;
			}
		}
		catch (Exception ex9)
		{
			list.Add("antiAliasing:" + ex9.GetType().Name);
		}
		return string.Join(",", list);
	}

	// D-187: the old ApplyGraphicsQuality() helper is gone. The host used to read Config.GraphicsQuality here, log
	// the level/level-name table and call QualitySettings.SetQualityLevel when the value was in range. Removing it
	// makes "the game uses its own quality level" the only behaviour, which is the behaviour the player sees in
	// 0.7. The earlier method's log lines (GRAPHICS_QUALITY levels= / applied / requested-out-of-range / skipped)
	// therefore no longer appear, by design.

	[HideFromIl2Cpp]
	private void ApplyPerfProfile()
	{
		try
		{
			PerfSettings current = ReadPerfSettings();
			PerfSettings? perfSettings = PerfProfilePolicy.Target(Config.PerfProfile, current);
			if (!perfSettings.HasValue)
			{
				perfProfile = new
				{
					profile = Config.PerfProfile,
					applied = false,
					before = current.Describe()
				};
				ManualLogSource? logger = Logger;
				if (logger != null)
				{
					logger.LogInfo((object)("[OPENXR_HOST] PERF_PROFILE profile=" + Config.PerfProfile + " applied=False game settings untouched | " + current.Describe()));
				}
				return;
			}
			string text = ApplyPerfSettings(perfSettings.Value);
			PerfSettings perfSettings2 = ReadPerfSettings();
			perfProfile = new
			{
				profile = Config.PerfProfile,
				applied = true,
				before = current.Describe(),
				target = perfSettings.Value.Describe(),
				after = perfSettings2.Describe(),
				failures = text
			};
			ManualLogSource? logger2 = Logger;
			if (logger2 != null)
			{
				logger2.LogInfo((object)("[OPENXR_HOST] PERF_PROFILE profile=" + Config.PerfProfile + " applied=True before=" + current.Describe()));
			}
			ManualLogSource? logger3 = Logger;
			if (logger3 != null)
			{
				logger3.LogInfo((object)("[OPENXR_HOST] PERF_PROFILE after=" + perfSettings2.Describe() + ((text.Length > 0) ? (" failures=" + text) : "") + " note=reversible engine dial, lower values are a quality cost, not a claimed fps gain"));
			}
		}
		catch (Exception ex)
		{
			perfProfile = null;
			ManualLogSource? logger4 = Logger;
			if (logger4 != null)
			{
				logger4.LogWarning((object)("[OPENXR_HOST] PERF_PROFILE skipped: " + ex.GetType().Name + ": " + ex.Message));
			}
		}
	}

	[HideFromIl2Cpp]
	private void TickMonitorCostProbe(NativeHostStatus status)
	{
		try
		{
			if ((Object)(object)source == (Object)null)
			{
				return;
			}
			if (monitorProbeOrigin < 0)
			{
				long num = status.Frames / 120 + 1;
				monitorProbeOrigin = num * 120;
				ManualLogSource? logger = Logger;
				if (logger != null)
				{
					logger.LogInfo((object)("[OPENXR_HOST] MONITOR_PROBE armed origin=" + monitorProbeOrigin + " framesPerPhase=" + 120 + " phases=" + 3 + " note=each phase equals one native timing window"));
				}
				return;
			}
			long num2 = status.Frames - monitorProbeOrigin;
			if (num2 < 0)
			{
				return;
			}
			int num3 = MonitorCostProbePolicy.Phase(num2);
			bool flag = MonitorCostProbePolicy.SourceDisabled(num2);
			if (!monitorProbeApplied || num3 != monitorProbePhase || flag != monitorProbeDisabled)
			{
				SetMonitorView(!flag);
				monitorProbePhase = num3;
				monitorProbeDisabled = flag;
				if (!monitorProbeApplied)
				{
					monitorProbeApplied = true;
					monitorProbeAppliedWindows = 0;
				}
				else if (num3 < 3)
				{
					monitorProbeAppliedWindows++;
				}
				ManualLogSource? logger2 = Logger;
				if (logger2 != null)
				{
					logger2.LogInfo((object)("[OPENXR_HOST] MONITOR_PROBE phase=" + num3 + " name=" + MonitorCostProbePolicy.Name(num3) + " frames=" + status.Frames + " intoPhase=" + MonitorCostProbePolicy.FramesIntoPhase(num2) + " sourceEnabled=" + !flag + ((num3 >= 3) ? " probe=done" : "")));
				}
			}
			if (flag)
			{
				monitorProbeDisabledFrames++;
			}
		}
		catch (Exception ex)
		{
			try
			{
				SetMonitorView(enabled: true);
			}
			catch (Exception)
			{
			}
			monitorProbeDisabled = false;
			ManualLogSource? logger3 = Logger;
			if (logger3 != null)
			{
				logger3.LogWarning((object)("[OPENXR_HOST] MONITOR_PROBE skipped: " + ex.GetType().Name + ": " + ex.Message));
			}
		}
	}

	[HideFromIl2Cpp]
	private void SetMonitorView(bool enabled)
	{
		if (!((Object)(object)source == (Object)null))
		{
			if (((Behaviour)source).enabled != enabled)
			{
				((Behaviour)source).enabled = enabled;
			}
			monitorProbeDisabled = !enabled;
		}
	}

	[HideFromIl2Cpp]
	private object MonitorProbeSnapshot()
	{
		return new
		{
			enabled = Config.MonitorCostProbe,
			active = (monitorProbeApplied && monitorProbePhase < 3),
			phase = monitorProbePhase,
			name = MonitorCostProbePolicy.Name(monitorProbePhase),
			origin = monitorProbeOrigin,
			framesPerPhase = 120,
			phases = 3,
			disabledFrames = monitorProbeDisabledFrames,
			appliedWindows = monitorProbeAppliedWindows,
			sourceDisabledNow = monitorProbeDisabled
		};
	}

	[HideFromIl2Cpp]
	private static int[] GcCounts()
	{
		try
		{
			return new int[3]
			{
				GC.CollectionCount(0),
				GC.CollectionCount(1),
				GC.CollectionCount(2)
			};
		}
		catch (Exception)
		{
			return new int[3] { -1, -1, -1 };
		}
	}

	[HideFromIl2Cpp]
	private void LogPerf(NativeHostStatus status)
	{
		try
		{
			ManualLogSource? logger = Logger;
			if (logger != null)
			{
				logger.LogInfo((object)("[OPENXR_HOST] PERF frames=" + status.Frames + " windowFrames=" + perfFrames + " updateMs=" + Math.Round(msUpdate, 2) + " lateMs=" + Math.Round(msLate, 2) + " prepareMs=" + Math.Round(msPrepare, 2) + " saveMs=" + Math.Round(msSave, 2) + " saveWorstMs=" + Math.Round(msWorstSave, 2) + " saveCalls=" + saveCalls + " saveBytes=" + saveBytes + " unityDeltaAvgMs=" + Math.Round(unityDeltaSum / (double)Math.Max(1, perfFrames), 2) + " unityDeltaMaxMs=" + Math.Round(unityDeltaMax, 2) + " evidence=" + evidence.Count + " managedHeapMb=" + ManagedHeapMb() + " gc=" + string.Join("/", GcCounts())));
			}
		}
		catch (Exception ex)
		{
			ManualLogSource? logger2 = Logger;
			if (logger2 != null)
			{
				logger2.LogWarning((object)("[OPENXR_HOST] PERF window skipped: " + ex.GetType().Name));
			}
		}
		perfFrames = 0;
		msUpdate = 0.0;
		msLate = 0.0;
		msPrepare = 0.0;
		msSave = 0.0;
		msWorstSave = 0.0;
		saveCalls = 0;
		saveBytes = 0L;
		unityDeltaSum = 0.0;
		unityDeltaMax = 0.0;
	}

	[HideFromIl2Cpp]
	private static string ProbeRenderPipeline(object? asset)
	{
		if (asset == null)
		{
			return "none (built-in pipeline)";
		}
		try
		{
			Type type = asset.GetType();
			List<string> list = new List<string>();
			string[] array = new string[4] { "msaaSampleCount", "supportsHDR", "antialiasing", "postProcessingEnabled" };
			foreach (string text in array)
			{
				PropertyInfo property = type.GetProperty(text);
				if (property == null)
				{
					list.Add(text + "=absent");
					continue;
				}
				try
				{
					list.Add(text + "=" + (property.GetValue(asset) ?? "null"));
				}
				catch (Exception ex)
				{
					list.Add(text + "=error:" + ex.GetType().Name);
				}
			}
			return type.FullName + " [" + string.Join(",", list) + "]";
		}
		catch (Exception ex2)
		{
			return "probe error: " + ex2.GetType().Name;
		}
	}

	[HideFromIl2Cpp]
	private static string ComponentNames(GameObject? go)
	{
		if ((Object)(object)go == (Object)null)
		{
			return "none";
		}
		try
		{
			List<string> list = new List<string>();
			foreach (Component component in go.GetComponents<Component>())
			{
				if (!((Object)(object)component == (Object)null))
				{
					list.Add(((object)component).GetType().Name);
					if (list.Count >= 16)
					{
						list.Add("...");
						break;
					}
				}
			}
			return string.Join(",", list);
		}
		catch (Exception ex)
		{
			return "error:" + ex.GetType().Name;
		}
	}

	[HideFromIl2Cpp]
	private void Fail(Exception e)
	{
		endReason = ((e is TimeoutException) ? "watchdog" : "error");
		try
		{
			SetMonitorView(enabled: true);
		}
		catch (Exception)
		{
		}
		done = true;
		ManualLogSource? logger = Logger;
		if (logger != null)
		{
			logger.LogError((object)("[OPENXR_HOST] FAILED: " + e));
		}
		try
		{
			handAim?.Release();
		}
		catch (Exception)
		{
		}
		handAim = null;
		if (configured)
		{
			closing = true;
			GL.IssuePluginEvent(callback, 3);
		}
		if (output.Length > 0)
		{
			File.WriteAllText(output + ".failure.json", JsonSerializer.Serialize(new
			{
				verdict = "FAIL",
				error = e.ToString()
			}, JsonOptions));
		}
		Camera[] array = eyes;
		foreach (Camera val in array)
		{
			if ((Object)(object)val != (Object)null)
			{
				((Behaviour)val).enabled = false;
			}
		}
		if (!configured)
		{
			Cleanup();
		}
	}

	[HideFromIl2Cpp]
	private bool StageEnded()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)source == (Object)null) && !((Object)(object)player == (Object)null))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			return activeScene.name != sourceScene;
		}
		return true;
	}

	[HideFromIl2Cpp]
	private void EndGameplay(NativeHostStatus status)
	{
		endReason = "level-exit";
		try
		{
			SetMonitorView(enabled: true);
		}
		catch (Exception)
		{
		}
		done = true;
		closing = true;
		try
		{
			handAim?.Release();
		}
		catch (Exception)
		{
		}
		handAim = null;
		Camera[] array = eyes;
		foreach (Camera val in array)
		{
			if ((Object)(object)val != (Object)null)
			{
				((Behaviour)val).enabled = false;
			}
		}
		GL.IssuePluginEvent(callback, 3);
		Save(status, Config.Headset ? "HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED" : "GAMEPLAY_LEFT_NOT_M4B_ACCEPTED");
		ManualLogSource? logger = Logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[OPENXR_HOST] GAMEPLAY_ENDED orderly close after stage teardown; diagnosis=" + OwnershipDiagnosis()));
		}
	}

	[HideFromIl2Cpp]
	private void ResetRunFolder()
	{
		if (runReset)
		{
			return;
		}
		runReset = true;
		try
		{
			string path = Path.Combine(Config.Output, "stop.request");
			bool flag = File.Exists(path);
			if (flag)
			{
				File.Delete(path);
			}
			List<string> list = new List<string>();
			string[] array = new string[11]
			{
				"host.json", "host.json.failure.json", "host.json.native.log", "hand-aim.json", "hand-aim.timeline.jsonl", "waiting.json", "gameplay-camera.json", "progress.json", "capture.request", "*.raw",
				"host.json.capture*"
			};
			foreach (string searchPattern in array)
			{
				list.AddRange(Directory.GetFiles(Config.Output, searchPattern));
			}
			if (list.Count == 0)
			{
				ManualLogSource? logger = Logger;
				if (logger != null)
				{
					logger.LogInfo((object)("[OPENXR_HOST] RUN_FOLDER clean (no previous receipts)" + (flag ? "; stale stop.request removed" : "")));
				}
				return;
			}
			string text = Path.Combine(Config.Output, "previous");
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			Directory.CreateDirectory(text);
			foreach (string item in list)
			{
				File.Move(item, Path.Combine(text, Path.GetFileName(item)));
			}
			ManualLogSource? logger2 = Logger;
			if (logger2 != null)
			{
				logger2.LogInfo((object)("[OPENXR_HOST] RUN_FOLDER previous receipts archived count=" + list.Count + " -> " + text + (flag ? "; stale stop.request removed" : "")));
			}
		}
		catch (Exception ex)
		{
			ManualLogSource? logger3 = Logger;
			if (logger3 != null)
			{
				logger3.LogWarning((object)("[OPENXR_HOST] RUN_FOLDER reset failed (receipts only): " + ex.Message));
			}
		}
	}

	private void Cleanup()
	{
		try
		{
			if (monitorProbeDisabled)
			{
				SetMonitorView(enabled: true);
			}
		}
		catch (Exception)
		{
		}
		try
		{
			handAim?.Release();
		}
		catch (Exception)
		{
		}
		handAim = null;
		for (int i = 0; i < 2; i++)
		{
			if ((Object)(object)eyes[i] != (Object)null)
			{
				((Behaviour)eyes[i]).enabled = false;
				eyes[i].targetTexture = null;
				Object.Destroy((Object)(object)((Component)eyes[i]).gameObject);
			}
			if ((Object)(object)targets[i] != (Object)null)
			{
				targets[i].Release();
				Object.Destroy((Object)(object)targets[i]);
			}
		}
	}

	private void OnApplicationQuit()
	{
		if (configured && (!done || closing))
		{
			GL.IssuePluginEvent(callback, 3);
		}
	}
	// D-178: menu-first handoff. Called from Update only while Config.MenuFirst is set and the host is still in
	// the main menu (player == null). Rescans for the gameplay camera exactly like Initialize's wait loop, and
	// the moment a stage is running it adopts the camera/player and installs hand/weapon integration.
	private bool TryEnterGameplay(bool immediate)
	{
		if (!immediate && DateTime.UtcNow < nextGameplayScan)
		{
			return false;
		}
		nextGameplayScan = DateTime.UtcNow.AddSeconds(1.0);
		Il2CppArrayBase<PlayerController> players = Resources.FindObjectsOfTypeAll<PlayerController>();
		List<GameplayCameraCandidate> candidates = new List<GameplayCameraCandidate>();
		List<PlayerController> owners = new List<PlayerController>();
		Scene activeScene = SceneManager.GetActiveScene();
		string scene = activeScene.name;
		Camera main = Camera.main;
		foreach (PlayerController candidate in players)
		{
			if ((Object)(object)candidate == (Object)null || (Object)(object)candidate.fp_camera == (Object)null)
			{
				continue;
			}
			Camera camera = candidate.fp_camera;
			candidates.Add(new GameplayCameraCandidate(scene, SafeName((Object?)(object)camera), (Object)(object)camera == (Object)(object)main, ((Component)camera).gameObject.activeInHierarchy, ((Behaviour)camera).enabled, !camera.orthographic, (Object)(object)camera.targetTexture != (Object)null, ((Component)candidate).gameObject.activeInHierarchy, ((Behaviour)candidate).enabled, (Object)(object)((Component)camera).transform.parent == (Object)(object)((Component)candidate).transform));
			owners.Add(candidate);
		}
		int selected = GameplayCameraPolicy.Select(candidates);
		if (selected < 0)
		{
			return false;
		}
		player = owners[selected];
		source = player.fp_camera;
		playerAnchor = ((Component)player).transform.position;
		sourceScene = scene;
		anchorPosition = ((Component)source).transform.position;
		anchorRotation = ((Component)source).transform.rotation;
		gameplayIdentity = new
		{
			policy = "active-player-fp-camera-direct-child-v1",
			scene = scene,
			sourceCamera = SafeName((Object?)(object)source, "none"),
			capturedUtc = DateTime.UtcNow.ToString("o"),
			processId = Environment.ProcessId,
			timeScale = Time.timeScale,
			cameraPath = ObjectPath(((Component)source).transform),
			playerPath = ObjectPath(((Component)player).transform),
			playerPosition = new float[3] { playerAnchor.x, playerAnchor.y, playerAnchor.z }
		};
		File.WriteAllText(Path.Combine(Config.Output, "gameplay-camera.json"), JsonSerializer.Serialize(gameplayIdentity, JsonOptions));
		// From here on the run is an ordinary gameplay run, so the gameplay watchdogs/ownership checks apply.
		Config.GameplayOnly = true;
		endReason = "running";
		// The eye cameras were prepared from the menu camera during Initialize; re-derive them from the gameplay
		// camera now so fov/culling/targets match the stage that is starting.
		// D-180: Initialize copied the MENU camera (ArenaCamera, cullingMask -1 = every layer). Without re-copying,
		// the eyes keep drawing layers the gameplay camera excludes - the minimap layer (bit 20, 'Mini Map Camera'
		// mask 1048576) shows up as red circles under enemies and extra icons. Gameplay 'Main Camera' mask is
		// 34603007 (layers 0-19 + 25). CopyFrom also copies targetTexture, so it is saved and restored.
		for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
		{
			if ((Object)(object)eyes[eyeIndex] == (Object)null)
			{
				continue;
			}
			RenderTexture eyeTarget = eyes[eyeIndex].targetTexture;
			eyes[eyeIndex].CopyFrom(source);
			eyes[eyeIndex].targetTexture = eyeTarget;
			((Behaviour)eyes[eyeIndex]).enabled = false;
		}
		ManualLogSource? maskLogger = Logger;
		if (maskLogger != null)
		{
			maskLogger.LogInfo((object)("[OPENXR_HOST] MENU_FIRST eye cameras re-copied from gameplay camera cullingMask=" + source.cullingMask + " (D-180)"));
		}
		HostNative.ReadStatus(out NativeHostStatus menuStatus);
		PrepareCameras(menuStatus);
		InstallHandAim();
		ManualLogSource? menuLogger = Logger;
		if (menuLogger != null)
		{
			menuLogger.LogInfo((object)("[OPENXR_HOST] MENU_FIRST gameplay camera acquired scene=" + scene + "; hand/weapon integration installed (D-178)"));
		}
		return true;
	}

	// D-181: after LeaveGameplayForMenu left source null (the next scene had no camera yet), or whenever the held source is
	// not the live main camera of the current menu scene, adopt Camera.main and rebuild the eyes from it.
	[HideFromIl2Cpp]
	private void AdoptMenuCameraIfNeeded()
	{
		Camera main = Camera.main;
		if ((Object)(object)main == (Object)null)
		{
			source = null;
			return;
		}
		if ((Object)(object)source == (Object)(object)main)
		{
			return;
		}
		source = main;
		sourceScene = SceneManager.GetActiveScene().name;
		anchorPosition = ((Component)source).transform.position;
		anchorRotation = ((Component)source).transform.rotation;
		for (int i = 0; i < 2; i++)
		{
			if ((Object)(object)eyes[i] != (Object)null)
			{
				RenderTexture eyeTarget = eyes[i].targetTexture;
				eyes[i].CopyFrom(source);
				eyes[i].targetTexture = eyeTarget;
				((Behaviour)eyes[i]).enabled = false;
			}
		}
		ManualLogSource? adoptLogger = Logger;
		if (adoptLogger != null)
		{
			adoptLogger.LogInfo((object)("[OPENXR_HOST] MENU_FIRST menu camera adopted '" + SafeName((Object?)(object)source, "none") + "' scene=" + sourceScene + " (D-181)"));
		}
	}

	// D-181: the stage ended and the game dropped back to a menu scene. True only in menu-first mode while a gameplay
	// player is adopted and the active scene is no longer the one the player belonged to.
	[HideFromIl2Cpp]
	private bool ReturnedToMenu()
	{
		// IL2CPP: a destroyed Unity object is only detected by the Unity-overloaded comparison, never by `is null`.
		if ((Object)(object)player == (Object)null)
		{
			// The adopted player was destroyed by the scene unload: that is the menu return.
			return true;
		}
		Scene active = SceneManager.GetActiveScene();
		if (active.name == sourceScene)
		{
			return false;
		}
		// A different scene is only the main menu if no gameplay player exists in it. This guards a stage-to-stage
		// load, where a fresh player appears and the normal ownership path must decide.
		return !PlayerExistsNow();
	}

	[HideFromIl2Cpp]
	private static bool PlayerExistsNow()
	{
		Il2CppArrayBase<PlayerController> found = Resources.FindObjectsOfTypeAll<PlayerController>();
		foreach (PlayerController candidate in found)
		{
			if ((Object)(object)candidate != (Object)null && (Object)(object)candidate.fp_camera != (Object)null && ((Component)candidate).gameObject.activeInHierarchy)
			{
				return true;
			}
		}
		return false;
	}

	// D-181: return to the menu-first state without ending the VR session. Everything TryEnterGameplay adopted is
	// released, and the host goes back to polling for the next stage. The menu camera becomes the eye source again so
	// the world-space menu (rebuilt by the extras plugin once player == null) is what the headset sees.
	[HideFromIl2Cpp]
	private void LeaveGameplayForMenu()
	{
		try
		{
			handAim?.Release();
		}
		catch (Exception)
		{
		}
		handAim = null;
		player = null;
		Config.GameplayOnly = false;
		endReason = "running";
		ownershipLostSince = -1.0;
		Camera menuCamera = Camera.main;
		if ((Object)(object)menuCamera != (Object)null)
		{
			source = menuCamera;
			sourceScene = SceneManager.GetActiveScene().name;
			anchorPosition = ((Component)source).transform.position;
			anchorRotation = ((Component)source).transform.rotation;
			for (int i = 0; i < 2; i++)
			{
				if ((Object)(object)eyes[i] != (Object)null)
				{
					RenderTexture eyeTarget = eyes[i].targetTexture;
					eyes[i].CopyFrom(source);
					eyes[i].targetTexture = eyeTarget;
					((Behaviour)eyes[i]).enabled = false;
				}
			}
		}
		else
		{
			// No camera yet (the next scene is still loading). Keep source null; Update skips the schedule until
			// TryEnterGameplay or the next frame finds one, instead of rendering against a dead camera.
			source = null;
		}
		ManualLogSource? returnLogger = Logger;
		if (returnLogger != null)
		{
			returnLogger.LogInfo((object)("[OPENXR_HOST] MENU_FIRST returned to menu scene=" + sourceScene + "; gameplay released, VR session kept (D-181)"));
		}
	}

	// D-178: the deferred half of the hand/weapon install. This mirrors the sequence Initialize runs inline for a
	// gameplay-first sitting. DUPLICATION IS DELIBERATE AND TEMPORARY: the inline block is left byte-for-byte as
	// the shipped D-171 behaviour, so the recovered baseline stays trustworthy. Unify the two when this source is
	// next refactored.
	private void InstallHandAim()
	{
		if (Marshal.SizeOf<NativeHandPose>() != 64 || Marshal.SizeOf<NativeHandsStatus>() != 144)
		{
			throw new InvalidOperationException("native hands ABI mismatch");
		}
		if (!nativeHandsEnabledEarly && HandAimNative.EnableHands(1) != 0)
		{
			throw new InvalidOperationException("native hand input rejected");
		}
		if (!WorldMarkerPolicy.TryParseKinds(Config.MarkerKinds, out string[] kinds))
		{
			throw new ArgumentException("invalid marker kinds");
		}
		handAim = new HandAimDriver(Config.Output, JsonOptions);
		handAim.UiEnabled = Config.HandUi;
		handAim.InputEnabled = Config.HandInput;
		handAim.SnapDegrees = Math.Clamp(Config.SnapTurnDegrees, 0f, 180f);
		handAim.HideHudBackdrop = Config.HideHudBackdrop;
		handAim.UiVerticalOffset = HudLayoutPolicy.ClampVerticalOffset(Config.UiVerticalOffset);
		handAim.GameBackdropMode = Config.GameBackdropMode;
		handAim.MarkersEnabled = Config.WorldMarkers == "on";
		handAim.MarkersMode = Config.WorldMarkers;
		handAim.MarkerKindsCsv = Config.MarkerKinds;
		handAim.MarkerKinds = kinds;
		handAim.MarkerMaxDistance = WorldMarkerPolicy.ClampMaxDistance(Config.MarkerMaxDistance);
		handAim.MarkerMaxCount = WorldMarkerPolicy.ClampMaxCount(Config.MarkerMaxCount);
		handAim.LeftWeaponTwistDeg = HandAimPolicy.ClampWeaponTwist(Config.LeftWeaponTwistDeg);
		handAim.RightWeaponTwistDeg = HandAimPolicy.ClampWeaponTwist(Config.RightWeaponTwistDeg);
		handAim.InstallUiRender(source, eyes[0], eyes[1]);
		if (handAim.UiRenderError != null)
		{
			ManualLogSource? uiLogger = Logger;
			if (uiLogger != null)
			{
				uiLogger.LogWarning((object)("[OPENXR_HOST] UI_RENDER " + handAim.UiRenderError));
			}
		}
		handAim.HapticEnabled = Config.HandHaptic;
		handAim.HapticAmplitude = Config.HapticAmplitude;
		handAim.HapticMilliseconds = Config.HapticMilliseconds;
		handAim.StockFireCadence = Config.HandFire && Config.StockFireCadence;
		handAim.AutonomousHeadAim = Config.AutonomousHeadAim;
		handAim.TurretCycle = string.IsNullOrEmpty(Config.TurretCycle) ? "Stick" : Config.TurretCycle; // D-206 (BUG-204)
		if(Config.RenderTimingDiagnostics) // D-136/D-138, restored (BUG-205)
		{
			handAim.InstallRenderTiming(source, eyes[0], eyes[1], Config.Output);
			ManualLogSource? renderTimingLogger2 = Logger;
			if (renderTimingLogger2 != null)
			{
				renderTimingLogger2.LogInfo((object)("[OPENXR_HOST] RENDER_TIMING installed; read-only, 180s bounded; NOT_ROOTCAUSE_NOT_ACCEPTED"));
			}
		}
		handAim.InstallWeaponRuntime(source, player);
		if (Config.HandShot)
		{
			ShotAim shotAim = new ShotAim();
			bool patched = shotAim.Install(source, handAim, overrideArg: true, Config.HandShotTracer);
			handAim.Shot = shotAim;
			ManualLogSource? shotLogger = Logger;
			if (shotLogger != null)
			{
				shotLogger.LogInfo((object)("[OPENXR_HOST] SHOT_AIM patch installed=" + patched + " tracer=" + shotAim.TracerPatched + " error=" + shotAim.Error));
			}
		}
		handAim.InstallFlameEndpoint(Config.FlameEndpointVfx);
		handAim.InstallFlameEmitterPin(Config.FlameEmitterPin, eyes[0], eyes[1]);
	}

}
