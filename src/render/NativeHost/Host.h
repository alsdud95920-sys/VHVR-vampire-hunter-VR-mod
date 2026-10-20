#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winreg.h>
#include <d3d11.h>
#include <dxgi.h>
#include <wrl/client.h>
#define XR_USE_PLATFORM_WIN32
#define XR_USE_GRAPHICS_API_D3D11
#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>
#include <mutex>
#include <vector>
#include <fstream>
#include <chrono>
#include <stdexcept>
#include <string>
#include <cstring>
#include <cstdio>
#include <algorithm>
using Microsoft::WRL::ComPtr;
using Clock = std::chrono::steady_clock;
inline double Ms(Clock::duration d) {return std::chrono::duration<double,std::milli>(d).count();}
struct EyePose { float x,y,z,qx,qy,qz,qw,left,right,up,down; };
// Optional controller sample (soft-gated per-hand aim path). Separate ABI: HostStatus is unchanged.
// D-091: thumbstick (x,y), stick click, lower face button (A/X) and menu button were added for locomotion/menu.
// squeeze (grip, 0..1) is appended by D-206 (BUG-204); the managed mirror in HandAimDriver.NativeHandPose must keep
// this exact order, and the field is read-only for the game (the mod never writes a controller state).
struct HandPose { int active; float x,y,z,qx,qy,qz,qw; float trigger; int face; float sx,sy; int stick; int lower; int menu; float squeeze; };
struct HandsStatus { int available, seq, frames, reserved; HandPose hands[2]; };
struct HostStatus {
    int state=0, width=0, height=0, frames=0, gpuCompleted=0;
    DWORD mainThread=0, renderThread=0;
    int adapterMatched=0, stopping=0, ended=0;
    double holdSeconds=0;
    EyePose eyes[2]{};
};
struct Host {
    std::mutex mutex;
    HostStatus status;
    ComPtr<ID3D11Texture2D> source[2];
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<ID3D11Query> query;
    XrInstance instance=XR_NULL_HANDLE;
    XrSession session=XR_NULL_HANDLE;
    XrSpace space=XR_NULL_HANDLE;
    XrSwapchain swapchains[2]{};
    std::vector<XrSwapchainImageD3D11KHR> images[2];
    XrView views[2]{};
    XrFrameState frame{XR_TYPE_FRAME_STATE};
    XrSessionState sessionState=XR_SESSION_STATE_UNKNOWN;
    bool running=false, frameBegun=false;
    std::string output, error;
    bool handsRequested=false, handsCreated=false, handsReady=false;
    // D-087: headset runtime is refused unless the managed side explicitly opts in (ConfigureHeadset) before init.
    // D-145: budgetSeconds 0 = no wall-time limit, so the session ends only on a runtime event or the managed watchdog.
    bool headsetAllowed=false;
    int budgetSeconds=55;
    // D-147: the frame-30 raw eye/source texture dump is opt-in (it wrote ~74 MB per sitting unasked); requested
    // captures (RequestCapture) are independent of this flag.
    bool frameDump=false;
    HandsStatus hands{};
    std::string handsError;
    // D-103: haptic requests are queued by the managed side and applied on the render thread right after xrSyncActions.
    // A haptic failure disables haptics only; hand input and the video path keep running.
    float hapticAmplitude[2]{};
    long long hapticDurationNs[2]{};
    bool hapticPending[2]{};
    bool hapticDisabled=false;
    int hapticApplied=0, hapticSkipped=0;
    std::string hapticError;
    // D-104: frame timing, written to the native log only (no ABI change). Sums over the current 120-submit window, in milliseconds.
    Clock::time_point preparedAt{}, lastSubmitEnd{};
    double waitMsSum=0, prepToSubmitMsSum=0, submitMsSum=0, cycleMsSum=0, displayPeriodMs=0;
    // D-148: submitMs split (measurement only, no ABI change). copyMs = both-eye copy issue + GPU fence wait,
    // fenceMs = the GPU completion wait inside it, endFrameMs = the xrEndFrame call. Sums over the same 120-submit window.
    double copyIssueMsSum=0, copyFenceMsSum=0, endFrameMsSum=0;
    // D-148b: the cycleMs decomposition (measurement only, no ABI change). One submit-to-submit cycle is
    // gapMs (previous submit end -> Prepare entry: render-thread turnaround + prepare dispatch)
    // + pumpMs (Pump at the Prepare entry) + waitMs (xrWaitFrame) + prepBodyMs (xrBeginFrame + xrLocateViews +
    // HandsSync) + prepToSubmitMs (Unity's own frame until the render-thread submit) + submitMs.
    // handsMs is the HandsSync part of prepBodyMs; logMs is the time inside Log() and is a sub-span of the
    // others (never additive). A Prepare that never reaches Submit (empty frame) leaves prepBodyMs/handsMs
    // out of the window; gapMs is stamped at entry, so Prepare-side means use prepareCount.
    double prepBodyMsSum=0, handsMsSum=0, pumpMsSum=0, prepareGapMsSum=0, logMsSum=0;
    int waitCount=0, submitCount=0, cycleCount=0, prepareCount=0;
    // D-148b3: opt-in overlap experiment for the submit fence. When on, both eye copies are issued with CopyAndFlush and
    // their GPU completion proof is taken one frame later, at the start of the next Submit (ping-pong event queries), so
    // the CPU does not spin on Unity's queued GPU work inside the same submit. The drain runs in Close() before any
    // swapchain image is destroyed, so no copy is ever left unproven while its image disappears. Off = D-148 behaviour
    // (CopyAndWait proves completion inside the submit, before the image is released).
    bool deferredGpuWait=false, deferredReady=false;
    ComPtr<ID3D11Query> deferredQuery[2];
    bool deferredPending[2]{};
    int deferredProofs=0;
    // D-149: opt-in early frame prep (lever 1 of the frame-cost work). When on, Submit() prepares the next XR frame itself
    // (xrWaitFrame + xrBeginFrame + xrLocateViews + HandsSync), so the engine's very next frame can render the eyes
    // instead of burning a whole frame on a prepare-only pass; the prepare event that follows finds preparedAhead and
    // returns without doing the work twice. The published frame state stays exactly what the un-chained path publishes
    // (status.state==2 after the submit), so the managed phase machine can consume it in the same engine frame. Off =
    // the shipped behaviour (the prepare event does the work, one engine frame after the submit).
    bool prepareAhead=false, preparedAhead=false;
    // D-186-FovScale: per-axis render-FOV scales applied to the four angles OpenXR reports from
    // xrLocateViews before both the managed-side projection (StereoMath.Projection in OpenXrHost.cs) and
    // the compositor submit (the v.fov assignment in Host::Submit). 1.0 = pass-through (the shipped
    // value). Lower values narrow the rendered FOV so the GPU draws fewer pixels.
    //   fovScaleX scales angleLeft / angleRight (the horizontal frustum).
    //   fovScaleY scales angleUp   / angleDown  (the vertical frustum).
    // Either axis can be narrowed independently. The managed side pushes each once before Host::Init
    // via SetFovScaleX() / SetFovScaleY(); values outside 0.50-1.00 are refused and the previous
    // value is kept.
    float fovScaleX=1.0f;
    float fovScaleY=1.0f;
    double aheadMsSum=0;
    int aheadPrepares=0;
    // D-149: CPU split of the same windows (measurement only, no ABI change). CPU time is read with GetProcessTimes and
    // GetThreadTimes at the window boundaries, so prepCpuMs can be compared with prepToSubmitMs: CPU close to wall time
    // means the engine frame is CPU-bound (one busy thread), CPU far below wall time means it is waiting on the GPU.
    // gapCpuMs prices the post-submit turnaround the same way and submitCpuMs prices our own submit. A negative value
    // means "not sampled" - never a silent zero.
    double prepCpuMsSum=0, prepMainCpuMsSum=0, prepRenderCpuMsSum=0, submitCpuMsSum=0, gapCpuMsSum=0;
    int bodyCount=0, cpuSamples=0;
    bool cpuSampled=false;
    HANDLE mainThreadHandle=nullptr, renderThreadHandle=nullptr;
    double prepStartCpu=0, prepStartMainCpu=0, prepStartRenderCpu=0;
    double submitStartCpu=0, submitStartMainCpu=0, submitStartRenderCpu=0;
    double lastSubmitCpu=0, lastSubmitMainCpu=0, lastSubmitRenderCpu=0;
    std::ofstream log;
    Clock::time_point start, holdStart;
    int target=120, captureId=-1;
    void Init(); void Prepare(); void Submit(); void Close(); void Pump();
    // D-149: the prepare body: the event pump, the session/budget exits, xrWaitFrame + xrBeginFrame + xrLocateViews +
    // HandsSync and the empty-frame exit. Prepare() owns only the gap stamp and the prepared-ahead no-op, so the chained
    // call in Submit() and the prepare event run the identical body - and the pump still happens exactly once per frame,
    // which is what keeps a user-initiated stop visible in chained mode.
    void PrepareBody();
    // D-148b3: take any outstanding deferred GPU completion proof (used before swapchain destruction).
    void DrainDeferredGpuWaits();
    void EndEmptyFrame(const char* call);
    void RecordTiming(Clock::time_point submitStart);
    void Log(const std::string& phase);
    // D-148b: same as Log(), but accumulates the time inside it so the per-frame log writes on the render thread
    // are visible in the timing window instead of hiding in the cycle.
    void TimedLog(const std::string& phase);
};
static_assert(sizeof(EyePose)==44,"EyePose managed ABI");
static_assert(sizeof(HostStatus)==136,"HostStatus managed ABI");
static_assert(sizeof(HandPose)==64,"HandPose managed ABI"); // D-206: +float squeeze (60 -> 64)
static_assert(sizeof(HandsStatus)==144,"HandsStatus managed ABI"); // D-206: 16 + 2*64
// D-087/D-145: the simulator is always allowed for development; any real HMD runtime the loader reports is accepted
// only after the explicit headset opt-in. The Virtual-Desktop-only restriction is gone (D-145).
inline bool RuntimeNameAllowed(const std::string& name,bool headsetAllowed) {
    if(name.empty()) return false;
    if(name=="Meta XR Simulator") return true;
    return headsetAllowed;
}
extern Host host;
void Check(XrResult result,const char* call);
void Hr(HRESULT result,const char* call);
// Controller actions live in Hands.cpp (product DLL only); fault fixtures substitute local stubs.
void HandsInit(XrInstance instance,XrSession session);
void HandsSync(XrSession session,XrSpace space,XrTime time,HandsStatus& out);
bool HandsHaptic(XrSession session,int hand,float amplitude,long long durationNs);
void HandsDestroy();
// D-148: issueMs/fenceMs are optional out-parameters so every existing call site keeps working; they exist because the
// copy call mixes row-copy issue time with the GPU completion wait, and only the split says whether the cost is Unity's
// queued GPU work or our own copy submission.
void CopyAndWait(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,
    ID3D11Texture2D* destination,ID3D11Texture2D* source,bool flipY=false,double* issueMs=nullptr,double* fenceMs=nullptr);
