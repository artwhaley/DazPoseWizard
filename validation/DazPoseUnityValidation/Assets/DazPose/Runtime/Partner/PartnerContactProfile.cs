using System;
using UnityEngine;
using DazPose.Performer.HandGrip;
namespace DazPose.UnityValidation.Partner
{
    [CreateAssetMenu(menuName="Performer/Partner Contact Profile")]
    public sealed class PartnerContactProfile : ScriptableObject
    {
        [SerializeField] private Mesh bodyMesh;
        [SerializeField] private Mesh shellMesh;
        [SerializeField] private Vector3[] centersRoot = Array.Empty<Vector3>();
        [SerializeField] private float[] radii = Array.Empty<float>();
        [SerializeField] private Vector2 supportedArcSpan = new Vector2(.2f,.75f);
        [SerializeField] private float measuredMaximumRadialDeviation;
        [SerializeField] private string measurementIdentity;
        [SerializeField] private Vector3 sectionTangentRoot = Vector3.forward;
        [SerializeField] private GripContactSurfaceData contactSurface;
        public GripContactSurfaceData ContactSurface => contactSurface;
        public Vector3 SectionTangentRoot => sectionTangentRoot;
        public Mesh BodyMesh => bodyMesh;
        public Mesh ShellMesh => shellMesh;
        public Vector2 SupportedArcSpan => supportedArcSpan;
        public float MaximumRadialDeviation => measuredMaximumRadialDeviation;
        public string MeasurementIdentity => measurementIdentity;
        public Vector3[] CentersRoot => (Vector3[])centersRoot.Clone();
        public float Radius(int index) => radii[index];
        public void Configure(Mesh body, Mesh shell, Vector3[] centers, float[] surfaceRadii, Vector2 span, float deviation, string identity, Vector3 sectionTangent, GripContactSurfaceData surface)
        {
            if (centers == null) throw new ArgumentNullException(nameof(centers));
            if (surfaceRadii == null) throw new ArgumentNullException(nameof(surfaceRadii));
            bodyMesh=body; shellMesh=shell; centersRoot=(Vector3[])centers.Clone();radii=(float[])surfaceRadii.Clone();
            supportedArcSpan=span;measuredMaximumRadialDeviation=deviation;measurementIdentity=identity;
            sectionTangentRoot = sectionTangent.normalized;
            contactSurface = surface;
            if(!IsReady(out string reason))throw new ArgumentException(reason);
        }
        public bool IsReady(out string reason)
        {
            reason=null;
            if(bodyMesh==null||shellMesh==null||centersRoot==null||radii==null||centersRoot.Length<2||radii.Length!=centersRoot.Length)
                reason="Measured body/shell meshes and matching center/radius samples are required.";
            else if(!GripFrame.IsFinite(sectionTangentRoot)||sectionTangentRoot.sqrMagnitude < .99f)
                reason="The calibrated cross-section plane tangent is required.";
            else if(contactSurface==null)
                reason="Prepared evaluated contact surface is required.";
            else if(!contactSurface.IsReady(out reason))
                reason=reason??"Prepared contact surface is invalid.";
            // The nominal radius places the palm; actual collision uses the prepared
            // triangle surface. Circular-fit error is evidence, not a collision bound.
            else if(!GripFrame.IsFinite(measuredMaximumRadialDeviation)||measuredMaximumRadialDeviation<0f)
                reason="Measured nominal-radius deviation must be finite and nonnegative.";
            else if(!GripFrame.IsFinite(supportedArcSpan.x)||!GripFrame.IsFinite(supportedArcSpan.y)||supportedArcSpan.x<0f||supportedArcSpan.y>1f||supportedArcSpan.x>=supportedArcSpan.y||string.IsNullOrEmpty(measurementIdentity))
                reason="Supported interior span and measurement identity are required.";
            else for(int i=0;i<centersRoot.Length;i++)
            {
                if(!GripFrame.IsFinite(centersRoot[i])||!GripFrame.IsFinite(radii[i])||radii[i]<=0f || i>0&&(centersRoot[i]-centersRoot[i-1]).sqrMagnitude<1e-10f)
                {reason="Contact samples must be finite, positive and nondegenerate.";break;}
            }
            return reason==null;
        }
    }
}
