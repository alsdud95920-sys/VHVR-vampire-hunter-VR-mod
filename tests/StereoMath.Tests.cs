using VampireHuntersVR.Render;

int passed=0;
void Near(float actual,float expected,string name) {
    if(MathF.Abs(actual-expected)>0.00002f) throw new Exception($"{name}: {actual} != {expected}");
    passed++;
}
void Reject(Action action,string name) {
    try {action();} catch(ArgumentException) {passed++; return;}
    throw new Exception("not rejected: "+name);
}
float a=MathF.PI/4;
float[] p=StereoMath.Projection(-a,a,a,-a,0.05f,100);
Near(p[0],1,"projection x"); Near(p[5],1,"projection y"); Near(p[2],0,"center x"); Near(p[6],0,"center y");
float[] identity={0,0,0,1};
float[] v=StereoMath.UnityView(new float[3],identity);
Near(StereoMath.Project(v,p,new[]{0f,0f,0.05f})[2],-1,"near clip");
Near(StereoMath.Project(v,p,new[]{0f,0f,100f})[2],1,"far clip");
float[] asym=StereoMath.Projection(-0.6f,0.9f,0.8f,-0.7f,0.1f,50);
Near(StereoMath.Project(v,asym,new[]{MathF.Tan(-0.6f)*2,0f,2f})[0],-1,"left FOV edge");
Near(StereoMath.Project(v,asym,new[]{MathF.Tan(0.9f)*2,0f,2f})[0],1,"right FOV edge");
Near(StereoMath.Project(v,asym,new[]{0f,MathF.Tan(0.8f)*2,2f})[1],1,"top FOV edge");
Near(StereoMath.Project(v,asym,new[]{0f,MathF.Tan(-0.7f)*2,2f})[1],-1,"bottom FOV edge");
float[] lp=StereoMath.UnityPosition(-0.03f,0,0), rp=StereoMath.UnityPosition(0.03f,0,0);
float[] lv=StereoMath.UnityView(lp,identity), rv=StereoMath.UnityView(rp,identity);
float Disparity(float z) => StereoMath.Project(lv,p,new[]{0f,0f,z})[0]-StereoMath.Project(rv,p,new[]{0f,0f,z})[0];
Near(Disparity(1),0.06f,"60mm IPD near disparity"); Near(Disparity(2),0.03f,"inverse depth disparity");
Near(Disparity(10),0.006f,"far disparity");
float[] yaw=StereoMath.UnityOrientation(0,MathF.Sin(a/2),0,MathF.Cos(a/2));
float[] head=StereoMath.Rotate(yaw,new[]{0f,0f,1f});
Near(head[0],-MathF.Sin(a),"XR yaw reflection"); Near(head[2],MathF.Cos(a),"XR forward reflection");
float[] worldView=StereoMath.UnityView(new[]{10f,2f,3f},yaw);
Near(StereoMath.Project(worldView,p,new[]{10+head[0]*2,2f,3+head[2]*2})[0],0,"rotated translated camera forward");
float[] moved=StereoMath.UnityView(new[]{0.1f,0f,0f},identity);
Near(StereoMath.Project(moved,p,new[]{0f,0f,1f})[0],-0.1f,"virtual lean opposite scene shift");
Near(StereoMath.UnityPosition(1,2,-3,2)[2],6,"meters-to-units reflection");
float[] product=StereoMath.MultiplyQuaternion(yaw,identity);
Near(product[1],yaw[1],"quaternion composition");
Reject(()=>StereoMath.Projection(-a,a,a,-a,0,100),"zero near");
Reject(()=>StereoMath.Projection(-a,a,a,-a,1,1),"equal far");
Reject(()=>StereoMath.Projection(a,-a,a,-a,0.1f,100),"reversed FOV");
Reject(()=>StereoMath.Projection(float.NaN,a,a,-a,0.1f,100),"nonfinite FOV");
Reject(()=>StereoMath.Projection(2,2.1f,a,-a,0.1f,100),"left outside tangent domain");
Reject(()=>StereoMath.Projection(-2.1f,-2,a,-a,0.1f,100),"right outside tangent domain");
Reject(()=>StereoMath.Projection(-a,a,2.1f,2,0.1f,100),"down outside tangent domain");
Reject(()=>StereoMath.UnityOrientation(0,0,0,0),"zero quaternion");
Reject(()=>StereoMath.UnityPosition(0,0,0,-1),"negative scale");
Reject(()=>StereoMath.Project(v,p,new[]{0f,0f,-1f}),"behind camera");
Console.WriteLine($"RESULT {passed} stereo math checks passed; pure math only, no runtime or game stereo claim");