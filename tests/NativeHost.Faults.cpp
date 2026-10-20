// Test-only XR symbols/GPU responses. This translation unit is never linked into the product DLL.
#include "Host.h"
#include "GpuWait.h"
#include <future>
#include <functional>
#include <iostream>
#include <sstream>
#include <thread>

namespace {
struct Responses {
    XrResult acquire=XR_SUCCESS,wait=XR_SUCCESS,release=XR_SUCCESS,endFrame=XR_SUCCESS;
    XrResult requestExit=XR_SUCCESS,endSession=XR_SUCCESS,poll=XR_EVENT_UNAVAILABLE;
    XrResult destroySwapchain=XR_SUCCESS,destroySpace=XR_SUCCESS,destroySession=XR_SUCCESS,destroyInstance=XR_SUCCESS;
    int failCopy=0,copyCalls=0,releaseCalls=0,emptyFrames=0,layers=0,endCalls=0,requestCalls=0;
    int timeoutCopy=0,errorCopy=0,proofCalls=0;
    bool proofFail=false;
    int handsSyncCalls=0,handsDestroyCalls=0,stopAfterPolls=0,polls=0;
    bool handsSyncFail=false,handsDestroyFail=false,hapticFail=false;
    int hapticCalls=0,lastHapticHand=-1;float lastHapticAmplitude=0.0f;long long lastHapticNs=0;
    int destroyedSwapchains=0,destroyedSpaces=0,destroyedSessions=0,destroyedInstances=0,saved=0;
    bool stopEvent=true,shouldRender=true,validPose=true;
    std::vector<std::string> order;
} fake;
void Require(bool value,const char* message) {if(!value) throw std::runtime_error(message);}
template<class Action> void Reject(Action action,const char* message) {
    try {action();} catch(const std::exception& e) {Require(std::string(e.what()).find(message)!=std::string::npos,"unexpected rejection");return;}
    throw std::runtime_error("expected rejection missing");
}
void Reset() {
    host.~Host();new(&host) Host();fake=Responses{};
    host.status.renderThread=GetCurrentThreadId();host.status.state=2;
    host.instance=reinterpret_cast<XrInstance>(1);host.session=reinterpret_cast<XrSession>(2);
    host.space=reinterpret_cast<XrSpace>(3);host.swapchains[0]=reinterpret_cast<XrSwapchain>(4);host.swapchains[1]=reinterpret_cast<XrSwapchain>(5);
    host.sessionState=XR_SESSION_STATE_STOPPING;host.running=true;host.frameBegun=true;
    host.start=Clock::now();host.frame.predictedDisplayTime=1;
    for(auto& images:host.images) images.push_back({XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR});
}
void Render(int event) {reinterpret_cast<void(__stdcall*)(int)>(GetRenderEvent())(event);}
void Closed(bool orderly=true) {
    Require(!host.instance&&!host.session&&!host.space&&!host.swapchains[0]&&!host.swapchains[1],"XR handles retained");
    Require(!host.frameBegun&&!host.running&&host.images[0].empty()&&host.images[1].empty(),"child state retained");
    Require(CanReleaseSources()==1,"source release not acknowledged");
    if(orderly)Require(host.status.ended==1,"orderly EndSession missing");
}
void PollCase(int completeAfter,int failAfter,bool timeout) {
    int calls=0,pauses=0;std::chrono::milliseconds elapsed{0};
    auto wait=[&]() {WaitForGpuCompletion([&]() {
        calls++;if(calls==failAfter)Hr(E_FAIL,"GPU query GetData");return calls>=completeAfter;
    },[&](){return elapsed;},[&](){elapsed+=std::chrono::milliseconds(500);pauses++;});};
    if(timeout)Reject(wait,"GPU completion timeout; image not released");
    else if(failAfter>0)Reject(wait,"GPU query GetData");
    else {wait();Require(calls==completeAfter&&pauses==completeAfter-1,"GPU polling sequence");}
    Require(pauses<=5,"GPU wait was not bounded");
}
// The fixture has no GPU of its own; the close drain still needs a provable context, so tests that leave a queued copy
// behind create one real device (no OpenXR session, no game).
void FixtureDevice() {
    D3D_FEATURE_LEVEL level;
    Hr(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&host.device,&level,&host.context),"fixture device");
}
}