// D-148b3: the same copy split into issue and proof so an opt-in overlap path can take the proof one frame later.
// CopyAndFlush keeps every check and the exact timing of CopyAndWait's issue half; WaitCopyCompletion is its wait half.
void CopyAndFlush(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,
    ID3D11Texture2D* destination,ID3D11Texture2D* source,bool flipY=false,double* issueMs=nullptr);
void WaitCopyCompletion(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,double* fenceMs=nullptr);
void SaveTexture(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Texture2D* texture,const std::string& path);
#define API extern "C" __declspec(dllexport)
API int __cdecl Configure(void* left,void* right,const char* output,const char* runtime,int target,int frameDump);
API void __cdecl ReadStatus(HostStatus* result);
API int __cdecl RequestPrepare();
API int __cdecl RequestCapture(int id);
API const char* __cdecl ReadError();
API int __cdecl CanReleaseSources();
API void* __cdecl GetRenderEvent();
API int __cdecl EnableHands(int enabled);
// D-148b3: opt-in overlap experiment (deferred submit fence). Only before the init event, like EnableHands.
API int __cdecl SetDeferredGpuWait(int enabled);
// D-149: opt-in early frame prep (the submit prepares the next frame). Only before the init event, like EnableHands.
API int __cdecl SetPrepareAhead(int enabled);
API int __cdecl ConfigureHeadset(int budgetSeconds);
// D-186-FovScale: pushes the managed-side FovScaleX (horizontal) and FovScaleY (vertical) to the native
// host. Each returns 0 on success, non-zero if the value is outside 0.50-1.00 (the previous value is
// kept on rejection). Must be called before Init (the same constraint as SetPrepareAhead /
// SetDeferredGpuWait) so PrepareBody and Submit both see them.
API int __cdecl SetFovScaleX(float scale);
API int __cdecl SetFovScaleY(float scale);
API int __cdecl ReadHands(HandsStatus* result);
API int __cdecl RequestHaptic(int hand,float amplitude,int durationMs);
API const char* __cdecl ReadHapticError();
API const char* __cdecl ReadHandsError();