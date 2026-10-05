using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Footwear fit plus floor support after animation; seated contact remains a separate task.</summary>
    [ExecuteAlways, DefaultExecutionOrder(9000), DisallowMultipleComponent]
    public sealed class LaraHeelReview : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private Transform rigRoot, leftFoot, rightFoot, leftToe, rightToe;
        [SerializeField] private PerformerFootwearProfile footwear;
        [SerializeField] private bool shoeSupportActive = true;
        [SerializeField] private bool bentFootPoseActive = true;
        [SerializeField] private bool floorSupport=true;
        [SerializeField, HideInInspector] private float baseRigY;
        [Tooltip("Daz exported the reference foot shape baked into vertices and toe pivots.")]
        [SerializeField, Range(0,1)] private float referenceFootShape=1;
        [Tooltip("Standing support height; a footwear profile supplies this when assigned.")]
        [SerializeField, Range(-.1f,.3f)] private float rigHeight;
        [SerializeField, Range(-30,30)] private float additionalFootPitch, additionalToePitch;
        private Transform[] bones;
        private Quaternion[] previousBase, previousApplied;
        private Vector3[] referencePositions;
        private Quaternion[] referenceRotations;
        private Transform[] upperLeg,knee;
        private Transform[] feet;
        private readonly Vector3[] targets=new Vector3[2];
        private readonly Quaternion[] rotations=new Quaternion[2];
        private float[] stance=new float[2];
        private float ground;
        private bool applied;
        public PerformerFootwearProfile Footwear => footwear;
        public float RigHeight { get => footwear!=null?footwear.standingHeight:rigHeight;set {rigHeight=value;if(footwear!=null)footwear.standingHeight=value;Apply();} }
        public float FootShrink { get => footwear!=null?footwear.footShrink:0;set {if(footwear!=null)footwear.footShrink=Mathf.Clamp(value,0,.1f);Apply();} }
        public void SetFootwear(PerformerFootwearProfile profile)
            => SetFootwear(profile, profile != null, profile != null);
        public void SetFootwear(PerformerFootwearProfile profile, bool shoesVisible, bool keepBentFootPose)
        { footwear=profile;shoeSupportActive=shoesVisible;bentFootPoseActive=keepBentFootPose;ResetBindings();Apply(); }
        public float LeftStance => stance[0];
        public float RightStance => stance[1];
        public float GroundY => ground;
        public void SetReferenceFootShape(float value) { referenceFootShape=Mathf.Clamp01(value);Apply(); }
        public void Configure(SkinnedMeshRenderer renderer,Transform root,Transform lf,Transform rf,Transform lt,Transform rt,float height)
        {
            body=renderer;rigRoot=root;leftFoot=lf;rightFoot=rf;leftToe=lt;rightToe=rt;
            baseRigY=root.localPosition.y;rigHeight=height;ResetBindings();Apply();
        }
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        private void OnValidate() {ResetBindings();Apply();}
        private void ResetBindings(){UndoPrevious();bones=null;}
        private void OnDisable()
        {
            UndoPrevious();if(rigRoot!=null){var p=rigRoot.localPosition;p.y=baseRigY;rigRoot.localPosition=p;}
        }
        private void UndoPrevious()
        {
            if(!applied || bones==null)return;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null && Quaternion.Angle(bones[i].localRotation,previousApplied[i])<.001f)
                bones[i].localRotation=previousBase[i];
            applied=false;
        }
        public void Apply()
        {
            if(body==null || body.sharedMesh==null || rigRoot==null)return;
            UndoPrevious();
            if(bones==null)ResolveBindings();
            int shape=body.sharedMesh.GetBlendShapeIndex("CapturedHeelFootPose");
            if(shape>=0)body.SetBlendShapeWeight(shape,bentFootPoseActive?referenceFootShape*100:0);
            int fit=body.sharedMesh.GetBlendShapeIndex("WardrobeFootFit");
            if(fit>=0)body.SetBlendShapeWeight(fit,bentFootPoseActive?FootShrink/.05f*100:0);
            var performer=GetComponent<SuccubusPerformer>();
            float standingWeight=bentFootPoseActive?(performer!=null?1-performer.SeatingOwnershipWeight:1):0;
            var position=rigRoot.localPosition;position.y=baseRigY+RigHeight*standingWeight;rigRoot.localPosition=position;
            for(int i=0;i<bones.Length;i++)
            {
                previousBase[i]=bones[i].localRotation;
                if(footwear!=null && footwear.constrainToes && IsToe(bones[i].name))
                {bones[i].localPosition=referencePositions[i];bones[i].localRotation=referenceRotations[i];}
            }
            leftToe.localRotation*=Quaternion.Euler(additionalToePitch,0,0);rightToe.localRotation*=Quaternion.Euler(additionalToePitch,0,0);
            ground=(rigRoot.parent!=null?rigRoot.parent.position.y:0)+(footwear!=null?footwear.groundOffset:baseRigY);
            float reachDrop=0;
            for(int side=0;side<2;side++)
            {
                var foot=feet[side];var original=foot.rotation*Quaternion.Euler(additionalFootPitch,0,0);
                rotations[side]=original;targets[side]=foot.position;stance[side]=0;
                if(footwear==null || !shoeSupportActive || !floorSupport || standingWeight<=0)continue;
                var heel=side==0?footwear.leftHeel:footwear.rightHeel;
                var toe=side==0?footwear.leftToe:footwear.rightToe;
                var level=rigRoot.rotation*Quaternion.Euler(additionalFootPitch,0,0);
                float clearance=foot.position.y+Mathf.Min((level*heel).y,(level*toe).y)-ground;
                float blend=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((clearance-.015f)/Mathf.Max(.01f,footwear.swingBlendHeight)));
                stance[side]=blend*standingWeight;
                rotations[side]=Quaternion.Slerp(original,level,stance[side]);
                float targetY=ground-Mathf.Min((rotations[side]*heel).y,(rotations[side]*toe).y);
                targets[side].y=Mathf.Lerp(foot.position.y,targetY,stance[side]);
                float length=Vector3.Distance(upperLeg[side].position,knee[side].position)+Vector3.Distance(knee[side].position,foot.position)-.0001f;
                var offset=upperLeg[side].position-targets[side];float horizontal=offset.x*offset.x+offset.z*offset.z;
                float maxVertical=Mathf.Sqrt(Mathf.Max(0,length*length-horizontal));
                if(stance[side]>.9f)reachDrop=Mathf.Max(reachDrop,offset.y-maxVertical);
            }
            if(reachDrop>0){position=rigRoot.localPosition;position.y-=Mathf.Min(reachDrop,.15f)/Mathf.Max(.001f,rigRoot.lossyScale.y);rigRoot.localPosition=position;}
            for(int side=0;side<2;side++)
            {
                if(stance[side]>0)SolveLeg(upperLeg[side],knee[side],feet[side],targets[side],rigRoot.forward);
                feet[side].rotation=rotations[side];
            }
            for(int i=0;i<bones.Length;i++)previousApplied[i]=bones[i].localRotation;
            applied=true;
        }
        private static bool IsToe(string name)=>name.Contains("Toe") || name.Contains("Metatarsals");
        private void ResolveBindings()
        {
            var lookup=body.bones.ToDictionary(b=>b.name);
            feet=new[]{leftFoot,rightFoot};
            upperLeg=new[]{lookup["lThighBend"],lookup["rThighBend"]};knee=new[]{lookup["lShin"],lookup["rShin"]};
            bones=upperLeg.Concat(knee).Concat(new[]{leftFoot,rightFoot}).Concat(body.bones.Where(b=>IsToe(b.name))).Distinct().ToArray();
            previousBase=new Quaternion[bones.Length];previousApplied=new Quaternion[bones.Length];
            referencePositions=new Vector3[bones.Length];referenceRotations=new Quaternion[bones.Length];
            var indices=body.bones.Select((b,i)=>(b,i)).ToDictionary(x=>x.b,x=>x.i);var bind=body.sharedMesh.bindposes;
            for(int i=0;i<bones.Length;i++)
            {
                var bone=bones[i];var local=indices.TryGetValue(bone.parent,out int parent)?bind[parent]*bind[indices[bone]].inverse:bone.localToWorldMatrix;
                referencePositions[i]=local.GetColumn(3);referenceRotations[i]=local.rotation;
            }
        }
        private static void SolveLeg(Transform upper,Transform shin,Transform foot,Vector3 target,Vector3 fallbackPole)
        {
            var origin=upper.position;var direction=target-origin;
            float a=Vector3.Distance(origin,shin.position),b=Vector3.Distance(shin.position,foot.position);
            float d=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);direction.Normalize();
            var pole=shin.position-origin;pole-=direction*Vector3.Dot(pole,direction);
            if(pole.sqrMagnitude<1e-8f)pole=Vector3.ProjectOnPlane(fallbackPole,direction);
            pole.Normalize();float along=(a*a+d*d-b*b)/(2*d);float across=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var wantedKnee=origin+direction*along+pole*across;
            upper.rotation=Quaternion.FromToRotation(shin.position-origin,wantedKnee-origin)*upper.rotation;
            shin.rotation=Quaternion.FromToRotation(foot.position-shin.position,target-shin.position)*shin.rotation;
        }
    }
}
