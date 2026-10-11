using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace DazPose.Performer.HandGrip
{
    internal struct PerformerArmIKJob : IAnimationJob
    {
        public TwoBoneIKConstraintJob Constraint;
        public float Weight;
        public FloatProperty EvaluationMarker;
        public NativeArray<Vector3> Evaluation;
        public NativeArray<Quaternion> Rotation;
        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid) return;
            Constraint.jobWeight.Set(stream, Mathf.Clamp01(Weight));
            Constraint.targetPositionWeight.Set(stream, 1f);
            Constraint.targetRotationWeight.Set(stream, 1f);
            Constraint.hintWeight.Set(stream, 1f);
            Constraint.ProcessAnimation(stream);
            if (Weight > 0f) { Evaluation[0]=Constraint.target.GetPosition(stream); Evaluation[1]=Constraint.tip.GetPosition(stream); Evaluation[2]=Constraint.root.GetPosition(stream); Rotation[0]=Constraint.tip.GetRotation(stream); }
            EvaluationMarker.Set(stream, 1f);
        }
    }

    internal sealed class PerformerArmIKLayer : IDisposable
    {
        private AnimationScriptPlayable _playable;
        private NativeArray<Vector3> _evaluation;
        private NativeArray<Quaternion> _rotation;
        public string EvaluationDetails => _evaluation.IsCreated ? "IK target="+_evaluation[0]+"; IK tip="+_evaluation[1]+"; scene tip="+Hand.position : "no evaluation";
        private readonly GameObject _targets;
        private readonly Transform _target;
        private readonly Transform _hint;
        public readonly Transform Upper;
        public readonly Transform Lower;
        public readonly Transform Hand;
        public readonly float UpperLength;
        public readonly float LowerLength;
        public Playable Output => _playable;
        public Vector3 HintPosition => _hint.position;

        public PerformerArmIKLayer(PlayableGraph graph, Playable input, Animator animator)
        {
            if (!PerformerMotionMaskUtility.TryResolveRightArm(animator, out _, out Transform upper,
                out Transform lower, out Transform hand, out _, out string reason))
                throw new InvalidOperationException(reason);
            Upper = upper; Lower = lower; Hand = hand;
            UpperLength = Vector3.Distance(upper.position, lower.position);
            LowerLength = Vector3.Distance(lower.position, hand.position);
            if (UpperLength <= 0.0001f || LowerLength <= 0.0001f)
                throw new InvalidOperationException("Degenerate Generic arm chain.");
            _targets = new GameObject("Grip IK Targets (owned)") { hideFlags = HideFlags.DontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_targets, animator.gameObject.scene);
            _target = new GameObject("Wrist Target").transform;
            _target.SetParent(_targets.transform, false);
            _hint = new GameObject("Elbow Hint").transform;
            _hint.SetParent(_targets.transform, false);
            _target.SetPositionAndRotation(hand.position, hand.rotation);
            _hint.position = lower.position;
            _evaluation=new NativeArray<Vector3>(3,Allocator.Persistent);
            _rotation=new NativeArray<Quaternion>(1,Allocator.Persistent);
            var constraint = new TwoBoneIKConstraintJob
            {
                root = ReadWriteTransformHandle.Bind(animator, upper),
                mid = ReadWriteTransformHandle.Bind(animator, lower),
                tip = ReadWriteTransformHandle.Bind(animator, hand),
                target = ReadOnlyTransformHandle.Bind(animator, _target),
                hint = ReadOnlyTransformHandle.Bind(animator, _hint),
                targetOffset = AffineTransform.identity,
                jobWeight = FloatProperty.BindCustom(animator, "Grip.IK.Weight"),
                targetPositionWeight = FloatProperty.BindCustom(animator, "Grip.IK.Position"),
                targetRotationWeight = FloatProperty.BindCustom(animator, "Grip.IK.Rotation"),
                hintWeight = FloatProperty.BindCustom(animator, "Grip.IK.Hint")
            };
            _playable = AnimationScriptPlayable.Create(graph, new PerformerArmIKJob { Constraint = constraint,
                Evaluation=_evaluation, Rotation=_rotation, EvaluationMarker = FloatProperty.BindCustom(animator, "Grip.IK.Evaluated") }, 1);
            _playable.SetProcessInputs(true);
            if (!graph.Connect(input, 0, _playable, 0)) throw new InvalidOperationException("Cannot connect arm IK.");
            _playable.SetInputWeight(0, 1f);
        }

        public void SetTarget(Pose wrist, Vector3 hint, float weight)
        {
            _target.SetPositionAndRotation(wrist.position, wrist.rotation);
            _hint.position = hint;
            var job = _playable.GetJobData<PerformerArmIKJob>();
            job.Weight = Mathf.Clamp01(weight);
            _playable.SetJobData(job);
        }

        // Footwear floor support moves the rig after animation. Reconcile the arm with
        // the already blended stream wrist; do not blend twice or alter support/tuning.
        public void ReconcileAfterSupport()
        {
            if (!_playable.IsValid() || !_evaluation.IsCreated || _playable.GetJobData<PerformerArmIKJob>().Weight <= 0f) return;
            Vector3 target = _evaluation[1];
            if ((Hand.position-target).sqrMagnitude <= 1e-10f) return;
            Vector3 origin=Upper.position, direction=target-origin;
            float a=Vector3.Distance(origin,Lower.position), b=Vector3.Distance(Lower.position,Hand.position);
            float d=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            direction.Normalize();
            Vector3 pole=Lower.position-origin;
            pole-=direction*Vector3.Dot(pole,direction);
            if(pole.sqrMagnitude<1e-8f) pole=Vector3.ProjectOnPlane(_hint.position-origin,direction);
            pole.Normalize();
            float along=(a*a+d*d-b*b)/(2*d), across=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 knee=origin+direction*along+pole*across;
            Upper.rotation=Quaternion.FromToRotation(Lower.position-origin,knee-origin)*Upper.rotation;
            Lower.rotation=Quaternion.FromToRotation(Hand.position-Lower.position,target-Lower.position)*Lower.rotation;
            Hand.rotation=_rotation[0];
        }

        public void Dispose()
        {
            if (_playable.IsValid()) _playable.Destroy();
            if (_evaluation.IsCreated) _evaluation.Dispose();
            if (_rotation.IsCreated) _rotation.Dispose();
            if (_targets != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_targets);
                else UnityEngine.Object.DestroyImmediate(_targets);
            }
        }
    }
}
