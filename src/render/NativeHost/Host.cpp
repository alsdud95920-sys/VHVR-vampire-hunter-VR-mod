#include "Host.h"
Host host;
void Check(XrResult result,const char* call) {
    if(XR_FAILED(result)) throw std::runtime_error(std::string(call)+" XrResult="+std::to_string(result));
}
void Host::Log(const std::string& phase) {
    log<<GetCurrentProcessId()<<" "<<GetCurrentThreadId()<<" "<<phase<<std::endl;
}
// D-148b: a per-frame Log() flushes to disk on the render thread, so its cost must be visible in the timing window
// rather than hiding in the cycle. Measurement only; it can never fail a frame.
void Host::TimedLog(const std::string& phase) {
    const Clock::time_point at=Clock::now();
    Log(phase);
    logMsSum+=Ms(Clock::now()-at);
}
// D-149: CPU time of the game process and of the two threads that decide a frame (the managed main thread and the
// render thread that runs the XR calls). GetProcessTimes/GetThreadTimes are cheap kernel reads - no sampling thread and
// no driver - so reading them twice per frame does not change what is measured. A handle that could not be opened
// leaves cpuSampled false, and the timing line then reports the CPU fields as -1 instead of pretending the CPU was idle.
static double FileTimeMs(const FILETIME& value) {
    return (static_cast<double>(value.dwHighDateTime)*4294967296.0+static_cast<double>(value.dwLowDateTime))/10000.0;
}
static bool ThreadCpuMs(HANDLE handle,double& out) {
    if(!handle) return false;
    FILETIME created,exited,kernel,user;
    if(!GetThreadTimes(handle,&created,&exited,&kernel,&user)) return false;
    out=FileTimeMs(kernel)+FileTimeMs(user);return true;
}
struct CpuPoint { double process=0, main=0, render=0; bool sampled=false; };
static CpuPoint SampleCpu(Host& target) {
    CpuPoint point;
    FILETIME created,exited,kernel,user;
    point.sampled=GetProcessTimes(GetCurrentProcess(),&created,&exited,&kernel,&user)!=0;
    if(point.sampled) point.process=FileTimeMs(kernel)+FileTimeMs(user);
    point.sampled=point.sampled&&ThreadCpuMs(target.mainThreadHandle,point.main)&&ThreadCpuMs(target.renderThreadHandle,point.render);
    return point;
}
static double CpuDelta(double now,double before) {return now>=before?now-before:0.0;}
void Host::Pump() {
    XrEventDataBuffer event{XR_TYPE_EVENT_DATA_BUFFER}; XrResult result;
    while((result=xrPollEvent(instance,&event))==XR_SUCCESS) {
        if(event.type==XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED) {
            auto changed=reinterpret_cast<XrEventDataSessionStateChanged*>(&event);
            if(changed->session==session) {sessionState=changed->state; Log("session state="+std::to_string(sessionState));}
        }
        event={XR_TYPE_EVENT_DATA_BUFFER};
    }
    if(result!=XR_EVENT_UNAVAILABLE) Check(result,"xrPollEvent");
}
void Host::Init() {
    status.renderThread=GetCurrentThreadId(); start=Clock::now();
    log.open(output+".native.log"); Log("Init same-device");
    // D-149: the two query-only thread handles the CPU split needs. A thread that cannot be opened (or a fixture that
    // never names a main thread) simply turns the CPU fields into -1 in the timing line; nothing else changes.
    mainThreadHandle=status.mainThread?OpenThread(THREAD_QUERY_LIMITED_INFORMATION,FALSE,status.mainThread):nullptr;
    renderThreadHandle=OpenThread(THREAD_QUERY_LIMITED_INFORMATION,FALSE,status.renderThread);
    cpuSampled=mainThreadHandle!=nullptr&&renderThreadHandle!=nullptr;
    Log("cpu sampling "+(cpuSampled?std::string("available"):std::string("unavailable"))+" mainThread="+std::to_string(status.mainThread)+" renderThread="+std::to_string(status.renderThread));
    if(prepareAhead) Log("prepare-ahead enabled: the submit prepares the next XR frame (D-149)");
    if(status.state!=0) throw std::runtime_error("duplicate init");
    // D-145: when no runtime json was handed in (managed passes "" for auto-detect), use the machine's active OpenXR
    // runtime - exactly the value the loader would use on its own. No registry value leaves the loader default in place.
    char runtimeJson[1024]{}; DWORD runtimeLength=GetEnvironmentVariableA("XR_RUNTIME_JSON",runtimeJson,sizeof(runtimeJson));
    if(runtimeLength==0||!runtimeJson[0]) {
        DWORD size=sizeof(runtimeJson);
        if(RegGetValueA(HKEY_LOCAL_MACHINE,"SOFTWARE\\Khronos\\OpenXR\\1","ActiveRuntime",RRF_RT_REG_SZ,nullptr,runtimeJson,&size)==ERROR_SUCCESS&&runtimeJson[0]) {
            SetEnvironmentVariableA("XR_RUNTIME_JSON",runtimeJson); Log("runtime json from registry="+std::string(runtimeJson));
        } else Log("no active OpenXR runtime registered; OpenXR loader default applies");
    } else Log("runtime json from environment="+std::string(runtimeJson));
    source[0]->GetDevice(&device);
    ComPtr<ID3D11Device> right; source[1]->GetDevice(&right);
    ComPtr<IUnknown> a,b; Hr(right.As(&a),"right identity"); Hr(device.As(&b),"device identity");
    if(a.Get()!=b.Get()) throw std::runtime_error("camera devices differ");
    device->GetImmediateContext(&context);
    if(context->GetType()!=D3D11_DEVICE_CONTEXT_IMMEDIATE) throw std::runtime_error("not immediate context");
    D3D11_QUERY_DESC queryDesc{D3D11_QUERY_EVENT,0}; Hr(device->CreateQuery(&queryDesc,&query),"GPU event query");
    // D-148b3: the deferred path needs one event query per eye because frame N's query is still being polled while frame
    // N+1 is issued. A failed creation leaves the deferred path off (the default inline proof keeps working).
    deferredPending[0]=deferredPending[1]=false;deferredReady=false;
    if(deferredGpuWait) {
        for(int eye=0;eye<2;eye++) Hr(device->CreateQuery(&queryDesc,&deferredQuery[eye]),"deferred GPU event query");
        deferredReady=true;Log("deferred submit fence enabled: both eye copies are proven one frame later (D-148b3)");
    }
    const char* extensions[]={XR_KHR_D3D11_ENABLE_EXTENSION_NAME};
    XrInstanceCreateInfo create{XR_TYPE_INSTANCE_CREATE_INFO};
    strcpy_s(create.applicationInfo.applicationName,"VampireHuntersVR game host");
    strcpy_s(create.applicationInfo.engineName,"Unity game-device bridge");
    create.applicationInfo.apiVersion=XR_MAKE_VERSION(1,0,34); create.enabledExtensionCount=1; create.enabledExtensionNames=extensions;
    Check(xrCreateInstance(&create,&instance),"xrCreateInstance");
    XrInstanceProperties properties{XR_TYPE_INSTANCE_PROPERTIES}; Check(xrGetInstanceProperties(instance,&properties),"xrGetInstanceProperties");
    Log("runtime="+std::string(properties.runtimeName));
    if(!RuntimeNameAllowed(std::string(properties.runtimeName),headsetAllowed)) throw std::runtime_error("runtime not allowed: the simulator is always available, any real runtime needs the explicit headset opt-in");
    XrSystemGetInfo getSystem{XR_TYPE_SYSTEM_GET_INFO}; getSystem.formFactor=XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    XrSystemId system; Check(xrGetSystem(instance,&getSystem,&system),"xrGetSystem");
    PFN_xrGetD3D11GraphicsRequirementsKHR requirementsFunction=nullptr;
    Check(xrGetInstanceProcAddr(instance,"xrGetD3D11GraphicsRequirementsKHR",reinterpret_cast<PFN_xrVoidFunction*>(&requirementsFunction)),"requirements entry");
    XrGraphicsRequirementsD3D11KHR requirements{XR_TYPE_GRAPHICS_REQUIREMENTS_D3D11_KHR};
    Check(requirementsFunction(instance,system,&requirements),"D3D11 requirements");
    ComPtr<IDXGIDevice> dxgi; Hr(device.As(&dxgi),"DXGI device"); ComPtr<IDXGIAdapter> adapter; Hr(dxgi->GetAdapter(&adapter),"adapter");
    DXGI_ADAPTER_DESC desc{}; Hr(adapter->GetDesc(&desc),"adapter desc");
    if(memcmp(&desc.AdapterLuid,&requirements.adapterLuid,sizeof(LUID))!=0||device->GetFeatureLevel()<requirements.minFeatureLevel)
        throw std::runtime_error("Unity device does not meet runtime adapter/feature requirements");
    status.adapterMatched=1; Log("adapter matches; device="+std::to_string(reinterpret_cast<uintptr_t>(device.Get()))+
        " mainThread="+std::to_string(status.mainThread)+" renderThread="+std::to_string(status.renderThread));
    XrGraphicsBindingD3D11KHR binding{XR_TYPE_GRAPHICS_BINDING_D3D11_KHR}; binding.device=device.Get();
    XrSessionCreateInfo sessionCreate{XR_TYPE_SESSION_CREATE_INFO}; sessionCreate.next=&binding;sessionCreate.systemId=system;
    Check(xrCreateSession(instance,&sessionCreate,&session),"xrCreateSession game device");
    XrReferenceSpaceCreateInfo ref{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};ref.referenceSpaceType=XR_REFERENCE_SPACE_TYPE_LOCAL;ref.poseInReferenceSpace.orientation.w=1;
    Check(xrCreateReferenceSpace(session,&ref,&space),"xrCreateReferenceSpace");
    // Optional per-hand input. Isolated: any failure leaves the stereo path untouched and is reported, never hidden.
    if(handsRequested) {
        try {HandsInit(instance,session);handsCreated=true;Log("hands actions attached");}
        catch(const std::exception& e) {handsError=e.what();handsCreated=false;Log("HANDS DISABLED "+handsError);}
    }
    uint32_t count=0; Check(xrEnumerateSwapchainFormats(session,0,&count,nullptr),"format count"); std::vector<int64_t> formats(count);
    Check(xrEnumerateSwapchainFormats(session,count,&count,formats.data()),"formats");
    D3D11_TEXTURE2D_DESC src{};source[0]->GetDesc(&src);
    bool bgra=src.Format==DXGI_FORMAT_B8G8R8A8_TYPELESS||src.Format==DXGI_FORMAT_B8G8R8A8_UNORM||src.Format==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    int64_t format=bgra?DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
    if(std::find(formats.begin(),formats.end(),format)==formats.end()) throw std::runtime_error("runtime lacks matching sRGB camera format; no silent colorspace fallback");
    status.width=static_cast<int>(src.Width);status.height=static_cast<int>(src.Height);Log("copy format="+std::to_string(format));
    for(int eye=0;eye<2;eye++) {
        XrSwapchainCreateInfo sc{XR_TYPE_SWAPCHAIN_CREATE_INFO};sc.usageFlags=XR_SWAPCHAIN_USAGE_TRANSFER_DST_BIT|XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT|XR_SWAPCHAIN_USAGE_SAMPLED_BIT;
        sc.format=format;sc.sampleCount=1;sc.width=src.Width;sc.height=src.Height;sc.faceCount=1;sc.arraySize=1;sc.mipCount=1;
        Check(xrCreateSwapchain(session,&sc,&swapchains[eye]),"xrCreateSwapchain");
        Check(xrEnumerateSwapchainImages(swapchains[eye],0,&count,nullptr),"image count"); images[eye].resize(count);
        for(auto& image:images[eye]) image={XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR};
        Check(xrEnumerateSwapchainImages(swapchains[eye],count,&count,reinterpret_cast<XrSwapchainImageBaseHeader*>(images[eye].data())),"images");
        views[eye]={XR_TYPE_VIEW};
    }
    auto ready=Clock::now(); while(sessionState!=XR_SESSION_STATE_READY && Clock::now()-ready<std::chrono::seconds(10)) {Pump();Sleep(5);}
    if(sessionState!=XR_SESSION_STATE_READY) throw std::runtime_error("READY timeout");
    XrSessionBeginInfo begin{XR_TYPE_SESSION_BEGIN_INFO};begin.primaryViewConfigurationType=XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
    Check(xrBeginSession(session,&begin),"xrBeginSession");running=true;status.state=1;Log("session began");
}
void Host::Prepare() {
    // D-148b: entry stamp. Together with lastSubmitEnd it bounds the cycle gap (render-thread turnaround plus the
    // prepare dispatch) that the old line left unmeasured between submitMs and cycleMs. D-149 prices the same window in
    // CPU time, which is what tells a busy render thread apart from a frame that is waiting on the GPU.
    const Clock::time_point entryAt=Clock::now();
    if(lastSubmitEnd.time_since_epoch().count()!=0) {
        prepareGapMsSum+=Ms(entryAt-lastSubmitEnd);prepareCount++;
        if(cpuSampled) {const CpuPoint gap=SampleCpu(*this);gapCpuMsSum+=CpuDelta(gap.process,lastSubmitCpu);cpuSamples++;}
    }
    // D-149: with prepare-ahead the submit already did this frame's work, the pump included, so this event only clears
    // the marker. status.state stays 2 for exactly the reason it would have been 2 had the body run here.
    if(preparedAhead) {preparedAhead=false;status.state=2;return;}
    PrepareBody();
}
void Host::PrepareBody() {
    const Clock::time_point pumpStart=Clock::now();
    Pump();
    pumpMsSum+=Ms(Clock::now()-pumpStart);
    if(sessionState==XR_SESSION_STATE_STOPPING||sessionState==XR_SESSION_STATE_LOSS_PENDING||sessionState==XR_SESSION_STATE_EXITING) {Close();return;}
    if(headsetAllowed) {
        // D-087: a bounded headset sitting ends orderly (EndSession after STOPPING), never by a throw.
        // D-145: budgetSeconds 0 removes the wall-time limit; the session then ends only on a runtime event or the watchdog.
        if(budgetSeconds>0&&Clock::now()-start>std::chrono::seconds(budgetSeconds)) {Log("headset session budget reached; orderly close");Close();return;}
    } else if(Clock::now()-start>std::chrono::seconds(55)) throw std::runtime_error("host frame budget exceeded");
    TimedLog("waitFrame begin");
    XrFrameWaitInfo wait{XR_TYPE_FRAME_WAIT_INFO};frame={XR_TYPE_FRAME_STATE};const Clock::time_point waitStart=Clock::now();Check(xrWaitFrame(session,&wait,&frame),"xrWaitFrame");waitMsSum+=Ms(Clock::now()-waitStart);waitCount++;displayPeriodMs=static_cast<double>(frame.predictedDisplayPeriod)/1e6;
    TimedLog("waitFrame returned");
    // D-148b: the Prepare body (begin + locate + hands) is what used to hide between waitMs and prepToSubmitMs.
    const Clock::time_point bodyStart=Clock::now();bodyCount++;
    XrFrameBeginInfo begin{XR_TYPE_FRAME_BEGIN_INFO};Check(xrBeginFrame(session,&begin),"xrBeginFrame");frameBegun=true;
    XrViewLocateInfo locate{XR_TYPE_VIEW_LOCATE_INFO};locate.viewConfigurationType=XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;locate.displayTime=frame.predictedDisplayTime;locate.space=space;
    XrViewState state{XR_TYPE_VIEW_STATE};uint32_t count=0;Check(xrLocateViews(session,&locate,&state,2,&count,views),"xrLocateViews");
    if(!frame.shouldRender||count!=2||(state.viewStateFlags&3)!=3) {
        EndEmptyFrame("empty frame");status.state=4;return;
    }
    for(int eye=0;eye<2;eye++) {auto& v=views[eye];auto& e=status.eyes[eye];
        // D-186-FovScale: multiply the four angles by the per-axis scale (1.0 = pass-through, the shipped
        // value) before writing them into HostStatus. fovScaleX scales the horizontal pair
        // (angleLeft, angleRight) and fovScaleY scales the vertical pair (angleUp, angleDown). The managed
        // side reads them back from HostStatus to build its projection matrix (StereoMath.Projection in
        // OpenXrHost.cs), and the same scaled angles are used for the compositor submit below. One-sided
        // narrowing (only one of the two sides scaled) would show black bars at the edges, so both ends
        // of the pipeline see the same scaled frustum on both axes.
        const float sx=fovScaleX, sy=fovScaleY;
        e={v.pose.position.x,v.pose.position.y,v.pose.position.z,
        v.pose.orientation.x,v.pose.orientation.y,v.pose.orientation.z,v.pose.orientation.w,
        v.fov.angleLeft*sx,v.fov.angleRight*sx,v.fov.angleUp*sy,v.fov.angleDown*sy};}
    if(handsCreated) {
        const Clock::time_point handsStart=Clock::now();
        try {
            HandsStatus sample{};HandsSync(session,space,frame.predictedDisplayTime,sample);
            sample.seq=hands.seq+1;sample.frames=status.frames;hands=sample;
            // D-103: apply queued vibration on this thread, one controller at a time; the result never reaches the hand sample.
            for(int hand=0;hand<2;hand++) {
                if(!hapticPending[hand]) continue;
                hapticPending[hand]=false;
                if(hapticDisabled) continue;
                try {if(HandsHaptic(session,hand,hapticAmplitude[hand],hapticDurationNs[hand])) hapticApplied++;else hapticSkipped++;}
                catch(const std::exception& e) {hapticError=e.what();hapticDisabled=true;Log("HAPTIC DISABLED "+hapticError);}
            }
            hands.reserved=hapticApplied;
        } catch(const std::exception& e) {
            handsError=e.what();handsCreated=false;hands=HandsStatus{};hands.seq=-1;Log("HANDS SYNC DISABLED "+handsError);
        }
        handsMsSum+=Ms(Clock::now()-handsStart);
    }
    // D-149: the CPU reading is taken just before the stamp, so the prepToSubmit window (this instant -> submit) is
    // exactly the engine frame that follows the prepare, chained or not.
    const CpuPoint prepared=SampleCpu(*this);
    preparedAt=Clock::now();prepBodyMsSum+=Ms(preparedAt-bodyStart);status.state=2;
    prepStartCpu=prepared.process;prepStartMainCpu=prepared.main;prepStartRenderCpu=prepared.render;
    if(prepared.sampled) cpuSamples++;
}
void Host::Submit() {
    if(!frameBegun||status.state!=2) throw std::runtime_error("submit without prepared frame");
    const Clock::time_point submitStart=Clock::now();prepToSubmitMsSum+=Ms(submitStart-preparedAt);
    // D-149: the CPU consumed from the end of the prepare body to this instant is the engine frame that rendered the
    // eyes. Comparing it with prepToSubmitMs is the whole CPU-vs-GPU question in one number.
    if(cpuSampled) {const CpuPoint at=SampleCpu(*this);if(at.sampled){submitStartCpu=at.process;submitStartMainCpu=at.main;submitStartRenderCpu=at.render;}}
    XrCompositionLayerProjectionView layerViews[2]{};
    double copyIssueMs=0,copyFenceMs=0; // D-148: both eyes, this frame only
    // D-148b3: take frame N-1's deferred proof first. One engine frame of slack has passed, so this wait is expected to
    // be near zero; if the GPU really fell behind it still blocks here (same backpressure as the inline path).
    const bool deferred=deferredGpuWait&&deferredReady;
    if(deferred) for(int eye=0;eye<2;eye++) if(deferredPending[eye]) {
        double fenceMs=0;WaitCopyCompletion(device.Get(),context.Get(),deferredQuery[eye].Get(),&fenceMs);
        deferredPending[eye]=false;deferredProofs++;status.gpuCompleted++;copyFenceMs+=fenceMs;
    }
    for(int eye=0;eye<2;eye++) {
        uint32_t index;XrSwapchainImageAcquireInfo acquire{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
        Check(xrAcquireSwapchainImage(swapchains[eye],&acquire,&index),"acquire");
        XrSwapchainImageWaitInfo wait{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};wait.timeout=200000000;
        XrResult waited=xrWaitSwapchainImage(swapchains[eye],&wait);
        if(waited!=XR_SUCCESS) throw std::runtime_error("swapchain wait not successful");
        if(index>=images[eye].size()) throw std::runtime_error("image index out of range");
        double issueMs=0,fenceMs=0;
        if(deferred) {
            // Release right after the copy is queued (standard OpenXR ordering); the proof is deferred to the next frame.
            CopyAndFlush(device.Get(),context.Get(),deferredQuery[eye].Get(),images[eye][index].texture,source[eye].Get(),true,&issueMs);
            deferredPending[eye]=true;
        } else {
            CopyAndWait(device.Get(),context.Get(),query.Get(),images[eye][index].texture,source[eye].Get(),true,&issueMs,&fenceMs);status.gpuCompleted++;
        }
        copyIssueMs+=issueMs;copyFenceMs+=fenceMs;
        auto savePair=[&](const std::string& prefix) {
            SaveTexture(device.Get(),context.Get(),images[eye][index].texture,prefix+".eye"+std::to_string(eye)+".raw");
            SaveTexture(device.Get(),context.Get(),source[eye].Get(),prefix+".source"+std::to_string(eye)+".raw");
        };
        if(captureId>=0) savePair(output+".capture"+std::to_string(captureId));
        if(frameDump&&status.frames==30) savePair(output); // D-147: opt-in only; requested captures are unaffected
        XrSwapchainImageReleaseInfo release{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};Check(xrReleaseSwapchainImage(swapchains[eye],&release),"release after GPU completion");
        auto& v=layerViews[eye];v.type=XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;v.pose=views[eye].pose;
        // D-186-FovScale: build a per-axis scaled XrFovf from status.eyes[eye] (which PrepareBody already
        // wrote with fovScaleX applied to Left/Right and fovScaleY applied to Up/Down). 1.0 = pass-through
        // (status.eyes == views[eye].fov on both axes). Reading the same source the managed side used for
        // the projection keeps the two sides of the pipeline in lockstep so the compositor never shows a
        // frustum the eye picture was not rendered for.
        v.fov.angleLeft=status.eyes[eye].left;v.fov.angleRight=status.eyes[eye].right;
        v.fov.angleUp=status.eyes[eye].up;v.fov.angleDown=status.eyes[eye].down;
        v.subImage.swapchain=swapchains[eye];v.subImage.imageRect.extent={status.width,status.height};
    }
    XrCompositionLayerProjection projection{XR_TYPE_COMPOSITION_LAYER_PROJECTION};projection.space=space;projection.viewCount=2;projection.views=layerViews;
    const XrCompositionLayerBaseHeader* layer=reinterpret_cast<const XrCompositionLayerBaseHeader*>(&projection);
    XrFrameEndInfo end{XR_TYPE_FRAME_END_INFO};end.displayTime=frame.predictedDisplayTime;end.environmentBlendMode=XR_ENVIRONMENT_BLEND_MODE_OPAQUE;end.layerCount=1;end.layers=&layer;
    const Clock::time_point endFrameStart=Clock::now();
    Check(xrEndFrame(session,&end),"xrEndFrame game cameras");frameBegun=false;status.frames++;status.state=1;
    endFrameMsSum+=Ms(Clock::now()-endFrameStart);copyIssueMsSum+=copyIssueMs;copyFenceMsSum+=copyFenceMs;
    RecordTiming(submitStart);
    if(captureId>=0) {TimedLog("capture id="+std::to_string(captureId)+" submitted="+std::to_string(status.frames));captureId=-1;}
    TimedLog("submitted="+std::to_string(status.frames)+" gpuCompleted="+std::to_string(status.gpuCompleted));
    if(status.frames==target) holdStart=Clock::now();
    if(status.frames>=target) {status.holdSeconds=std::chrono::duration<double>(Clock::now()-holdStart).count();if(status.holdSeconds>=3&&!headsetAllowed) Close();} // headset sitting ends only by its wall-time budget
    // D-149: prepare the next XR frame here, so the engine's very next frame can render the eyes instead of spending a
    // frame on a prepare-only event. The body owns every exit (stop, budget, empty frame), so a chain can only ever
    // publish the same status the prepare event would have published; anything else leaves preparedAhead false and the
    // prepare event simply does the work as usual.
    if(prepareAhead&&running&&status.state==1&&
        sessionState!=XR_SESSION_STATE_STOPPING&&sessionState!=XR_SESSION_STATE_LOSS_PENDING&&sessionState!=XR_SESSION_STATE_EXITING) {
        const Clock::time_point aheadStart=Clock::now();
        PrepareBody();
        aheadMsSum+=Ms(Clock::now()-aheadStart);aheadPrepares++;
        preparedAhead=status.state==2;
        if(!preparedAhead) Log("prepare-ahead did not produce a prepared frame (state="+std::to_string(status.state)+"); the next prepare event does the work");
    }
}
// D-104: measurement only. One log line per 120 submitted frames; nothing here can fail a frame.
void Host::RecordTiming(Clock::time_point submitStart) {
    const Clock::time_point now=Clock::now();
    submitMsSum+=Ms(now-submitStart);submitCount++;
    if(lastSubmitEnd.time_since_epoch().count()!=0) {cycleMsSum+=Ms(now-lastSubmitEnd);cycleCount++;}
    lastSubmitEnd=now;
    // D-149: the CPU split of the same cycle. prepCpuMs is the process CPU consumed while the engine frame ran (the two
    // named threads are reported separately), submitCpuMs is our own submit work, and gapCpuMs is the post-submit
    // turnaround. All three are sub-spans of the wall-clock fields, never part of the cycle identity.
    if(cpuSampled) {
        const CpuPoint end=SampleCpu(*this);
        if(end.sampled) {
            prepCpuMsSum+=CpuDelta(submitStartCpu,prepStartCpu);
            prepMainCpuMsSum+=CpuDelta(submitStartMainCpu,prepStartMainCpu);
            prepRenderCpuMsSum+=CpuDelta(submitStartRenderCpu,prepStartRenderCpu);
            submitCpuMsSum+=CpuDelta(end.process,submitStartCpu);
            lastSubmitCpu=end.process;lastSubmitMainCpu=end.main;lastSubmitRenderCpu=end.render;
            cpuSamples++;
        }
    }
    if(submitCount<120) return;
    // D-148: copyMs/fenceMs/endFrameMs split what submitMs contains. fenceMs is the part that proves Unity's queued GPU
    // work is on the critical path; endFrameMs is the runtime call (Virtual Desktop encode/transport).
    // D-148b: the new fields close the cycle identity cycleMs = gapMs + pumpMs + waitMs + prepBodyMs +
    // prepToSubmitMs + submitMs. Prepare-side means divide by prepareCount (a Prepare that throws or sees an empty
    // frame never submits); logMs is the time inside Log() on the render thread and is a sub-span, never additive.
    const double prepares=prepareCount?static_cast<double>(prepareCount):1.0;
    const double bodies=bodyCount?static_cast<double>(bodyCount):1.0;
    const double submitted=submitCount?static_cast<double>(submitCount):1.0;
    const double aheads=aheadPrepares?static_cast<double>(aheadPrepares):1.0;
    // D-149: a CPU field the process could not sample is reported as -1, never as an idle CPU.
    const auto cpu=[&](double sum,double count) {return cpuSampled?sum/count:-1.0;};
    char text[640];
    snprintf(text,sizeof text,"timing frames=%d window=%d cycleMs=%.2f waitMs=%.2f prepToSubmitMs=%.2f submitMs=%.2f copyMs=%.2f fenceMs=%.2f endFrameMs=%.2f displayPeriodMs=%.2f prepBodyMs=%.2f handsMs=%.2f pumpMs=%.2f gapMs=%.2f logMs=%.2f prepares=%d cpuSampled=%d prepCpuMs=%.2f prepMainCpuMs=%.2f prepRenderCpuMs=%.2f submitCpuMs=%.2f gapCpuMs=%.2f aheadPrepares=%d aheadMs=%.2f",
        status.frames,submitCount,cycleCount?cycleMsSum/cycleCount:0.0,waitCount?waitMsSum/waitCount:0.0,prepToSubmitMsSum/submitted,submitMsSum/submitted,
        (copyIssueMsSum+copyFenceMsSum)/submitted,copyFenceMsSum/submitted,endFrameMsSum/submitted,displayPeriodMs,
        prepBodyMsSum/bodies,handsMsSum/bodies,pumpMsSum/bodies,prepareGapMsSum/prepares,logMsSum/submitted,prepareCount,
        cpuSampled?1:0,cpu(prepCpuMsSum,submitted),cpu(prepMainCpuMsSum,submitted),cpu(prepRenderCpuMsSum,submitted),
        cpu(submitCpuMsSum,submitted),cpu(gapCpuMsSum,prepares),aheadPrepares,aheadMsSum/aheads);
    Log(text);
    waitMsSum=prepToSubmitMsSum=submitMsSum=cycleMsSum=copyIssueMsSum=copyFenceMsSum=endFrameMsSum=0;waitCount=submitCount=cycleCount=0;
    prepBodyMsSum=handsMsSum=pumpMsSum=prepareGapMsSum=logMsSum=0;prepareCount=0;
    prepCpuMsSum=prepMainCpuMsSum=prepRenderCpuMsSum=submitCpuMsSum=gapCpuMsSum=0;bodyCount=0;aheadPrepares=0;aheadMsSum=0;
}
void Host::EndEmptyFrame(const char* call) {
    if(frameBegun) {
        XrFrameEndInfo end{XR_TYPE_FRAME_END_INFO};end.displayTime=frame.predictedDisplayTime;end.environmentBlendMode=XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        Check(xrEndFrame(session,&end),call);frameBegun=false;
    }
}
// D-148b3: take any outstanding deferred GPU completion proof. Close() calls this before the swapchain images are
// destroyed, so no copy is ever left unproven while its image disappears; a failed proof surfaces like any other copy
// failure (Close records it and the session ends in the failure state).
void Host::DrainDeferredGpuWaits() {
    for(int eye=0;eye<2;eye++) if(deferredPending[eye]) {
        // D-148b3: a queued copy whose proof cannot be taken must fail loudly; clearing the flag silently would be a
        // downgrade of the "no layer without a proven copy" rule.
        if(!deferredReady||!context||!device) throw std::runtime_error("deferred GPU proof unavailable at close");
        double fenceMs=0;WaitCopyCompletion(device.Get(),context.Get(),deferredQuery[eye].Get(),&fenceMs);
        deferredPending[eye]=false;deferredProofs++;status.gpuCompleted++;copyFenceMsSum+=fenceMs;
    }
}
void Host::Close() {
    // One failed shutdown call must not prevent the remaining destruction attempts.
    // Failed child handles remain owned until a successful parent destruction or a retry.
    std::string failures;
    auto attempt=[&](auto action) {
        try {action();} catch(const std::exception& e) {if(!failures.empty()) failures+="; ";failures+=e.what();}
    };
    attempt([&](){EndEmptyFrame("close empty frame");});
    attempt([&](){DrainDeferredGpuWaits();}); // D-148b3: no deferred copy may outlive its swapchain image
    if(running && sessionState!=XR_SESSION_STATE_LOSS_PENDING && sessionState!=XR_SESSION_STATE_EXITING) attempt([&]() {
        if(sessionState!=XR_SESSION_STATE_STOPPING) {
            Check(xrRequestExitSession(session),"xrRequestExitSession");
            auto stop=Clock::now();while(sessionState!=XR_SESSION_STATE_STOPPING && Clock::now()-stop<std::chrono::seconds(5)) {Pump();Sleep(5);}
        }
        if(sessionState!=XR_SESSION_STATE_STOPPING) throw std::runtime_error("STOPPING not observed; no EndSession");
        status.stopping=1;Check(xrEndSession(session),"xrEndSession after STOPPING");status.ended=1;running=false;
    });
    for(int eye=0;eye<2;eye++) if(swapchains[eye]) attempt([&]() {
        Check(xrDestroySwapchain(swapchains[eye]),"destroy swapchain");swapchains[eye]=XR_NULL_HANDLE;images[eye].clear();
    });
    if(handsCreated) attempt([&](){HandsDestroy();handsCreated=false;});
    if(space) attempt([&](){Check(xrDestroySpace(space),"destroy space");space=XR_NULL_HANDLE;});
    auto clearChildren=[&]() {space=XR_NULL_HANDLE;for(int eye=0;eye<2;eye++){swapchains[eye]=XR_NULL_HANDLE;images[eye].clear();}running=false;frameBegun=false;};
    if(session) attempt([&](){Check(xrDestroySession(session),"destroy session");session=XR_NULL_HANDLE;clearChildren();});
    if(instance) attempt([&](){Check(xrDestroyInstance(instance),"destroy instance");instance=XR_NULL_HANDLE;session=XR_NULL_HANDLE;clearChildren();});
    if(!instance&&!session&&!space&&!swapchains[0]&&!swapchains[1]) {
        query.Reset();context.Reset();device.Reset();source[0].Reset();source[1].Reset();
        deferredQuery[0].Reset();deferredQuery[1].Reset();deferredReady=false;deferredPending[0]=deferredPending[1]=false;
    }
    // D-149: the CPU probe handles belong to this host. Closing them is bookkeeping and can never fail a shutdown, so it
    // stays outside the attempt list; the CPU fields fall back to -1 for anything measured after this point.
    if(mainThreadHandle) {CloseHandle(mainThreadHandle);mainThreadHandle=nullptr;}
    if(renderThreadHandle) {CloseHandle(renderThreadHandle);renderThreadHandle=nullptr;}
    cpuSampled=false;
    if(!failures.empty()) {status.state=-1;Log("CLEANUP FAIL "+failures);throw std::runtime_error(failures);}
    status.state=3;Log("CLOSED orderly="+std::to_string(status.ended));log.close();
}
API int __cdecl Configure(void* left,void* right,const char* output,const char* runtime,int target,int frameDump) {
    std::lock_guard<std::mutex> lock(host.mutex);
    if(host.status.state!=0||!left||!right||!output||!runtime||target<120||target>1000||(frameDump!=0&&frameDump!=1)) return 2;
    if(runtime[0]&&!SetEnvironmentVariableA("XR_RUNTIME_JSON",runtime)) return 2; // empty = use the machine's active runtime
    host.source[0]=static_cast<ID3D11Texture2D*>(left);host.source[1]=static_cast<ID3D11Texture2D*>(right);
    host.output=output;host.target=target;host.frameDump=frameDump==1;host.status.mainThread=GetCurrentThreadId();return 0;
}
API void __cdecl ReadStatus(HostStatus* result) {
    if(!result)return;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(lock.owns_lock()) *result=host.status;
    else {*result=HostStatus{};result->state=6;} // render callback busy; never block Unity's main thread on xrWaitFrame
}
API int __cdecl RequestPrepare() {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=1&&host.status.state!=4)return 2;
    host.status.state=5;return 0;
}
API int __cdecl RequestCapture(int id) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state==3||host.status.state==-1||host.captureId>=0||id<0||id>10)return 2;
    host.captureId=id;return 0;
}
API const char* __cdecl ReadError() {
    thread_local std::string snapshot;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    snapshot=lock.owns_lock()?host.error:"native host busy";
    return snapshot.c_str();
}
API int __cdecl EnableHands(int enabled) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=0||(enabled!=0&&enabled!=1))return 2; // only before the init event
    host.handsRequested=enabled==1;return 0;
}
// D-148b3: opt-in overlap experiment. Must precede Configure/ConfigureHeadset and the init render event, like EnableHands.
API int __cdecl SetDeferredGpuWait(int enabled) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=0||(enabled!=0&&enabled!=1))return 2;
    host.deferredGpuWait=enabled==1;return 0;
}
// D-149: opt-in early frame prep. Same contract as SetDeferredGpuWait: before Configure/ConfigureHeadset and the init
// render event, so a sitting cannot change the frame structure mid-run.
API int __cdecl SetPrepareAhead(int enabled) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=0||(enabled!=0&&enabled!=1))return 2;
    host.prepareAhead=enabled==1;return 0;
}
// D-186-FovScale: pushes the managed-side FovScaleX (horizontal) and FovScaleY (vertical) onto the native
// host. Same contract as SetPrepareAhead / SetDeferredGpuWait (before Configure and the init render
// event, so the fovScale PrepareBody and Submit read is settled for the whole run). A value outside
// 0.50-1.00 is refused (returns 2) and the previous value is kept, so a bad value cannot start a sitting
// narrower than the user asked for.
API int __cdecl SetFovScaleX(float scale) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=0)return 2;
    if(!(scale>=0.50f&&scale<=1.00f))return 2;
    host.fovScaleX=scale;return 0;
}
API int __cdecl SetFovScaleY(float scale) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(host.status.state!=0)return 2;
    if(!(scale>=0.50f&&scale<=1.00f))return 2;
    host.fovScaleY=scale;return 0;
}
API int __cdecl ConfigureHeadset(int budgetSeconds) {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    // before Configure and the init event only; D-145: 0 = unlimited, otherwise the 60-900 s bound still applies
    if(host.status.state!=0||host.source[0]||budgetSeconds<0||(budgetSeconds>0&&(budgetSeconds<60||budgetSeconds>900)))return 2;
    host.headsetAllowed=true;host.budgetSeconds=budgetSeconds;return 0;
}
API int __cdecl ReadHands(HandsStatus* result) {
    if(!result)return 2;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock()){*result=HandsStatus{};return 6;}
    *result=host.hands;return host.handsCreated?0:1; // 1: not available (disabled, failed or not requested)
}
API const char* __cdecl ReadHandsError() {
    thread_local std::string snapshot;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    snapshot=lock.owns_lock()?host.handsError:"native host busy";
    return snapshot.c_str();
}
// D-103: queue one short vibration. 0 queued; 1 haptics unavailable (no hands or disabled after a failure); 2 bad argument; 6 busy (retry next frame).
API int __cdecl RequestHaptic(int hand,float amplitude,int durationMs) {
    if(hand<0||hand>1||!(amplitude>0.0f)||amplitude>1.0f||durationMs<1||durationMs>500)return 2;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    if(!lock.owns_lock())return 6;
    if(!host.handsCreated||host.hapticDisabled)return 1;
    host.hapticAmplitude[hand]=amplitude;host.hapticDurationNs[hand]=static_cast<long long>(durationMs)*1000000LL;host.hapticPending[hand]=true;return 0;
}
API const char* __cdecl ReadHapticError() {
    thread_local std::string snapshot;
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    snapshot=lock.owns_lock()?host.hapticError:"native host busy";
    return snapshot.c_str();
}
API int __cdecl CanReleaseSources() {
    std::unique_lock<std::mutex> lock(host.mutex,std::try_to_lock);
    return lock.owns_lock()&&!host.source[0]&&!host.source[1]&&!host.instance&&!host.session;
}
static void __stdcall OnRenderEvent(int event) {
    std::lock_guard<std::mutex> lock(host.mutex);
    try {
        if(host.status.state==-1&&event!=3) return;
        if(event==0) host.Init();
        else if(GetCurrentThreadId()!=host.status.renderThread) throw std::runtime_error("render thread changed");
        else if(event==1) {if(host.status.state!=5)throw std::runtime_error("prepare event not dispatched");host.Prepare();}
        else if(event==2) host.Submit();else if(event==3) host.Close();
    } catch(const std::exception& e) {
        host.error=e.what();host.Log("FAIL "+host.error);
        try {host.Close();} catch(const std::exception& cleanup) {host.error+="; cleanup: "+std::string(cleanup.what());}
        host.status.state=-1;
    }
}
API void* __cdecl GetRenderEvent() {return reinterpret_cast<void*>(&OnRenderEvent);}