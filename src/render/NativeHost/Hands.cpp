// Optional per-hand input and haptics (D-084/D-091/D-103). Separate ABI: HostStatus is unchanged.
// Isolated from the stereo path: every failure here disables only the hands layer and is reported, never hidden.
// The fault fixture (tests\NativeHost.Faults.cpp) provides its own stubs and never links this file.
#include "Host.h"
#include <cstdio>
#include <stdexcept>
#include <string>

namespace {
// "vhvr_hands": the action set only ever names the mod's own bindings; no game action is touched.
XrActionSet g_set=XR_NULL_HANDLE;
XrAction g_pose[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrPath g_poseSub[2]={XR_NULL_PATH,XR_NULL_PATH};
XrAction g_haptic[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_trigger[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_face[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_stick[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_stickClick[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_lower[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrAction g_menu[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
// D-206 (BUG-204): the grip. Present but unread since D-057 deleted the grip assignment; the turret build mode is
// the only consumer, and only while the game says it is in build mode.
XrAction g_squeeze[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
XrSpace g_aim[2]={XR_NULL_HANDLE,XR_NULL_HANDLE};
bool g_ready=false;

void HandsCheck(XrResult result,const char* call) {
    if(XR_FAILED(result)) throw std::runtime_error(std::string(call)+" XrResult="+std::to_string(result));
}

XrPath HandsPath(XrInstance instance,const char* text) {
    XrPath out=XR_NULL_PATH;
    HandsCheck(xrStringToPath(instance,text,&out),text);
    return out;
}
}

void HandsInit(XrInstance instance,XrSession session) {
    g_ready=false;
    XrActionSetCreateInfo setCreate{XR_TYPE_ACTION_SET_CREATE_INFO};
    std::snprintf(setCreate.actionSetName,sizeof setCreate.actionSetName,"vhvr_hands");
    std::snprintf(setCreate.localizedActionSetName,sizeof setCreate.localizedActionSetName,"VampireHuntersVR hands");
    HandsCheck(xrCreateActionSet(instance,&setCreate,&g_set),"xrCreateActionSet vhvr_hands");
    g_poseSub[0]=HandsPath(instance,"/user/hand/left");
    g_poseSub[1]=HandsPath(instance,"/user/hand/right");
    for(int hand=0;hand<2;hand++) {
        char name[64];
        std::snprintf(name,sizeof name,"vhvr_pose_%02d",hand);
        XrActionCreateInfo create{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_POSE_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_pose[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_haptic_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        // XR_ACTION_TYPE_VIBRATION_OUTPUT: the only output action; one per hand.
        create.actionType=XR_ACTION_TYPE_VIBRATION_OUTPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_haptic[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_trigger_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_FLOAT_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_trigger[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_face_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_BOOLEAN_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_face[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_stick_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_VECTOR2F_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_stick[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_stickclick_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_BOOLEAN_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_stickClick[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_lower_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_BOOLEAN_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_lower[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_menu_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_BOOLEAN_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_menu[hand]),name);
        std::snprintf(name,sizeof name,"vhvr_squeeze_%02d",hand);
        create=XrActionCreateInfo{XR_TYPE_ACTION_CREATE_INFO};
        create.actionType=XR_ACTION_TYPE_FLOAT_INPUT;
        std::snprintf(create.actionName,sizeof create.actionName,"%s",name);
        std::snprintf(create.localizedActionName,sizeof create.localizedActionName,"%s",name);
        create.countSubactionPaths=1;create.subactionPaths=&g_poseSub[hand];
        HandsCheck(xrCreateAction(g_set,&create,&g_squeeze[hand]),name);
    }
    // Quest Touch profile (OpenXR path is oculus/touch_controller): map only controls this profile exposes.
    // XR_ERROR_PATH_UNSUPPORTED from the old menu binding disabled the WHOLE optional hand action set,
    // which removed the menu laser and stick scrolling. Touch has Y on left, B on right, and a system/menu
    // button on left only. Submit one complete profile suggestion (OpenXR expects one binding set per profile).
    const XrPath profile=HandsPath(instance,"/interaction_profiles/oculus/touch_controller");
    const char* sideName[2]={"/user/hand/left","/user/hand/right"};
    const char* hapticPath[2]={"/user/hand/left/output/haptic","/user/hand/right/output/haptic"};
    const char* facePath[2]={"/user/hand/left/input/y/click","/user/hand/right/input/b/click"};
    const char* lowerPath[2]={"/user/hand/left/input/x/click","/user/hand/right/input/a/click"};
    XrActionSuggestedBinding bindings[19];int count=0;char text[96];
    for(int hand=0;hand<2;hand++) {
        std::snprintf(text,sizeof text,"%s/input/aim/pose",sideName[hand]);
        bindings[count++]={g_pose[hand],HandsPath(instance,text)};
        bindings[count++]={g_haptic[hand],HandsPath(instance,hapticPath[hand])};
        std::snprintf(text,sizeof text,"%s/input/trigger/value",sideName[hand]);
        bindings[count++]={g_trigger[hand],HandsPath(instance,text)};
        bindings[count++]={g_face[hand],HandsPath(instance,facePath[hand])};
        bindings[count++]={g_lower[hand],HandsPath(instance,lowerPath[hand])};
        std::snprintf(text,sizeof text,"%s/input/thumbstick",sideName[hand]);
        bindings[count++]={g_stick[hand],HandsPath(instance,text)};
        std::snprintf(text,sizeof text,"%s/input/thumbstick/click",sideName[hand]);
        bindings[count++]={g_stickClick[hand],HandsPath(instance,text)};
        // D-206: the grip. oculus/touch_controller exposes input/squeeze/value on both hands.
        std::snprintf(text,sizeof text,"%s/input/squeeze/value",sideName[hand]);
        bindings[count++]={g_squeeze[hand],HandsPath(instance,text)};
    }
    bindings[count++]={g_menu[0],HandsPath(instance,"/user/hand/left/input/menu/click")};
    XrInteractionProfileSuggestedBinding suggested{XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
    suggested.interactionProfile=profile;suggested.countSuggestedBindings=count;suggested.suggestedBindings=bindings;
    HandsCheck(xrSuggestInteractionProfileBindings(instance,&suggested),"xrSuggestInteractionProfileBindings Touch");
    for(int hand=0;hand<2;hand++) {
        XrActionSpaceCreateInfo spaceCreate{XR_TYPE_ACTION_SPACE_CREATE_INFO};
        spaceCreate.action=g_pose[hand];spaceCreate.subactionPath=g_poseSub[hand];
        spaceCreate.poseInActionSpace.orientation.w=1;
        HandsCheck(xrCreateActionSpace(session,&spaceCreate,&g_aim[hand]),"xrCreateActionSpace aim");
    }
    XrSessionActionSetsAttachInfo attach{XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO};
    attach.countActionSets=1;attach.actionSets=&g_set;
    HandsCheck(xrAttachSessionActionSets(session,&attach),"xrAttachSessionActionSets vhvr_hands");
    g_ready=true;
}
void HandsSync(XrSession session,XrSpace space,XrTime time,HandsStatus& out) {
    out=HandsStatus{};
    if(!g_ready) return;
    XrActiveActionSet active{g_set,XR_NULL_PATH};
    XrActionsSyncInfo sync{XR_TYPE_ACTIONS_SYNC_INFO};sync.countActiveActionSets=1;sync.activeActionSets=&active;
    HandsCheck(xrSyncActions(session,&sync),"xrSyncActions");
    out.available=1;out.seq++;out.frames++;
    for(int hand=0;hand<2;hand++) {
        auto& sample=out.hands[hand];
        XrActionStateGetInfo get{XR_TYPE_ACTION_STATE_GET_INFO};
        XrActionStatePose pose{XR_TYPE_ACTION_STATE_POSE};
        get.action=g_pose[hand];
        HandsCheck(xrGetActionStatePose(session,&get,&pose),"xrGetActionStatePose");
        if(!pose.isActive) continue;
        XrSpaceLocation location{XR_TYPE_SPACE_LOCATION};
        HandsCheck(xrLocateSpace(g_aim[hand],space,time,&location),"xrLocateSpace aim");
        if((location.locationFlags&XR_SPACE_LOCATION_POSITION_VALID_BIT)==0||(location.locationFlags&XR_SPACE_LOCATION_ORIENTATION_VALID_BIT)==0) continue;
        sample.active=1;
        sample.x=location.pose.position.x;sample.y=location.pose.position.y;sample.z=location.pose.position.z;
        sample.qx=location.pose.orientation.x;sample.qy=location.pose.orientation.y;sample.qz=location.pose.orientation.z;sample.qw=location.pose.orientation.w;
        XrActionStateFloat trigger{XR_TYPE_ACTION_STATE_FLOAT};get.action=g_trigger[hand];
        HandsCheck(xrGetActionStateFloat(session,&get,&trigger),"xrGetActionStateFloat trigger");
        sample.trigger=trigger.isActive?trigger.currentState:0.0f;
        XrActionStateBoolean face{XR_TYPE_ACTION_STATE_BOOLEAN};get.action=g_face[hand];
        HandsCheck(xrGetActionStateBoolean(session,&get,&face),"xrGetActionStateBoolean face");
        sample.face=face.isActive&&face.currentState?1:0;
        XrActionStateVector2f stick{XR_TYPE_ACTION_STATE_VECTOR2F};get.action=g_stick[hand];
        HandsCheck(xrGetActionStateVector2f(session,&get,&stick),"xrGetActionStateVector2f stick");
        sample.sx=stick.isActive?stick.currentState.x:0.0f;
        sample.sy=stick.isActive?stick.currentState.y:0.0f;
        XrActionStateBoolean click{XR_TYPE_ACTION_STATE_BOOLEAN};get.action=g_stickClick[hand];
        HandsCheck(xrGetActionStateBoolean(session,&get,&click),"xrGetActionStateBoolean stick click");
        sample.stick=click.isActive&&click.currentState?1:0;
        XrActionStateBoolean lower{XR_TYPE_ACTION_STATE_BOOLEAN};get.action=g_lower[hand];
        HandsCheck(xrGetActionStateBoolean(session,&get,&lower),"xrGetActionStateBoolean lower");
        sample.lower=lower.isActive&&lower.currentState?1:0;
        XrActionStateBoolean menu{XR_TYPE_ACTION_STATE_BOOLEAN};get.action=g_menu[hand];
        HandsCheck(xrGetActionStateBoolean(session,&get,&menu),"xrGetActionStateBoolean menu");
        sample.menu=menu.isActive&&menu.currentState?1:0;
        XrActionStateFloat squeeze{XR_TYPE_ACTION_STATE_FLOAT};get.action=g_squeeze[hand];
        HandsCheck(xrGetActionStateFloat(session,&get,&squeeze),"xrGetActionStateFloat squeeze");
        sample.squeeze=squeeze.isActive?squeeze.currentState:0.0f;
    }
}

bool HandsHaptic(XrSession session,int hand,float amplitude,long long durationNs) {
    if(!g_ready||hand<0||hand>1) throw std::runtime_error("haptic without hands init");
    XrHapticVibration vibration{XR_TYPE_HAPTIC_VIBRATION};
    vibration.amplitude=amplitude;
    vibration.duration=durationNs;
    XrHapticActionInfo actionInfo{XR_TYPE_HAPTIC_ACTION_INFO};
    actionInfo.action=g_haptic[hand];actionInfo.subactionPath=g_poseSub[hand];
    HandsCheck(xrApplyHapticFeedback(session,&actionInfo,reinterpret_cast<const XrHapticBaseHeader*>(&vibration)),"xrApplyHapticFeedback");
    return true;
}

void HandsDestroy() {
    for(int hand=0;hand<2;hand++) {
        if(g_aim[hand]!=XR_NULL_HANDLE) {xrDestroySpace(g_aim[hand]);g_aim[hand]=XR_NULL_HANDLE;} // destroy hand space
    }
    if(g_set!=XR_NULL_HANDLE) {xrDestroyActionSet(g_set);g_set=XR_NULL_HANDLE;}
    for(int hand=0;hand<2;hand++) {
        g_pose[hand]=g_haptic[hand]=g_trigger[hand]=g_face[hand]=XR_NULL_HANDLE;
        g_stick[hand]=g_stickClick[hand]=g_lower[hand]=g_menu[hand]=g_squeeze[hand]=XR_NULL_HANDLE;
        g_poseSub[hand]=XR_NULL_PATH;
    }
    g_ready=false;
}