void Hr(HRESULT result,const char* call) {if(FAILED(result))throw std::runtime_error(call);}
// Test-local controller symbols: the fixture never links Hands.cpp or an OpenXR loader.
void HandsInit(XrInstance,XrSession) {throw std::runtime_error("unexpected hands init in fault fixture");}
void HandsSync(XrSession,XrSpace,XrTime,HandsStatus& out) {
    fake.handsSyncCalls++;
    if(fake.handsSyncFail)throw std::runtime_error("injected xrSyncActions failure");
    out=HandsStatus{};out.available=1;out.hands[1].active=1;out.hands[1].qw=1;out.hands[1].trigger=0.75f;out.hands[1].face=1;
}
void HandsDestroy() {fake.handsDestroyCalls++;if(fake.handsDestroyFail)throw std::runtime_error("injected hands destroy failure");}
bool HandsHaptic(XrSession,int hand,float amplitude,long long durationNs) {
    fake.hapticCalls++;fake.lastHapticHand=hand;fake.lastHapticAmplitude=amplitude;fake.lastHapticNs=durationNs;
    if(fake.hapticFail)throw std::runtime_error("injected xrApplyHapticFeedback failure");
    return true;
}
// D-148: the production signature gained two optional out-parameters (copy issue vs GPU fence wait). The fixture keeps
// the same shape and reports fixed values so the split stays assertable without any GPU.
// The defaults live on the Host.h declaration; repeating them here is a C2572 redefinition.
void CopyAndWait(ID3D11Device*,ID3D11DeviceContext*,ID3D11Query*,ID3D11Texture2D*,ID3D11Texture2D*,bool,double* issueMs,double* fenceMs) {
    fake.copyCalls++;fake.order.push_back("copy");
    if(fake.copyCalls==fake.failCopy)throw std::runtime_error("injected GPU failure");
    if(fake.copyCalls==fake.timeoutCopy||fake.copyCalls==fake.errorCopy) {
        std::chrono::milliseconds elapsed{0};
        WaitForGpuCompletion([&](){if(fake.copyCalls==fake.errorCopy)Hr(E_FAIL,"GPU query GetData");return false;},
            [&](){return elapsed;},[&](){elapsed+=std::chrono::milliseconds(500);});
    }
    PollCase(4,0,false); // Delayed but completed before release.
    if(issueMs)*issueMs=0.5;
    if(fenceMs)*fenceMs=0.25;
}
// D-148b3: the queued half and the proof half of the same copy, for the opt-in deferred-fence path. The fixture keeps no
// GPU at all, so the proof only reports a fixed wait; the point is when Host::Submit asks for it.
void CopyAndFlush(ID3D11Device*,ID3D11DeviceContext*,ID3D11Query*,ID3D11Texture2D*,ID3D11Texture2D*,bool,double* issueMs) {
    fake.copyCalls++;fake.order.push_back("copy");
    if(fake.copyCalls==fake.failCopy)throw std::runtime_error("injected GPU failure");
    if(issueMs)*issueMs=0.5;
}
void WaitCopyCompletion(ID3D11Device*,ID3D11DeviceContext*,ID3D11Query*,double* fenceMs) {
    fake.proofCalls++;fake.order.push_back("proof");
    if(fake.proofFail)throw std::runtime_error("injected deferred GPU query failure");
    if(fenceMs)*fenceMs=0.25;
}
void SaveTexture(ID3D11Device*,ID3D11DeviceContext*,ID3D11Texture2D*,const std::string&) {fake.saved++;}
XRAPI_ATTR XrResult XRAPI_CALL xrPollEvent(XrInstance,XrEventDataBuffer* event) {
    if(fake.poll!=XR_EVENT_UNAVAILABLE)return fake.poll;
    if(fake.stopEvent&&host.sessionState!=XR_SESSION_STATE_STOPPING) {
        if(fake.polls++<fake.stopAfterPolls)return XR_EVENT_UNAVAILABLE;
        auto changed=reinterpret_cast<XrEventDataSessionStateChanged*>(event);
        *changed={XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED};changed->session=host.session;changed->state=XR_SESSION_STATE_STOPPING;
        return XR_SUCCESS;
    }
    return XR_EVENT_UNAVAILABLE;
}
XRAPI_ATTR XrResult XRAPI_CALL xrWaitFrame(XrSession,const XrFrameWaitInfo*,XrFrameState* frame) {
    frame->shouldRender=fake.shouldRender;frame->predictedDisplayTime=1;return XR_SUCCESS;
}
XRAPI_ATTR XrResult XRAPI_CALL xrBeginFrame(XrSession,const XrFrameBeginInfo*) {return XR_SUCCESS;}
XRAPI_ATTR XrResult XRAPI_CALL xrLocateViews(XrSession,const XrViewLocateInfo*,XrViewState* state,uint32_t,uint32_t* count,XrView* views) {
    state->viewStateFlags=fake.validPose?XR_VIEW_STATE_ORIENTATION_VALID_BIT|XR_VIEW_STATE_POSITION_VALID_BIT:0;*count=2;
    for(int eye=0;eye<2;eye++){views[eye].pose.orientation.w=1;views[eye].pose.position.x=eye==0?-0.03f:0.03f;}
    return XR_SUCCESS;
}
XRAPI_ATTR XrResult XRAPI_CALL xrAcquireSwapchainImage(XrSwapchain,const XrSwapchainImageAcquireInfo*,uint32_t* index) {*index=0;return fake.acquire;}
XRAPI_ATTR XrResult XRAPI_CALL xrWaitSwapchainImage(XrSwapchain,const XrSwapchainImageWaitInfo*) {return fake.wait;}
XRAPI_ATTR XrResult XRAPI_CALL xrReleaseSwapchainImage(XrSwapchain,const XrSwapchainImageReleaseInfo*) {fake.releaseCalls++;fake.order.push_back("release");return fake.release;}
XRAPI_ATTR XrResult XRAPI_CALL xrEndFrame(XrSession,const XrFrameEndInfo* end) {
    if(end->layerCount==0)fake.emptyFrames++;else {
        fake.layers++;
        // D-148b3: the inline path proves both completions before endFrame; the opt-in deferred path instead requires the
        // copies to be queued and defers their proof to the next submit (or the close drain). Only these two states pass.
        const bool inlineProof=fake.releaseCalls==2*fake.layers&&host.status.gpuCompleted==2*fake.layers;
        const bool deferredQueued=fake.releaseCalls==2*fake.layers&&host.deferredGpuWait&&host.deferredPending[0]&&host.deferredPending[1]&&fake.copyCalls==2*fake.layers;
        std::string reason="unsafe stereo layer release="+std::to_string(fake.releaseCalls)+" completed="+std::to_string(host.status.gpuCompleted)+
            " deferred="+std::to_string(host.deferredGpuWait)+std::to_string(host.deferredReady)+" pending="+std::to_string(host.deferredPending[0])+std::to_string(host.deferredPending[1])+
            " copies="+std::to_string(fake.copyCalls)+" layers="+std::to_string(fake.layers);
        Require(inlineProof||deferredQueued,reason.c_str());
    }
    fake.order.push_back("end");return fake.endFrame;
}
XRAPI_ATTR XrResult XRAPI_CALL xrRequestExitSession(XrSession) {fake.requestCalls++;return fake.requestExit;}
XRAPI_ATTR XrResult XRAPI_CALL xrEndSession(XrSession) {fake.endCalls++;Require(host.sessionState==XR_SESSION_STATE_STOPPING,"EndSession before STOPPING");return fake.endSession;}
XRAPI_ATTR XrResult XRAPI_CALL xrDestroySwapchain(XrSwapchain) {fake.destroyedSwapchains++;return fake.destroySwapchain;}
XRAPI_ATTR XrResult XRAPI_CALL xrDestroySpace(XrSpace) {fake.destroyedSpaces++;return fake.destroySpace;}
XRAPI_ATTR XrResult XRAPI_CALL xrDestroySession(XrSession) {fake.destroyedSessions++;return fake.destroySession;}
XRAPI_ATTR XrResult XRAPI_CALL xrDestroyInstance(XrInstance) {fake.destroyedInstances++;return fake.destroyInstance;}

