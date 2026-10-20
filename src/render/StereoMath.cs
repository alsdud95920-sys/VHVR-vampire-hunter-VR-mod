using System;

namespace VampireHuntersVR.Render;

// Column-vector, row-major matrices. Unity Camera.projectionMatrix expects OpenGL-style clip Z [-1,1];
// Unity converts that to the active GPU convention internally. Do NOT feed D3D [0,1] projection directly.
// OpenXR local space: +X right, +Y up, -Z forward. Unity transforms: +Z forward.
internal static class StereoMath
{
    public static float[] Projection(float left, float right, float up, float down, float near, float far)
    {
        if (!Finite(left) || !Finite(right) || !Finite(up) || !Finite(down) ||
            MathF.Abs(left) >= MathF.PI/2 || MathF.Abs(right) >= MathF.PI/2 || MathF.Abs(down) >= MathF.PI/2 || MathF.Abs(up) >= MathF.PI/2 ||
            left >= right || down >= up || !Finite(near) || !Finite(far) || near <= 0 || far <= near)
            throw new ArgumentException("invalid FOV or clipping planes");
        float l=MathF.Tan(left), r=MathF.Tan(right), u=MathF.Tan(up), d=MathF.Tan(down);
        return new[] {2/(r-l),0,(r+l)/(r-l),0, 0,2/(u-d),(u+d)/(u-d),0,
            0,0,-(far+near)/(far-near),-2*far*near/(far-near), 0,0,-1,0};
    }

    public static float[] UnityPosition(float x, float y, float z, float unitsPerMeter=1)
    {
        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(unitsPerMeter) || unitsPerMeter <= 0)
            throw new ArgumentException("invalid pose or world scale");
        return new[] {x*unitsPerMeter,y*unitsPerMeter,-z*unitsPerMeter};
    }

    public static float[] UnityOrientation(float x, float y, float z, float w)
    {
        float norm=x*x+y*y+z*z+w*w;
        if (!Finite(norm) || MathF.Abs(norm-1)>0.01f) throw new ArgumentException("invalid pose quaternion");
        float inverse=1/MathF.Sqrt(norm);
        return new[] {-x*inverse,-y*inverse,z*inverse,w*inverse};
    }

    public static float[] Rotate(float[] q, float[] v)
    {
        if(q.Length!=4 || v.Length!=3) throw new ArgumentException("pose dimensions");
        float tx=2*(q[1]*v[2]-q[2]*v[1]);
        float ty=2*(q[2]*v[0]-q[0]*v[2]);
        float tz=2*(q[0]*v[1]-q[1]*v[0]);
        return new[] {v[0]+q[3]*tx+q[1]*tz-q[2]*ty,
            v[1]+q[3]*ty+q[2]*tx-q[0]*tz, v[2]+q[3]*tz+q[0]*ty-q[1]*tx};
    }

    public static float[] MultiplyQuaternion(float[] a,float[] b) => new[] {
        a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1],
        a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],
        a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3],
        a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2]};

    public static float[] UnityView(float[] position,float[] q)
    {
        if(position.Length!=3 || q.Length!=4) throw new ArgumentException("pose dimensions");
        float x=q[0],y=q[1],z=q[2],w=q[3];
        // Transpose of quaternion rotation, then negate camera-space Z for Unity's right-handed view.
        float[] m={1-2*(y*y+z*z),2*(x*y+z*w),2*(x*z-y*w),0,
            2*(x*y-z*w),1-2*(x*x+z*z),2*(y*z+x*w),0,
            -2*(x*z+y*w),-2*(y*z-x*w),-(1-2*(x*x+y*y)),0, 0,0,0,1};
        for(int row=0;row<3;row++) m[row*4+3]=-(m[row*4]*position[0]+m[row*4+1]*position[1]+m[row*4+2]*position[2]);
        return m;
    }

    public static float[] Project(float[] view,float[] projection,float[] world)
    {
        if(view.Length!=16 || projection.Length!=16 || world.Length!=3) throw new ArgumentException("matrix dimensions");
        float[] camera=Transform(view,new[] {world[0],world[1],world[2],1f});
        float[] clip=Transform(projection,camera);
        if (!Finite(clip[3]) || clip[3]<=0) throw new ArgumentException("point behind or on camera plane");
        return new[] {clip[0]/clip[3],clip[1]/clip[3],clip[2]/clip[3]};
    }

    private static float[] Transform(float[] m,float[] v)
    {
        float[] r=new float[4];
        for(int row=0;row<4;row++) for(int col=0;col<4;col++) r[row]+=m[row*4+col]*v[col];
        return r;
    }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}