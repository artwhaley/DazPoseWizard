using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    internal static class PerformerExpressionRuntimeSelfTests
    {
        private static readonly FieldInfo ClipField = typeof(PerformerExpression).GetField("clip", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ChannelsField = typeof(PerformerExpression).GetField("channels", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BoneChannelsField = typeof(PerformerExpression).GetField("boneChannels", BindingFlags.Instance | BindingFlags.NonPublic);
        private const float PositionTolerance = 0.0001f;
        private const float RotationToleranceDegrees = 0.02f;

        public static string[] Run()
        {
            var failures = new List<string>();
            var root = new GameObject("P081ExpressionRuntimeSelfTest");
            var animator = root.AddComponent<Animator>();
            var upperFace = Child(root.transform, "upperFaceRig");
            var brow = Child(upperFace, "brow");
            var lowerJaw = Child(root.transform, "lowerJaw");
            var jaw = Child(lowerJaw, "jaw");
            var graph = PlayableGraph.Create("P0.8.1 Expression Runtime Self Test");
            var expressionClip = new AnimationClip { name = "P081ExpressionRuntimeTestClip" };
            PerformerExpressionLayer layer = null;
            NativeArray<TransformStreamHandle> handles = default;
            NativeArray<Vector3> basePositions = default;
            NativeArray<Quaternion> baseRotations = default;
            PerformerExpression expressionA = null;
            PerformerExpression expressionB = null;
            PerformerExpression expressionC = null;
            var forbiddenExpressions = new List<PerformerExpression>();

            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                handles = new NativeArray<TransformStreamHandle>(new[]
                {
                    animator.BindStreamTransform(brow), animator.BindStreamTransform(jaw)
                }, Allocator.Persistent);
                basePositions = new NativeArray<Vector3>(new[]
                {
                    new Vector3(0.1f, 0.2f, 0.3f), new Vector3(-0.1f, 0.05f, 0.2f)
                }, Allocator.Persistent);
                baseRotations = new NativeArray<Quaternion>(new[]
                {
                    Quaternion.Euler(2f, 5f, -3f), Quaternion.Euler(-1f, 4f, 2f)
                }, Allocator.Persistent);
                var basePlayable = AnimationScriptPlayable.Create(graph, new ExpressionSelfTestBaseJob
                {
                    Handles = handles, Positions = basePositions, Rotations = baseRotations
                }, 0);
                layer = new PerformerExpressionLayer(animator, graph, basePlayable);
                var output = AnimationPlayableOutput.Create(graph, "P0.8.1 Expression Output", animator);
                output.SetSourcePlayable(layer.OutputPlayable);

                var targetPositionA = new Vector3(0.4f, -0.2f, 0.15f);
                var targetRotationA = Quaternion.Euler(18f, -12f, 7f);
                var targetJawRotationA = Quaternion.Euler(8f, 0f, -5f);
                var targetJawRotationB = Quaternion.Euler(-14f, 3f, 11f);
                var targetBrowRotationC = Quaternion.Euler(-6f, 21f, 4f);
                expressionA = CreateExpression(expressionClip, new[]
                {
                    Bone("upperFaceRig/brow", "testBrow", PerformerExpressionBoneProperties.LocalPosition | PerformerExpressionBoneProperties.LocalRotation,
                        targetPositionA, targetRotationA),
                    Bone("lowerJaw/jaw", "testJaw", PerformerExpressionBoneProperties.LocalRotation,
                        Vector3.zero, targetJawRotationA)
                });
                expressionB = CreateExpression(expressionClip, new[]
                {
                    Bone("lowerJaw/jaw", "testJaw", PerformerExpressionBoneProperties.LocalRotation,
                        Vector3.zero, targetJawRotationB)
                });
                expressionC = CreateExpression(expressionClip, new[]
                {
                    Bone("upperFaceRig/brow", "testBrow", PerformerExpressionBoneProperties.LocalRotation,
                        Vector3.zero, targetBrowRotationC)
                });
                foreach (var forbiddenId in new[] { "head", "lEye", "rEye" })
                {
                    forbiddenExpressions.Add(CreateExpression(expressionClip, new[]
                    {
                        Bone("upperFaceRig/brow", forbiddenId, PerformerExpressionBoneProperties.LocalRotation,
                            Vector3.zero, targetBrowRotationC)
                    }));
                }

                graph.Play();
                Evaluate(graph);
                var incomingPosition = basePositions[0];
                var incomingRotation = baseRotations[0];
                layer.SetExpression(expressionA, 0f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, incomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, incomingRotation) <= RotationToleranceDegrees,
                    "zero intensity returns the exact incoming facial position and rotation", failures);

                layer.SetExpression(expressionA, 1f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, targetPositionA) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, targetRotationA) <= RotationToleranceDegrees
                    && Quaternion.Angle(jaw.localRotation, targetJawRotationA) <= RotationToleranceDegrees,
                    "full intensity reaches imported position and rotation targets", failures);

                layer.SetExpression(expressionA, 0.5f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, Vector3.Lerp(incomingPosition, targetPositionA, 0.5f)) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, Quaternion.Lerp(incomingRotation, targetRotationA, 0.5f)) <= RotationToleranceDegrees,
                    "partial intensity blends from the live incoming position and rotation", failures);

                var nextIncomingPosition = new Vector3(-0.3f, 0.12f, 0.08f);
                var nextIncomingRotation = Quaternion.Euler(-9f, 14f, 3f);
                basePositions[0] = nextIncomingPosition;
                baseRotations[0] = nextIncomingRotation;
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, Vector3.Lerp(nextIncomingPosition, targetPositionA, 0.5f)) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, Quaternion.Lerp(nextIncomingRotation, targetRotationA, 0.5f)) <= RotationToleranceDegrees,
                    "partial Expression follows changes to its live incoming base", failures);

                layer.SetExpression(expressionB, 1f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, nextIncomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, nextIncomingRotation) <= RotationToleranceDegrees
                    && Quaternion.Angle(jaw.localRotation, targetJawRotationB) <= RotationToleranceDegrees,
                    "disjoint retarget releases A-only facial properties and applies B-only rotation", failures);

                layer.SetExpression(expressionA, 1f, 0f);
                Evaluate(graph);
                layer.SetExpression(expressionB, 1f, 0.6f);
                layer.Advance(0.08f);
                Evaluate(graph);
                var beforeInterruptPosition = brow.localPosition;
                var beforeInterruptRotation = brow.localRotation;
                var beforeInterruptJaw = jaw.localRotation;
                layer.SetExpression(expressionC, 1f, 0.6f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, beforeInterruptPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, beforeInterruptRotation) <= RotationToleranceDegrees
                    && Quaternion.Angle(jaw.localRotation, beforeInterruptJaw) <= RotationToleranceDegrees,
                    "A to B to C interruption preserves facial transforms at the retarget point", failures);
                layer.Advance(1f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, nextIncomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, targetBrowRotationC) <= RotationToleranceDegrees
                    && Quaternion.Angle(jaw.localRotation, baseRotations[1]) <= RotationToleranceDegrees,
                    "interrupted completion drops stale A and B contributions", failures);

                for (var index = 0; index < 8; index++)
                {
                    layer.SetExpression(index % 2 == 0 ? expressionA : expressionB, 1f, 0.5f);
                    layer.Advance(0.03f);
                    Evaluate(graph);
                }
                layer.SetExpression(expressionC, 1f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, nextIncomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, targetBrowRotationC) <= RotationToleranceDegrees
                    && Quaternion.Angle(jaw.localRotation, baseRotations[1]) <= RotationToleranceDegrees,
                    "repeated interruptions do not leak stale facial contributions", failures);

                var rejectedForbiddenTargets = 0;
                foreach (var forbidden in forbiddenExpressions)
                {
                    try { layer.SetExpression(forbidden, 1f, 0f); }
                    catch (InvalidOperationException exception)
                    {
                        if (exception.Message.Contains("gaze bone")) rejectedForbiddenTargets++;
                    }
                }
                Check(rejectedForbiddenTargets == 3, "runtime rejects Expression descriptors targeting head and both eyes", failures);

                layer.SetBypassedForAcceptance(true);
                layer.SetExpression(expressionA, 1f, 0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, nextIncomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, nextIncomingRotation) <= RotationToleranceDegrees,
                    "bypass preserves exact incoming facial transforms", failures);
                layer.SetBypassedForAcceptance(false);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, new Vector3(0.4f, -0.2f, 0.15f)) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, targetRotationA) <= RotationToleranceDegrees,
                    "removing bypass restores the requested facial Expression", failures);

                var finalIncomingPosition = new Vector3(0.07f, -0.16f, 0.22f);
                var finalIncomingRotation = Quaternion.Euler(4f, -17f, 6f);
                basePositions[0] = finalIncomingPosition;
                baseRotations[0] = finalIncomingRotation;
                layer.Clear(0f);
                Evaluate(graph);
                Check(Vector3.Distance(brow.localPosition, finalIncomingPosition) <= PositionTolerance
                    && Quaternion.Angle(brow.localRotation, finalIncomingRotation) <= RotationToleranceDegrees,
                    "ClearExpression reveals the current live incoming pose", failures);
            }
            catch (Exception exception)
            {
                failures.Add("Synthetic facial Expression runtime check threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                layer?.Dispose();
                if (layer != null)
                    Check(!layer.HasNativeAllocations && !layer.OutputPlayable.IsValid(),
                        "Expression layer disposal releases native arrays and its playable", failures);
                if (graph.IsValid()) graph.Destroy();
                if (handles.IsCreated) handles.Dispose();
                if (basePositions.IsCreated) basePositions.Dispose();
                if (baseRotations.IsCreated) baseRotations.Dispose();
                if (expressionA != null) UnityEngine.Object.Destroy(expressionA);
                if (expressionB != null) UnityEngine.Object.Destroy(expressionB);
                if (expressionC != null) UnityEngine.Object.Destroy(expressionC);
                foreach (var forbidden in forbiddenExpressions)
                    if (forbidden != null) UnityEngine.Object.Destroy(forbidden);
                if (expressionClip != null) UnityEngine.Object.Destroy(expressionClip);
                UnityEngine.Object.Destroy(root);
            }
            return failures.ToArray();
        }

        private static PerformerExpression CreateExpression(AnimationClip clip, PerformerExpressionBoneChannel[] bones)
        {
            if (ClipField == null || ChannelsField == null || BoneChannelsField == null)
                throw new MissingFieldException("PerformerExpression acceptance metadata fields changed.");
            var expression = ScriptableObject.CreateInstance<PerformerExpression>();
            ClipField.SetValue(expression, clip);
            ChannelsField.SetValue(expression, Array.Empty<PerformerExpressionChannel>());
            BoneChannelsField.SetValue(expression, bones);
            return expression;
        }

        private static PerformerExpressionBoneChannel Bone(string path, string id,
            PerformerExpressionBoneProperties properties, Vector3 position, Quaternion rotation)
            => new PerformerExpressionBoneChannel(path, id, properties, position, rotation);

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void Evaluate(PlayableGraph graph) => graph.Evaluate(0f);

        private static void Check(bool condition, string label, List<string> failures)
        {
            if (!condition) failures.Add(label + " failed.");
        }

        private struct ExpressionSelfTestBaseJob : IAnimationJob
        {
            [ReadOnly] public NativeArray<TransformStreamHandle> Handles;
            [ReadOnly] public NativeArray<Vector3> Positions;
            [ReadOnly] public NativeArray<Quaternion> Rotations;

            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if (!stream.isValid || !Handles.IsCreated) return;
                for (var index = 0; index < Handles.Length; index++)
                {
                    var handle = Handles[index];
                    if (!handle.IsValid(stream)) continue;
                    handle.SetLocalPosition(stream, Positions[index]);
                    handle.SetLocalRotation(stream, Rotations[index]);
                }
            }
        }
    }
}