// Keep even unused Init symbols local: the fault executable must not load an OpenXR loader/runtime.
static XrResult UnexpectedSetup() {throw std::runtime_error("unexpected XR setup in fault fixture");}
XRAPI_ATTR XrResult XRAPI_CALL xrCreateInstance(const XrInstanceCreateInfo*,XrInstance*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrGetInstanceProperties(XrInstance,XrInstanceProperties*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrGetSystem(XrInstance,const XrSystemGetInfo*,XrSystemId*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrGetInstanceProcAddr(XrInstance,const char*,PFN_xrVoidFunction*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrCreateSession(XrInstance,const XrSessionCreateInfo*,XrSession*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrCreateReferenceSpace(XrSession,const XrReferenceSpaceCreateInfo*,XrSpace*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrEnumerateSwapchainFormats(XrSession,uint32_t,uint32_t*,int64_t*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrCreateSwapchain(XrSession,const XrSwapchainCreateInfo*,XrSwapchain*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrEnumerateSwapchainImages(XrSwapchain,uint32_t,uint32_t*,XrSwapchainImageBaseHeader*) {return UnexpectedSetup();}
XRAPI_ATTR XrResult XRAPI_CALL xrBeginSession(XrSession,const XrSessionBeginInfo*) {return UnexpectedSetup();}

int main(int argc,char** argv) {
    if(argc!=2)return 2;
    int passed=0;std::ostringstream cases;
    auto test=[&](const char* name,std::function<void()> action) {
        Reset();
        // D-148b3: a failing case must say which case it was; a bare message was ambiguous the moment cases grew.
        try {action();} catch(const std::exception& e) {throw std::runtime_error(std::string(name)+": "+e.what());}
        if(passed++)cases<<",";cases<<"\""<<name<<"\"";std::cout<<"PASS "<<name<<std::endl;
    };
    try {
        test("busy-apis",[]() {
            std::promise<void> held,release;auto released=release.get_future();
            std::thread worker([&](){std::lock_guard<std::mutex> lock(host.mutex);held.set_value();released.wait();});
            held.get_future().wait();auto start=Clock::now();HostStatus status;ReadStatus(&status);
            int prepare=RequestPrepare(),capture=RequestCapture(0),safe=CanReleaseSources();std::string error=ReadError();
            auto elapsed=Clock::now()-start;release.set_value();worker.join();
            Require(status.state==6&&prepare==6&&capture==6&&safe==0&&error=="native host busy","BUSY contract");
            Require(elapsed<std::chrono::milliseconds(250),"BUSY API blocked");
        });
        test("request-state-guards",[](){Require(RequestPrepare()==2,"invalid prepare");host.status.state=1;Require(RequestPrepare()==0&&host.status.state==5,"prepare");Require(RequestPrepare()==2,"duplicate prepare");Require(RequestCapture(-1)==2&&RequestCapture(11)==2,"capture range");Require(RequestCapture(0)==0&&RequestCapture(1)==2,"capture single flight");});
        test("gpu-immediate",[](){PollCase(1,0,false);});
        test("gpu-delayed",[](){PollCase(4,0,false);});
        test("gpu-timeout",[](){PollCase(100,0,true);});
        test("gpu-query-error",[](){PollCase(100,2,false);});
        // D-148b3: the opt-in deferred fence must not prove the copy inside the same submit, must prove it at the start
        // of the next submit, and must drain before the swapchain images are destroyed.
        test("deferred-fence-one-frame-behind",[](){
            host.deferredGpuWait=true;host.deferredReady=true;
            host.Submit();
            Require(host.status.gpuCompleted==0&&host.deferredPending[0]&&host.deferredPending[1]&&host.deferredProofs==0,"deferred path proved inside the submit");
            Require(fake.order==std::vector<std::string>{"copy","release","copy","release","end"},"deferred issue order");
            host.status.state=2;host.frameBegun=true;host.Submit();
            Require(host.status.gpuCompleted==2&&host.deferredProofs==2,"deferred proof missing");
            Require(fake.order==std::vector<std::string>{"copy","release","copy","release","end","proof","proof","copy","release","copy","release","end"},"deferred proof order");
            FixtureDevice();
            host.Close();Closed();
            Require(host.status.gpuCompleted==4&&host.deferredProofs==4,"close did not prove the last frame");
        });
        test("deferred-fence-drained-before-swapchain-destroy",[](){
            host.deferredGpuWait=true;host.deferredReady=true;
            // The fixture has no device of its own; the drain refuses to clear a queued copy without a provable context.
            FixtureDevice();
            host.Submit();Require(host.status.gpuCompleted==0,"proof taken early");
            host.Close();Closed();
            std::string drained="pending copy outlived its image completed="+std::to_string(host.status.gpuCompleted)+" proofs="+std::to_string(host.deferredProofs)+
                " proofCalls="+std::to_string(fake.proofCalls)+" pending="+std::to_string(host.deferredPending[0])+std::to_string(host.deferredPending[1])+" ready="+std::to_string(host.deferredReady);
            Require(host.status.gpuCompleted==2&&host.deferredProofs==2&&fake.proofCalls==2&&!host.deferredPending[0]&&!host.deferredPending[1]&&!host.deferredReady,drained.c_str());
            auto expected=std::vector<std::string>{"copy","release","copy","release","end","proof","proof"};
            Require(fake.order==expected,"drain did not precede destruction");
        });
        test("deferred-fence-rejected-after-init",[](){
            host.status.state=1;Require(SetDeferredGpuWait(1)==2&&!host.deferredGpuWait,"opt-in after init must be rejected");
            host.status.state=0;Require(SetDeferredGpuWait(2)==2&&!host.deferredGpuWait,"flag range");
            Require(SetDeferredGpuWait(1)==0&&host.deferredGpuWait,"deferred opt-in contract");
        });
        test("deferred-fence-proof-failure-is-fatal",[](){
            host.deferredGpuWait=true;host.deferredReady=true;fake.proofFail=true;
            host.Submit();host.status.state=2;host.frameBegun=true;
            Render(2);Require(host.status.state==-1&&std::string(ReadError()).find("injected deferred GPU query failure")!=std::string::npos,"failed deferred proof survived");
            Closed(false);
        });
        // D-149: early frame prep. The submit must prepare the next frame and publish it as prepared, the prepare event
        // that follows must not repeat any of that work, the opt-in must still be refused after init, and a stop seen by
        // the chained body must close the session instead of being swallowed by the extra call site.
        test("prepare-ahead-chains-next-frame",[](){
            host.prepareAhead=true;host.sessionState=XR_SESSION_STATE_FOCUSED;fake.stopEvent=false;
            host.Submit();
            Require(host.status.frames==1&&host.status.state==2&&host.frameBegun&&host.preparedAhead,"prepare-ahead did not prepare the next frame");
            Require(host.aheadPrepares==1&&host.aheadMsSum>=0.0,"prepare-ahead not accounted");
            host.Prepare();
            Require(!host.preparedAhead&&host.status.state==2&&host.frameBegun,"prepare event did not consume the prepared frame");
            Require(host.prepareCount==1&&host.bodyCount==1&&host.waitCount==1,"prepare-ahead ran the frame body twice");
        });
        test("prepare-ahead-owns-every-stop",[](){
            host.prepareAhead=true;host.sessionState=XR_SESSION_STATE_FOCUSED;fake.stopEvent=true;fake.stopAfterPolls=0;
            host.Submit();
            Require(host.status.state==3&&host.status.ended==1&&host.status.stopping==1&&!host.preparedAhead,"chained prepare swallowed the stop");
            Closed();
        });
        test("prepare-ahead-rejected-after-init",[](){
            host.status.state=1;Require(SetPrepareAhead(1)==2&&!host.prepareAhead,"opt-in after init must be rejected");
            host.status.state=0;Require(SetPrepareAhead(2)==2&&!host.prepareAhead,"flag range");
            Require(SetPrepareAhead(1)==0&&host.prepareAhead,"prepare-ahead opt-in contract");
        });
        test("stereo-copy-release-order",[](){host.Submit();Require(fake.order==std::vector<std::string>{"copy","release","copy","release","end"},"copy/release order");Require(fake.layers==1&&host.status.frames==1,"stereo frame");host.Close();Closed();});
        test("capture-pair-reuse",[](){host.frameDump=true;host.status.frames=30;host.captureId=0;host.Submit();Require(fake.saved==8&&host.captureId==-1,"capture raw identities");host.Close();Closed();});
        test("frame-dump-off-by-default",[](){host.status.frames=30;host.Submit();Require(fake.saved==0,"unrequested raw dump");host.Close();Closed();});
        test("frame-dump-opt-in-adds-pair",[](){host.frameDump=true;host.status.frames=30;host.Submit();Require(fake.saved==4,"opt-in raw dump");host.Close();Closed();});
        test("left-gpu-failure",[](){fake.failCopy=1;Render(2);Require(fake.releaseCalls==0&&fake.layers==0&&host.status.frames==0&&host.status.state==-1,"failed left submitted");Closed();});
        test("right-gpu-failure",[](){fake.failCopy=2;Render(2);Require(fake.releaseCalls==1&&fake.layers==0&&host.status.frames==0&&host.status.gpuCompleted==1,"failed right submitted");Closed();});
        test("left-gpu-timeout-no-release",[](){fake.timeoutCopy=1;Render(2);Require(fake.releaseCalls==0&&fake.layers==0&&host.status.gpuCompleted==0&&std::string(ReadError()).find("GPU completion timeout")!=std::string::npos,"timed-out GPU image released");Closed();});
        test("right-gpu-query-error-no-layer",[](){fake.errorCopy=2;Render(2);Require(fake.releaseCalls==1&&fake.layers==0&&host.status.gpuCompleted==1&&std::string(ReadError()).find("GPU query GetData")!=std::string::npos,"GPU error submitted");Closed();});
        test("swapchain-wait-timeout",[](){fake.wait=XR_TIMEOUT_EXPIRED;Render(2);Require(fake.copyCalls==0&&fake.releaseCalls==0&&fake.layers==0,"timeout image used");Closed();});
        test("acquire-failure",[](){fake.acquire=XR_ERROR_RUNTIME_FAILURE;Render(2);Require(fake.copyCalls==0&&fake.releaseCalls==0&&fake.layers==0,"failed acquire used");Closed();});
        test("release-failure",[](){fake.release=XR_ERROR_RUNTIME_FAILURE;Render(2);Require(fake.layers==0&&host.status.frames==0,"failed release submitted");Closed();});
        test("empty-nonrender-frame",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;fake.shouldRender=false;host.Prepare();Require(host.status.state==4&&!host.frameBegun&&fake.emptyFrames==1&&fake.copyCalls==0,"nonrender frame");});
        test("invalid-pose-frame",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;fake.validPose=false;host.Prepare();Require(host.status.state==4&&!host.frameBegun&&fake.emptyFrames==1,"invalid pose used");});
        test("frame-budget-watchdog",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;fake.stopEvent=false;host.start=Clock::now()-std::chrono::seconds(56);host.status.state=5;Render(1);Require(host.status.state==-1&&std::string(ReadError()).find("frame budget exceeded")!=std::string::npos,"frame watchdog");Closed(false);});
        test("orderly-idempotent-close",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;host.Close();Closed();host.Close();Require(fake.requestCalls==1&&fake.endCalls==1&&fake.destroyedInstances==1&&fake.destroyedSwapchains==2,"duplicate destruction");});
        test("empty-end-failure-cleanup",[](){fake.endFrame=XR_ERROR_RUNTIME_FAILURE;Reject([](){host.Close();},"close empty frame");Closed();Require(host.status.state==-1&&fake.destroyedInstances==1,"cleanup skipped");});
        test("stereo-end-failure-not-counted",[](){fake.endFrame=XR_ERROR_RUNTIME_FAILURE;Render(2);Require(host.status.state==-1&&host.status.frames==0&&fake.layers==1,"failed end counted");Closed();});
        test("poll-failure-cleanup",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;fake.poll=XR_ERROR_RUNTIME_FAILURE;Reject([](){host.Close();},"xrPollEvent");Require(fake.endCalls==0,"poll error allowed EndSession");Closed(false);});
        test("stopping-timeout-cleanup",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;fake.stopEvent=false;auto start=Clock::now();Reject([](){host.Close();},"STOPPING not observed");Require(Clock::now()-start<std::chrono::seconds(6)&&fake.endCalls==0,"STOPPING budget");Closed(false);});
        test("exit-request-failure-cleanup",[](){host.sessionState=XR_SESSION_STATE_FOCUSED;fake.requestExit=XR_ERROR_RUNTIME_FAILURE;Reject([](){host.Close();},"xrRequestExitSession");Require(fake.endCalls==0,"EndSession without stop");Closed(false);});
        test("end-session-failure-cleanup",[](){fake.endSession=XR_ERROR_RUNTIME_FAILURE;Reject([](){host.Close();},"xrEndSession after STOPPING");Require(host.status.ended==0,"failed end marked success");Closed(false);});
        test("child-destroy-failure-parent-cleanup",[](){fake.destroySwapchain=XR_ERROR_RUNTIME_FAILURE;fake.destroySpace=XR_ERROR_RUNTIME_FAILURE;Reject([](){host.Close();},"destroy swapchain");Closed();Require(fake.destroyedSwapchains==2&&fake.destroyedSpaces==1&&fake.destroyedSessions==1&&fake.destroyedInstances==1,"remaining destruction skipped");});
        test("session-loss-destroy-only",[](){host.sessionState=XR_SESSION_STATE_LOSS_PENDING;host.Close();Closed(false);Require(fake.requestCalls==0&&fake.endCalls==0&&host.status.ended==0,"loss marked orderly");});
        test("retained-source-close-retry",[](){
            D3D_FEATURE_LEVEL level;Hr(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&host.device,&level,&host.context),"fixture device");
            D3D11_TEXTURE2D_DESC d{};d.Width=4;d.Height=4;d.MipLevels=1;d.ArraySize=1;d.Format=DXGI_FORMAT_R8G8B8A8_UNORM;d.SampleDesc.Count=1;
            Hr(host.device->CreateTexture2D(&d,nullptr,&host.source[0]),"fixture source");
            fake.destroySwapchain=fake.destroySpace=fake.destroySession=fake.destroyInstance=XR_ERROR_RUNTIME_FAILURE;Render(3);
            Require(host.status.state==-1&&CanReleaseSources()==0&&host.source[0]&&host.device,"live ownership discarded");
            fake.destroySwapchain=fake.destroySpace=fake.destroySession=fake.destroyInstance=XR_SUCCESS;Render(3);Closed();Require(host.status.state==3&&!host.device&&!host.context,"retry did not free resources");
        });
        test("headset-opt-in-contract",[](){
            Require(RuntimeNameAllowed("Meta XR Simulator",false)&&!RuntimeNameAllowed("VirtualDesktopXR",false)&&RuntimeNameAllowed("VirtualDesktopXR",true)&&!RuntimeNameAllowed("VirtualDesktopXR",false)&&!RuntimeNameAllowed("",true),"runtime gate");
            Require(ConfigureHeadset(600)==2&&!host.headsetAllowed,"opt-in after init must be rejected");
            host.status.state=0;
            Require(ConfigureHeadset(-1)==2&&ConfigureHeadset(59)==2&&ConfigureHeadset(901)==2&&!host.headsetAllowed,"budget range");
            Require(ConfigureHeadset(600)==0&&host.headsetAllowed&&host.budgetSeconds==600,"opt-in contract");
        });
        // D-145: the Virtual-Desktop-only rule is gone, but still only after the explicit headset opt-in.
        test("headset-any-runtime-after-opt-in",[](){
            Require(RuntimeNameAllowed("Meta XR Simulator",false)&&RuntimeNameAllowed("Meta XR Simulator",true),"simulator always allowed");
            Require(!RuntimeNameAllowed("VirtualDesktopXR",false)&&!RuntimeNameAllowed("Oculus",false)&&!RuntimeNameAllowed("SteamVR",false)&&!RuntimeNameAllowed("",true),"no runtime without opt-in");
            Require(RuntimeNameAllowed("VirtualDesktopXR",true)&&RuntimeNameAllowed("Oculus",true)&&RuntimeNameAllowed("SteamVR",true),"any real runtime with opt-in");
        });
        // D-145: budget 0 = no wall-time limit, so an arbitrarily old session start must not close the session.
        test("headset-unlimited-budget-keeps-running",[](){
            host.headsetAllowed=true;host.budgetSeconds=0;host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;
            host.start=Clock::now()-std::chrono::seconds(100000);host.Prepare();
            Require(host.status.state==2&&host.frameBegun&&fake.requestCalls==0,"unlimited headset session closed by a wall-time budget");
        });
        test("headset-budget-orderly-close",[](){
            host.headsetAllowed=true;host.budgetSeconds=60;host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopAfterPolls=1;
            host.start=Clock::now()-std::chrono::seconds(61);host.Prepare();
            Require(host.status.state==3&&host.status.ended==1&&host.status.stopping==1&&fake.requestCalls==1&&fake.endCalls==1,"headset budget must end orderly, not throw");Closed();
        });
        test("headset-runs-past-simulator-budget",[](){
            host.headsetAllowed=true;host.budgetSeconds=900;host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;
            host.start=Clock::now()-std::chrono::seconds(100);host.Prepare();
            Require(host.status.state==2&&host.frameBegun&&fake.requestCalls==0,"headset session killed by the 55 s simulator budget");
        });
        test("headset-no-hold-close",[](){
            host.headsetAllowed=true;host.target=120;host.status.frames=130;host.holdStart=Clock::now()-std::chrono::seconds(10);host.Submit();
            Require(host.status.state==1&&host.running&&host.status.holdSeconds>=3&&fake.requestCalls==0,"headset session closed by the simulator hold");host.Close();Closed();
        });
        test("hands-sample-published",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;host.handsCreated=true;
            host.Prepare();
            Require(host.status.state==2&&fake.handsSyncCalls==1&&host.hands.seq==1&&host.hands.hands[1].active==1&&host.hands.hands[0].active==0&&
                host.hands.hands[1].trigger==0.75f&&host.hands.hands[1].face==1,"hand sample not published");
            HandsStatus read{};Require(ReadHands(&read)==0&&read.seq==1&&read.hands[1].active==1,"ReadHands contract");
        });
        test("hands-sync-failure-isolated",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;host.handsCreated=true;fake.handsSyncFail=true;
            host.Prepare();
            HandsStatus read{};
            Require(host.status.state==2&&host.frameBegun&&!host.handsCreated&&host.hands.seq==-1&&host.hands.hands[0].active==0&&host.hands.hands[1].active==0,"hand failure leaked into video path");
            Require(ReadHands(&read)==1&&read.seq==-1&&std::string(ReadHandsError()).find("injected xrSyncActions failure")!=std::string::npos,"hand failure hidden");
            host.frameBegun=false;host.Prepare();Require(fake.handsSyncCalls==1,"disabled hands kept polling");
        });
        test("hands-not-requested-unavailable",[](){
            HandsStatus read{};Require(ReadHands(&read)==1&&read.hands[0].active==0&&read.hands[1].active==0&&EnableHands(1)==2,"disabled hands contract");
            host.status.state=0;Require(EnableHands(1)==0&&host.handsRequested&&EnableHands(0)==0&&!host.handsRequested&&EnableHands(2)==2,"EnableHands contract");
        });
        test("hands-haptic-queued-and-applied-once",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;host.handsCreated=true;
            Require(RequestHaptic(0,0.4f,60)==0&&host.hapticPending[0]&&!host.hapticPending[1],"haptic not queued for one hand");
            host.Prepare();
            Require(fake.hapticCalls==1&&fake.lastHapticHand==0&&fake.lastHapticAmplitude==0.4f&&fake.lastHapticNs==60000000LL&&!host.hapticPending[0]&&host.hapticApplied==1,"haptic not applied to the requested hand once");
            host.frameBegun=false;host.Prepare();Require(fake.hapticCalls==1,"haptic repeated without a new request");
        });
        test("hands-haptic-failure-isolated",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.frameBegun=false;fake.stopEvent=false;host.handsCreated=true;fake.hapticFail=true;
            Require(RequestHaptic(1,0.4f,60)==0,"haptic not queued");
            host.Prepare();
            Require(host.status.state==2&&host.frameBegun&&host.handsCreated&&host.hapticDisabled&&host.hands.hands[1].active==1,"haptic failure leaked into hand or video path");
            Require(std::string(ReadHapticError()).find("injected xrApplyHapticFeedback failure")!=std::string::npos&&RequestHaptic(1,0.4f,60)==1,"haptic failure hidden or still accepted");
            host.frameBegun=false;host.Prepare();Require(fake.hapticCalls==1,"disabled haptics kept firing");
        });
        test("hands-haptic-argument-and-availability-contract",[](){
            Require(RequestHaptic(0,0.4f,60)==1,"haptic accepted without hands");
            host.handsCreated=true;
            Require(RequestHaptic(2,0.4f,60)==2&&RequestHaptic(-1,0.4f,60)==2&&RequestHaptic(0,0.0f,60)==2&&RequestHaptic(0,1.5f,60)==2&&RequestHaptic(0,0.4f,0)==2&&RequestHaptic(0,0.4f,501)==2&&RequestHaptic(0,NAN,60)==2,"bad haptic arguments accepted");
            Require(fake.hapticCalls==0&&!host.hapticPending[0]&&!host.hapticPending[1],"rejected haptic left state behind");
        });
        test("timing-window-resets-and-never-fails",[](){
            host.lastSubmitEnd=Clock::now();
            for(int i=0;i<119;i++)host.RecordTiming(Clock::now());
            Require(host.submitCount==119&&host.cycleCount==119,"timing window counted wrong");
            // D-148b: the cycle decomposition sums are part of the same window and must reset with it.
            host.prepBodyMsSum=1.5;host.handsMsSum=1.0;host.pumpMsSum=0.5;host.prepareGapMsSum=2.0;host.logMsSum=0.25;host.prepareCount=7;
            // D-149: so are the CPU split and the prepare-ahead counters; a stale sum would silently price the wrong window.
            host.prepCpuMsSum=1.0;host.prepMainCpuMsSum=0.5;host.prepRenderCpuMsSum=0.75;host.submitCpuMsSum=0.2;host.gapCpuMsSum=0.3;
            host.bodyCount=5;host.aheadPrepares=3;host.aheadMsSum=0.4;
            host.RecordTiming(Clock::now());
            Require(host.submitCount==0&&host.cycleCount==0&&host.waitCount==0&&host.submitMsSum==0.0&&host.cycleMsSum==0.0&&
                host.copyIssueMsSum==0.0&&host.copyFenceMsSum==0.0&&host.endFrameMsSum==0.0&&
                host.prepBodyMsSum==0.0&&host.handsMsSum==0.0&&host.pumpMsSum==0.0&&host.prepareGapMsSum==0.0&&host.logMsSum==0.0&&
                host.prepareCount==0&&host.prepCpuMsSum==0.0&&host.prepMainCpuMsSum==0.0&&host.prepRenderCpuMsSum==0.0&&
                host.submitCpuMsSum==0.0&&host.gapCpuMsSum==0.0&&host.bodyCount==0&&host.aheadPrepares==0&&host.aheadMsSum==0.0,
                "timing window not reset");
            Require(host.status.state==2,"timing touched the frame state");
        });
        test("hands-destroyed-on-close",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.handsCreated=true;host.Close();
            Require(fake.handsDestroyCalls==1&&!host.handsCreated,"hands not destroyed");Closed();
        });
        test("hands-destroy-failure-parent-cleanup",[](){
            host.sessionState=XR_SESSION_STATE_FOCUSED;host.handsCreated=true;fake.handsDestroyFail=true;
            Reject([](){host.Close();},"injected hands destroy failure");
            Require(fake.destroyedInstances==1&&fake.destroyedSessions==1,"hand cleanup failure skipped parent destruction");Closed();
        });        std::ofstream receipt(argv[1]);receipt<<"{\"verdict\":\"PASS\",\"provenance\":\"TEST_ONLY_XR_GPU_RESPONSES\",\"runtimeStarted\":false,\"passed\":"<<passed<<",\"cases\":["<<cases.str()<<"]}";
        Require(static_cast<bool>(receipt),"fixture receipt write");std::cout<<"RESULT "<<passed<<" native host fault fixtures passed; not runtime/game acceptance"<<std::endl;return 0;
    } catch(const std::exception& e) {std::cerr<<"FAIL "<<e.what()<<std::endl;return 2;}
}