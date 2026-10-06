using System;
using UnityEngine;
using DazPose.Performer.HandGrip;
namespace DazPose.UnityValidation.Partner
{
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class PartnerContactTarget : MonoBehaviour, IGripTarget, IGripTargetContactSurface
    {
        [SerializeField] private PartnerBody body;
        [SerializeField] private PartnerContactProfile profile;
        [SerializeField] private Vector3 referenceNormalRoot = Vector3.right;
        private Vector3[] centersPelvis;
        private Vector3 normalPelvis;
        private Vector3 sectionTangentPelvis;
        private Vector3 surfaceOriginPelvis;
        private Quaternion surfaceRotationPelvis;
        private Transform boundPelvis;
        public PartnerBody Body => body;
        public PartnerContactProfile Profile => profile;
        public string ValidationError {get;private set;}
        public void Configure(PartnerBody source,PartnerContactProfile data){body=source;profile=data;Bind();}
        private void OnEnable(){Bind();}
        private bool Bind()
        {
            centersPelvis=null;boundPelvis=null;
            if(body==null||profile==null||body.MainPelvis==null){ValidationError="Partner body, profile and pelvis are required.";return false;}
            if(!profile.IsReady(out string reason)){ValidationError=reason;return false;}
            boundPelvis=body.MainPelvis;
            var centers=profile.CentersRoot;centersPelvis=new Vector3[centers.Length];
            for(int i=0;i<centers.Length;i++)centersPelvis[i]=boundPelvis.InverseTransformPoint(body.transform.TransformPoint(centers[i]));
            normalPelvis=boundPelvis.InverseTransformDirection(body.transform.TransformDirection(referenceNormalRoot));
            sectionTangentPelvis=boundPelvis.InverseTransformDirection(body.transform.TransformDirection(profile.SectionTangentRoot));
            surfaceOriginPelvis=boundPelvis.InverseTransformPoint(body.transform.position);
            surfaceRotationPelvis=Quaternion.Inverse(boundPelvis.rotation)*body.transform.rotation;
            ValidationError=null;return true;
        }
        public GripFrame Evaluate(float position01)
        {
            if(!TryEvaluate(position01,out GripFrame frame,out string reason))throw new InvalidOperationException(reason);
            return frame;
        }
        public bool TryGetContactSurface(out GripContactSurfaceSnapshot snapshot)
        {
            snapshot = default;
            if (!TryEvaluate(.5f, out _, out _)) return false;
            snapshot = new GripContactSurfaceSnapshot(profile.ContactSurface,
                boundPelvis.TransformPoint(surfaceOriginPelvis), boundPelvis.rotation*surfaceRotationPelvis);
            return snapshot.IsValid;
        }
        public bool TryEvaluate(float position01,out GripFrame frame,out string reason)
        {
            frame=default;reason=null;
            if(!isActiveAndEnabled||body==null||!body.isActiveAndEnabled||!body.IsReady||!body.IsContactSupported||body.MainPelvis==null)
                reason="Partner contact requires a ready active body at its supported rigid endpoint and unit scale.";
            else if(profile==null||!profile.IsReady(out reason))reason=reason??"Contact profile unavailable.";
            else if(body.StructuralBody==null||!body.StructuralBody.enabled||!body.StructuralBody.gameObject.activeInHierarchy||body.ShellRenderer==null||!body.ShellRenderer.enabled||!body.ShellRenderer.gameObject.activeInHierarchy
                ||body.StructuralBody.sharedMesh!=profile.BodyMesh||body.ShellRenderer.sharedMesh!=profile.ShellMesh)
                reason="Visible evaluated body/shell bindings do not match the measured profile.";
            else if(!GripFrame.IsFinite(position01)||position01<0f||position01>1f)reason="Contact position must be normalized.";
            if(reason!=null){ValidationError=reason;return false;}
            if(centersPelvis==null||boundPelvis!=body.MainPelvis){if(!Bind()){reason=ValidationError;return false;}}
            int count=centersPelvis.Length;float sample=position01*(count-1);int index=Mathf.Min(count-2,Mathf.FloorToInt(sample));float t=sample-index;
            Vector3 center=Vector3.Lerp(centersPelvis[index],centersPelvis[index+1],t);
            // Radii were measured in these planes. Re-deriving a tangent from contour
            // centers would rotate the assumed surface without re-measuring it.
            Vector3 tangent=sectionTangentPelvis;
            if(!GripFrame.TryCreate(boundPelvis.TransformPoint(center),boundPelvis.TransformDirection(tangent),boundPelvis.TransformDirection(normalPelvis),
                Mathf.Lerp(profile.Radius(index),profile.Radius(index+1),t),out frame)){reason="Measured contact frame is degenerate.";ValidationError=reason;return false;}
            ValidationError=null;return true;
        }
        private Vector3 Tangent(int i){return(centersPelvis[Mathf.Min(centersPelvis.Length-1,i+1)]-centersPelvis[Mathf.Max(0,i-1)]).normalized;}
    }
}
