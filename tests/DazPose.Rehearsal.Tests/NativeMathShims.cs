// Managed math shims only. These tests validate shared formulas; they cannot establish Unity native render/serialization behavior.
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
using Matrix=System.Numerics.Matrix4x4;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 up=>new(0,1,0);
        public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;
        public Vector3 normalized {get{var v=this;v.Normalize();return v;}}
        public void Normalize(){float m=MathF.Sqrt(sqrMagnitude);if(m>0){x/=m;y/=m;z/=m;}}
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float scale)=>new(a.x*scale,a.y*scale,a.z*scale);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 normal)=>a-normal*(Dot(a,normal)/normal.sqrMagnitude);
    }
    public struct Quaternion
    {
        public NQuaternion value;
        public static Quaternion identity=>new(){value=NQuaternion.Identity};
        public static Quaternion LookRotation(Vector3 direction,Vector3 up)
        {
            var f=NVector.Normalize(new(direction.x,direction.y,direction.z));
            var r=NVector.Normalize(NVector.Cross(new(up.x,up.y,up.z),f));var u=NVector.Cross(f,r);
            return new(){value=NQuaternion.CreateFromRotationMatrix(new Matrix(r.X,r.Y,r.Z,0,u.X,u.Y,u.Z,0,f.X,f.Y,f.Z,0,0,0,0,1))};
        }
    }
    public static class Mathf {public static float Abs(float value)=>MathF.Abs(value);}
}
