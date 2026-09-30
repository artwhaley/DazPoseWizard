using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Importing
{
    public static class DazPoseAnimationClipGenerator
    {
        private static readonly HashSet<string> ExpressionExcludedBlendShapes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Breathe", "EX_Breathe", "Genesis8Female__EX_Breathe",
            "BreatheBelly", "EX_BreatheBelly", "Genesis8Female__EX_BreatheBelly",
            "eCTRLEyesClosedL", "eCTRLEyesClosedR",
            "Genesis8Female__eCTRLEyesClosedL", "Genesis8Female__eCTRLEyesClosedR"
        };
        public const string OutputFolder = "Assets/Generated/DazPoses/G8F";
        public const float DurationSeconds = 1f;
        public static DazPoseAnimationClipReport Generate(ResolvedUnityPose pose, bool replaceExisting)
        {
            if (pose == null || pose.Definition == null)
                throw new ArgumentNullException(nameof(pose), "A resolved G8F pose is required.");
            var assetPath = AssetPathFor(pose);
            var safeName = SanitizeAssetName(DazPoseJsonLoader.PoseName(pose.Definition.source.poseFile, pose.Definition.source.poseAssetId));
            return Generate(pose, assetPath, OutputFolder + "/" + safeName + ".report.json", replaceExisting);
        }

        public static DazPoseAnimationClipReport Generate(ResolvedUnityPose pose, string assetPath, string reportPath, bool replaceExisting)
        {
            if (pose == null || pose.Definition == null || pose.BindingRoot == null)
                throw new ArgumentNullException(nameof(pose), "A resolved G8F pose and common animation binding root are required.");

            assetPath = NormalizeAssetPath(assetPath);
            reportPath = NormalizeProjectRelativePath(reportPath);
            var safeName = Path.GetFileNameWithoutExtension(assetPath);
            var fullAssetPath = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullAssetPath));
            AssetDatabase.Refresh();

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null && !replaceExisting)
                throw new InvalidOperationException("AnimationClip already exists at " + assetPath + ". Confirm regeneration before replacing it.");

            var clip = new AnimationClip
            {
                name = safeName,
                frameRate = 30f,
                legacy = false
            };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            var diagnostics = new List<DazPoseAnimationBindingDiagnostic>();
            var morphDiagnostics = new List<DazPoseAnimationMorphBindingDiagnostic>();
            AnimationClip backup = null;
            DazPoseAnimationClipReport report = null;
            var existingGuid = existing == null ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var createdAsset = false;

            try
            {
                PopulateClip(pose, clip, diagnostics, morphDiagnostics);

                var candidateParity = DazPoseClipParityValidator.Validate(pose, clip);
                report = BuildReport(pose, clip, diagnostics, morphDiagnostics, candidateParity, assetPath);
                if (!candidateParity.Passed)
                {
                    report.generationFailure = "In-memory candidate failed parity: " + candidateParity.Summary;
                    WriteReport(reportPath, report);
                    throw new InvalidOperationException("Candidate AnimationClip failed direct-pose parity. The existing saved asset was left untouched. Detailed report: " + reportPath + ". " + candidateParity.Summary);
                }

                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clip, assetPath);
                    createdAsset = true;
                    clip = null;
                }
                else
                {
                    backup = UnityEngine.Object.Instantiate(existing);
                    backup.hideFlags = HideFlags.HideAndDontSave;
                    CopyClipContents(clip, existing);
                    EditorUtility.SetDirty(existing);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (savedClip == null) throw new InvalidOperationException("Unity saved the clip but could not reload " + assetPath + ".");

                var parity = DazPoseClipParityValidator.Validate(pose, savedClip);
                if (!parity.Passed)
                    throw new InvalidOperationException("Saved AnimationClip failed direct-pose parity. Detailed report: " + reportPath + ". " + parity.Summary);

                var currentGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(existingGuid) && !string.Equals(existingGuid, currentGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("AnimationClip asset identity changed during in-place regeneration. Previous GUID " + existingGuid + ", current GUID " + currentGuid + ".");

                EnsurePerformerPoseAsset(assetPath, savedClip);

                report = BuildReport(pose, savedClip, diagnostics, morphDiagnostics, parity, assetPath);
                report.assetGuid = currentGuid;
                report.generationPassed = true;
                WriteReport(reportPath, report);
                Debug.Log("DAZ Pose → Unity AnimationClip\n"
                    + "Source: " + Path.GetFileName(pose.SourcePoseJsonPath) + "\n"
                    + "Character: " + pose.CharacterName + " (Genesis 8 Female)\n"
                    + "Resolved bones: " + pose.Bones.Count + " | pose targets: " + pose.PoseTargetBoneCount + " | Rotation curves: " + report.rotationCurveCount
                    + " | Position curves: " + report.positionCurveCount + " | Scale curves: " + report.scaleCurveCount
                    + " | blendshape curves: " + report.blendShapeCurveCount + "\n"
                    + "Unresolved required bones: 0 | Ambiguous bones: 0\n"
                    + "Clip duration: " + report.durationSeconds.ToString("F1") + " sec | direct/clip parity: PASS at 0, 0.5, 1.0 sec\n"
                    + "Output: " + assetPath + " | report: " + reportPath);
                return report;
            }
            catch (Exception exception)
            {
                if (report == null) report = BuildReport(pose, clip, diagnostics, morphDiagnostics, new DazPoseClipParityResult(), assetPath);
                report.generationPassed = false;
                if (string.IsNullOrEmpty(report.generationFailure)) report.generationFailure = exception.Message;
                try { WriteReport(reportPath, report); }
                catch (Exception reportException) { Debug.LogError("Could not write DAZ Pose generation failure report: " + reportException.Message); }

                if (existing != null && backup != null)
                {
                    try
                    {
                        var assetToRestore = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                        if (assetToRestore == null) throw new InvalidOperationException("The existing AnimationClip could not be reloaded for rollback.");
                        CopyClipContents(backup, assetToRestore);
                        EditorUtility.SetDirty(assetToRestore);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogError("Could not restore the previous DAZ Pose AnimationClip after a failed regeneration: " + restoreException);
                    }
                }
                else if (createdAsset)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                throw;
            }
            finally
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
            }
        }

        internal static bool IsExpressionReservedBlendShape(string name) => ExpressionExcludedBlendShapes.Contains(name);

        internal static AnimationClip BuildExpressionCandidateClip(ResolvedUnityPose pose,
            out PerformerExpressionChannel[] channels, out string[] excludedChannels)
            => BuildExpressionCandidateClip(pose, out channels, out _, out excludedChannels);

        internal static AnimationClip BuildExpressionCandidateClip(ResolvedUnityPose pose,
            out PerformerExpressionChannel[] channels, out PerformerExpressionBoneChannel[] boneChannels,
            out string[] excludedChannels)
        {
            if (pose == null || pose.BindingRoot == null) throw new ArgumentNullException(nameof(pose));
            var clip = new AnimationClip { name = "DAZ Expression Candidate", frameRate = 30f, legacy = false };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            var channelList = new List<PerformerExpressionChannel>();
            var boneChannelList = new List<PerformerExpressionBoneChannel>();
            var excluded = new List<ResolvedUnityMorphBinding>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var pair in pose.MorphControls.SelectMany(control => control.Bindings.Select(binding => new { Control = control, Binding = binding }))
                             .OrderBy(item => item.Binding.RendererPath, StringComparer.Ordinal).ThenBy(item => item.Binding.BlendShapeName, StringComparer.Ordinal))
                {
                    ValidateMorphBinding(pose, pair.Control, pair.Binding);
                    if (ExpressionExcludedBlendShapes.Contains(pair.Binding.BlendShapeName)) { excluded.Add(pair.Binding); continue; }
                    var property = "blendShape." + pair.Binding.BlendShapeName;
                    var key = pair.Binding.RendererPath + "|" + property;
                    if (!emitted.Add(key)) throw new InvalidOperationException("More than one active DAZ control resolves to expression curve '" + key + "'.");
                    AddBlendShapeCurve(bindings, curves, pair.Binding.RendererPath, property, pair.Binding.UnityWeight);
                    channelList.Add(new PerformerExpressionChannel(pair.Binding.RendererPath, pair.Binding.BlendShapeName, pair.Binding.UnityWeight));
                }

                foreach (var bone in pose.Bones.OrderBy(item => item.AnimationPath, StringComparer.Ordinal).ThenBy(item => item.DazBoneId, StringComparer.Ordinal))
                {
                    var properties = PerformerExpressionBoneProperties.None;
                    if (bone.ExpressionHasPosition) properties |= PerformerExpressionBoneProperties.LocalPosition;
                    if (bone.ExpressionHasRotation) properties |= PerformerExpressionBoneProperties.LocalRotation;
                    if (properties == PerformerExpressionBoneProperties.None) continue;
                    if (IsForbiddenGazeBoneId(bone.DazBoneId)
                        || IsForbiddenGazeTransformPath(bone.AnimationPath))
                        throw new InvalidOperationException("Expression facial bone metadata cannot own gaze bone '" + bone.DazBoneId + "'.");
                    ValidateAnimationPath(pose, bone);
                    var transformPath = bone.AnimationPath;
                    if ((properties & PerformerExpressionBoneProperties.LocalPosition) != 0)
                    {
                        RequireFinite(bone.LocalPosition, bone.DazBoneId + " Expression local position");
                        var positionKey = transformPath + "|m_LocalPosition";
                        if (!emitted.Add(positionKey)) throw new InvalidOperationException("Duplicate Expression local-position channel '" + positionKey + "'.");
                        AddVector3(bindings, curves, transformPath, "m_LocalPosition", bone.LocalPosition);
                    }
                    if ((properties & PerformerExpressionBoneProperties.LocalRotation) != 0)
                    {
                        var localRotation = Normalize(bone.LocalRotation, bone.DazBoneId + " Expression local rotation");
                        var rotationKey = transformPath + "|m_LocalRotation";
                        if (!emitted.Add(rotationKey)) throw new InvalidOperationException("Duplicate Expression local-rotation channel '" + rotationKey + "'.");
                        AddVector4(bindings, curves, transformPath, "m_LocalRotation", localRotation);
                        boneChannelList.Add(new PerformerExpressionBoneChannel(transformPath, bone.DazBoneId,
                            properties, bone.LocalPosition, localRotation));
                        continue;
                    }
                    boneChannelList.Add(new PerformerExpressionBoneChannel(transformPath, bone.DazBoneId,
                        properties, bone.LocalPosition, bone.LocalRotation));
                }
                if (channelList.Count == 0 && boneChannelList.Count == 0)
                    throw new InvalidOperationException("This preset produced no usable Expression morph or facial-bone channels after sanitation. No active morph or facial articulation channel remains.");
                AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
                var candidateBindings = AnimationUtility.GetCurveBindings(clip);
                if (candidateBindings.Any(binding => !IsSupportedExpressionBinding(binding)))
                    throw new InvalidOperationException("Expression candidate contains a curve outside the supported blendshape and facial Transform properties.");
                channels = channelList.ToArray();
                boneChannels = boneChannelList.ToArray();
                excludedChannels = excluded.Select(binding => binding.RendererPath + "|" + binding.BlendShapeName + "|"
                    + binding.UnityWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                return clip;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        public static void GenerateExpression(ResolvedUnityPose pose, string assetPath, string reportPath, bool replaceExisting)
        {
            if (pose == null || pose.BindingRoot == null) throw new ArgumentNullException(nameof(pose));
            assetPath = NormalizeAssetPath(assetPath);
            reportPath = NormalizeProjectRelativePath(reportPath);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var occupiedClip = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (occupiedClip != null && existing == null)
                throw new InvalidOperationException("Another asset occupies the expected AnimationClip path " + assetPath + ".");
            if (existing != null && !replaceExisting) throw new InvalidOperationException("AnimationClip already exists at " + assetPath + ".");
            var clip = BuildExpressionCandidateClip(pose, out var channels, out var boneChannels, out var excluded);
            clip.name = Path.GetFileNameWithoutExtension(assetPath);
            var expectedCurveKeys = BuildExpressionCurveKeys(channels, boneChannels);
            var consideredMorphControlCount = pose.Definition == null || pose.Definition.figureControls == null
                ? pose.MorphControls.Count : pose.Definition.figureControls.Length;
            var fullAssetPath = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullAssetPath));
            AssetDatabase.Refresh();
            existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var existingGuid = existing == null ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var occupiedWrapper = AssetDatabase.LoadMainAssetAtPath(Path.ChangeExtension(assetPath, ".asset").Replace('\\', '/'));
            if (occupiedWrapper != null && !(occupiedWrapper is PerformerExpression))
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw new InvalidOperationException("Another asset occupies the expected PerformerExpression wrapper path.");
            }
            AnimationClip backup = null;
            PerformerExpression wrapperBackup = null;
            var wrapperPath = Path.ChangeExtension(assetPath, ".asset").Replace('\\', '/');
            var existingWrapper = AssetDatabase.LoadAssetAtPath<PerformerExpression>(wrapperPath);
            var wrapperGuid = existingWrapper == null ? string.Empty : AssetDatabase.AssetPathToGUID(wrapperPath);
            var wrapperCreated = existingWrapper == null;
            var fullReport = Path.Combine(ProjectRoot, reportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullReport));
            var previousReportExists = File.Exists(fullReport);
            var previousReport = previousReportExists ? File.ReadAllBytes(fullReport) : null;
            var reportWritten = false;
            try
            {
                if (existing == null) { AssetDatabase.CreateAsset(clip, assetPath); clip = null; }
                else
                {
                    backup = UnityEngine.Object.Instantiate(existing);
                    backup.hideFlags = HideFlags.HideAndDontSave;
                    CopyClipContents(clip, existing);
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (saved == null) throw new InvalidOperationException("Unity did not reload the saved Expression clip at " + assetPath + ".");
                var savedBindings = AnimationUtility.GetCurveBindings(saved);
                if (savedBindings.Any(binding => !IsSupportedExpressionBinding(binding)))
                    throw new InvalidOperationException("Saved Expression clip contains a curve outside the supported blendshape and facial Transform properties.");
                var savedKeys = new HashSet<string>(savedBindings.Select(ExpressionCurveKey), StringComparer.Ordinal);
                if (!savedKeys.SetEquals(expectedCurveKeys))
                    throw new InvalidOperationException("Saved Expression clip does not contain the exact expected blendshape and facial-bone curves.");
                if (existingWrapper != null)
                {
                    wrapperBackup = UnityEngine.Object.Instantiate(existingWrapper);
                    wrapperBackup.hideFlags = HideFlags.HideAndDontSave;
                }
                EnsurePerformerExpressionAsset(assetPath, saved, channels, boneChannels);
                ValidatePerformerExpressionAsset(wrapperPath, saved);
                EnsureGuidUnchanged(assetPath, existingGuid, "Expression clip");
                EnsureGuidUnchanged(wrapperPath, wrapperGuid, "PerformerExpression wrapper");

                File.WriteAllText(fullReport, JsonUtility.ToJson(new DazPoseExpressionImportReport
                {
                    generatedAtUtc = DateTime.UtcNow.ToString("O"), assetPath = assetPath,
                    assetKind = "Expression", sourceSkeletalChannelCountConsidered = pose.ActiveSkeletalChannelCount,
                    facialSkeletalChannelCountRetained = pose.RetainedFacialSkeletalChannelCount,
                    nonfacialSkeletalChannelCountIgnored = pose.IgnoredSkeletalChannelCount,
                    unsupportedFacialSkeletalChannelCount = pose.UnsupportedFacialSkeletalChannelCount,
                    sourceMorphControlCountConsidered = consideredMorphControlCount,
                    reservedControlCountExcluded = excluded.Select(value => value.Split('|')[1]).Distinct(StringComparer.Ordinal).Count(),
                    finalExpressionChannelCountEmitted = channels.Length + boneChannels.Length,
                    blendShapeChannelCountEmitted = channels.Length,
                    facialBoneChannelCountEmitted = boneChannels.Length,
                    blendShapeCurveCount = savedBindings.Count(binding => binding.type == typeof(SkinnedMeshRenderer)),
                    rotationCurveCount = savedBindings.Count(binding => binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)),
                    positionCurveCount = savedBindings.Count(binding => binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)),
                    totalCurveCount = savedBindings.Length,
                    sourceMorphControlCount = consideredMorphControlCount, emittedMorphCurveCount = channels.Length,
                    transformCurveCount = savedBindings.Count(binding => binding.type == typeof(Transform)),
                    emittedChannels = channels.Select(c => c.RendererPath + "|" + c.BlendShapeName + "|" + c.TargetWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
                    emittedBoneChannels = boneChannels.Select(c => c.DazBoneId + "|" + c.TransformPath + "|" + c.Properties
                        + "|" + c.TargetLocalPosition + "|" + c.TargetLocalRotation).ToArray(),
                    skeletalChannelDiagnostics = pose.SkeletalChannelDiagnostics ?? Array.Empty<string>(),
                    excludedChannels = excluded.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }, true));
                reportWritten = true;
            }
            catch
            {
                if (existing != null && backup != null)
                {
                    var restoreClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if (restoreClip != null) { CopyClipContents(backup, restoreClip); EditorUtility.SetDirty(restoreClip); }
                }
                else if (existing == null && AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);

                if (existingWrapper != null && wrapperBackup != null)
                {
                    var restoreWrapper = AssetDatabase.LoadAssetAtPath<PerformerExpression>(wrapperPath);
                    if (restoreWrapper != null) CopyExpressionWrapperContents(wrapperBackup, restoreWrapper);
                }
                else if (wrapperCreated && AssetDatabase.LoadAssetAtPath<PerformerExpression>(wrapperPath) != null)
                    AssetDatabase.DeleteAsset(wrapperPath);
                AssetDatabase.SaveAssets();
                throw;
            }
            finally
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
                if (wrapperBackup != null) UnityEngine.Object.DestroyImmediate(wrapperBackup);
                if (!reportWritten)
                {
                    if (previousReportExists) File.WriteAllBytes(fullReport, previousReport);
                    else if (File.Exists(fullReport)) File.Delete(fullReport);
                }
            }
        }

        private static void EnsurePerformerExpressionAsset(string animationAssetPath, AnimationClip clip,
            PerformerExpressionChannel[] channels, PerformerExpressionBoneChannel[] boneChannels)
        {
            var assetPath = Path.ChangeExtension(animationAssetPath, ".asset").Replace('\\', '/');
            var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>(assetPath);
            var occupied = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (occupied != null && expression == null) throw new InvalidOperationException("Another asset occupies " + assetPath + ".");
            var guid = expression == null ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            if (expression == null) { expression = ScriptableObject.CreateInstance<PerformerExpression>(); expression.name = Path.GetFileNameWithoutExtension(assetPath); AssetDatabase.CreateAsset(expression, assetPath); }
            var serialized = new SerializedObject(expression);
            serialized.FindProperty("clip").objectReferenceValue = clip;
            var channelProperty = serialized.FindProperty("channels");
            channelProperty.arraySize = channels.Length;
            for (var i = 0; i < channels.Length; i++)
            {
                var item = channelProperty.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("rendererPath").stringValue = channels[i].RendererPath;
                item.FindPropertyRelative("blendShapeName").stringValue = channels[i].BlendShapeName;
                item.FindPropertyRelative("targetWeight").floatValue = channels[i].TargetWeight;
            }
            var boneChannelProperty = serialized.FindProperty("boneChannels");
            boneChannelProperty.arraySize = boneChannels.Length;
            for (var i = 0; i < boneChannels.Length; i++)
            {
                var item = boneChannelProperty.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("transformPath").stringValue = boneChannels[i].TransformPath;
                item.FindPropertyRelative("dazBoneId").stringValue = boneChannels[i].DazBoneId;
                item.FindPropertyRelative("properties").intValue = (int)boneChannels[i].Properties;
                item.FindPropertyRelative("targetLocalPosition").vector3Value = boneChannels[i].TargetLocalPosition;
                item.FindPropertyRelative("targetLocalRotation").quaternionValue = boneChannels[i].TargetLocalRotation;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(expression); AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(guid) && guid != AssetDatabase.AssetPathToGUID(assetPath)) throw new InvalidOperationException("PerformerExpression GUID changed during regeneration.");
        }

        private static void ValidatePerformerExpressionAsset(string assetPath, AnimationClip clip)
        {
            var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>(assetPath);
            if (expression == null || expression.Clip != clip
                || ((expression.Channels == null || expression.Channels.Length == 0)
                    && (expression.BoneChannels == null || expression.BoneChannels.Length == 0)))
                throw new InvalidOperationException("PerformerExpression wrapper is missing its exact clip reference or generated channel metadata at " + assetPath + ".");
            if (expression.Channels.Any(channel => !IsFinite(channel.TargetWeight)))
                throw new InvalidOperationException("Saved PerformerExpression metadata contains a non-finite target weight.");
            if (!ExpressionMetadataMatchesClip(expression, clip))
                throw new InvalidOperationException("Saved PerformerExpression metadata does not have exact one-to-one parity with its blendshape and facial-bone clip curves.");
        }

        private static void CopyExpressionWrapperContents(PerformerExpression source, PerformerExpression destination)
        {
            var sourceSerialized = new SerializedObject(source);
            var destinationSerialized = new SerializedObject(destination);
            destinationSerialized.FindProperty("clip").objectReferenceValue = sourceSerialized.FindProperty("clip").objectReferenceValue;
            var sourceChannels = sourceSerialized.FindProperty("channels");
            var destinationChannels = destinationSerialized.FindProperty("channels");
            destinationChannels.arraySize = sourceChannels.arraySize;
            for (var index = 0; index < sourceChannels.arraySize; index++)
            {
                var sourceItem = sourceChannels.GetArrayElementAtIndex(index);
                var destinationItem = destinationChannels.GetArrayElementAtIndex(index);
                destinationItem.FindPropertyRelative("rendererPath").stringValue = sourceItem.FindPropertyRelative("rendererPath").stringValue;
                destinationItem.FindPropertyRelative("blendShapeName").stringValue = sourceItem.FindPropertyRelative("blendShapeName").stringValue;
                destinationItem.FindPropertyRelative("targetWeight").floatValue = sourceItem.FindPropertyRelative("targetWeight").floatValue;
            }
            var sourceBoneChannels = sourceSerialized.FindProperty("boneChannels");
            var destinationBoneChannels = destinationSerialized.FindProperty("boneChannels");
            destinationBoneChannels.arraySize = sourceBoneChannels.arraySize;
            for (var index = 0; index < sourceBoneChannels.arraySize; index++)
            {
                var sourceItem = sourceBoneChannels.GetArrayElementAtIndex(index);
                var destinationItem = destinationBoneChannels.GetArrayElementAtIndex(index);
                destinationItem.FindPropertyRelative("transformPath").stringValue = sourceItem.FindPropertyRelative("transformPath").stringValue;
                destinationItem.FindPropertyRelative("dazBoneId").stringValue = sourceItem.FindPropertyRelative("dazBoneId").stringValue;
                destinationItem.FindPropertyRelative("properties").intValue = sourceItem.FindPropertyRelative("properties").intValue;
                destinationItem.FindPropertyRelative("targetLocalPosition").vector3Value = sourceItem.FindPropertyRelative("targetLocalPosition").vector3Value;
                destinationItem.FindPropertyRelative("targetLocalRotation").quaternionValue = sourceItem.FindPropertyRelative("targetLocalRotation").quaternionValue;
            }
            destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(destination);
        }

        internal static bool IsSupportedExpressionBinding(EditorCurveBinding binding)
        {
            if (binding.type == typeof(SkinnedMeshRenderer))
                return binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)
                    && binding.propertyName.Length > "blendShape.".Length;
            if (binding.type != typeof(Transform)) return false;
            return binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.y"
                || binding.propertyName == "m_LocalPosition.z" || binding.propertyName == "m_LocalRotation.x"
                || binding.propertyName == "m_LocalRotation.y" || binding.propertyName == "m_LocalRotation.z"
                || binding.propertyName == "m_LocalRotation.w";
        }

        internal static bool ExpressionMetadataMatchesClip(PerformerExpression expression, AnimationClip clip)
        {
            if (expression == null || clip == null || expression.Clip != clip) return false;
            var channels = expression.Channels ?? Array.Empty<PerformerExpressionChannel>();
            var boneChannels = expression.BoneChannels ?? Array.Empty<PerformerExpressionBoneChannel>();
            if (channels.Length == 0 && boneChannels.Length == 0) return false;
            HashSet<string> expected;
            try { expected = BuildExpressionCurveKeys(channels, boneChannels); }
            catch (Exception) { return false; }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0
                || bindings.Any(binding => !IsSupportedExpressionBinding(binding))) return false;
            var actual = new HashSet<string>(bindings.Select(ExpressionCurveKey), StringComparer.Ordinal);
            if (actual.Count != bindings.Length || !actual.SetEquals(expected)) return false;

            foreach (var channel in channels)
            {
                var binding = EditorCurveBinding.FloatCurve(channel.RendererPath, typeof(SkinnedMeshRenderer), "blendShape." + channel.BlendShapeName);
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0
                    || curve.keys.Any(key => !IsFinite(key.value) || Mathf.Abs(key.value - channel.TargetWeight) > 0.001f)) return false;
            }
            foreach (var channel in boneChannels)
            {
                var properties = channel.Properties;
                if ((properties & PerformerExpressionBoneProperties.LocalPosition) != 0)
                {
                    if (!CurveValueMatches(clip, channel.TransformPath, "m_LocalPosition.x", channel.TargetLocalPosition.x)
                        || !CurveValueMatches(clip, channel.TransformPath, "m_LocalPosition.y", channel.TargetLocalPosition.y)
                        || !CurveValueMatches(clip, channel.TransformPath, "m_LocalPosition.z", channel.TargetLocalPosition.z)) return false;
                }
                if ((properties & PerformerExpressionBoneProperties.LocalRotation) != 0)
                {
                    var rotation = Normalize(channel.TargetLocalRotation, channel.DazBoneId);
                    if (!CurveRotationMatches(clip, channel.TransformPath, rotation)) return false;
                }
            }
            return true;
        }

        private static HashSet<string> BuildExpressionCurveKeys(PerformerExpressionChannel[] channels,
            PerformerExpressionBoneChannel[] boneChannels)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var morphChannels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in channels ?? Array.Empty<PerformerExpressionChannel>())
            {
                if (channel.RendererPath == null || string.IsNullOrWhiteSpace(channel.BlendShapeName)
                    || !IsFinite(channel.TargetWeight) || ExpressionExcludedBlendShapes.Contains(channel.BlendShapeName))
                    throw new InvalidOperationException("PerformerExpression has invalid or reserved blendshape metadata.");
                var morphKey = channel.RendererPath + "|" + channel.BlendShapeName;
                if (!morphChannels.Add(morphKey)) throw new InvalidOperationException("Duplicate PerformerExpression blendshape metadata '" + morphKey + "'.");
                AddExpectedCurve(keys, typeof(SkinnedMeshRenderer), channel.RendererPath, "blendShape." + channel.BlendShapeName);
            }

            var boneProperties = new HashSet<string>(StringComparer.Ordinal);
            var boneDescriptorPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in boneChannels ?? Array.Empty<PerformerExpressionBoneChannel>())
            {
                var properties = channel.Properties;
                if (string.IsNullOrWhiteSpace(channel.DazBoneId)
                    || channel.TransformPath == null
                    || properties == PerformerExpressionBoneProperties.None
                    || (properties & ~(PerformerExpressionBoneProperties.LocalPosition | PerformerExpressionBoneProperties.LocalRotation)) != 0
                    || IsForbiddenGazeBoneId(channel.DazBoneId) || IsForbiddenGazeTransformPath(channel.TransformPath)
                    || channel.TransformPath.IndexOf("[", StringComparison.Ordinal) >= 0
                    || channel.TransformPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("PerformerExpression has invalid facial-bone metadata or attempts to own a gaze bone.");
                if (!boneDescriptorPaths.Add(channel.TransformPath))
                    throw new InvalidOperationException("Duplicate PerformerExpression facial-bone descriptor path '" + channel.TransformPath + "'.");
                if ((properties & PerformerExpressionBoneProperties.LocalPosition) != 0)
                    RequireFinite(channel.TargetLocalPosition, channel.DazBoneId + " Expression target position");
                if ((properties & PerformerExpressionBoneProperties.LocalRotation) != 0)
                    Normalize(channel.TargetLocalRotation, channel.DazBoneId + " Expression target rotation");

                if ((properties & PerformerExpressionBoneProperties.LocalPosition) != 0)
                {
                    var key = channel.TransformPath + "|m_LocalPosition";
                    if (!boneProperties.Add(key)) throw new InvalidOperationException("Duplicate PerformerExpression bone property '" + key + "'.");
                    foreach (var axis in new[] { "x", "y", "z" })
                        AddExpectedCurve(keys, typeof(Transform), channel.TransformPath, "m_LocalPosition." + axis);
                }
                if ((properties & PerformerExpressionBoneProperties.LocalRotation) != 0)
                {
                    var key = channel.TransformPath + "|m_LocalRotation";
                    if (!boneProperties.Add(key)) throw new InvalidOperationException("Duplicate PerformerExpression bone property '" + key + "'.");
                    foreach (var axis in new[] { "x", "y", "z", "w" })
                        AddExpectedCurve(keys, typeof(Transform), channel.TransformPath, "m_LocalRotation." + axis);
                }
            }
            return keys;
        }

        private static void AddExpectedCurve(HashSet<string> keys, Type type, string path, string property)
        {
            if (!keys.Add(type.FullName + "|" + path + "|" + property))
                throw new InvalidOperationException("Duplicate PerformerExpression clip binding '" + path + "|" + property + "'.");
        }

        private static string ExpressionCurveKey(EditorCurveBinding binding)
            => binding.type.FullName + "|" + binding.path + "|" + binding.propertyName;

        private static bool CurveValueMatches(AnimationClip clip, string path, string property, float expected)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
            return curve != null && curve.length > 0 && IsFinite(expected)
                && curve.keys.All(key => IsFinite(key.value) && Mathf.Abs(key.value - expected) <= 0.001f);
        }

        private static bool CurveRotationMatches(AnimationClip clip, string path, Quaternion expected)
        {
            var x = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"));
            var y = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"));
            var z = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"));
            var w = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"));
            if (x == null || y == null || z == null || w == null
                || x.length == 0 || x.length != y.length || x.length != z.length || x.length != w.length) return false;
            for (var index = 0; index < x.length; index++)
            {
                if (Mathf.Abs(x.keys[index].time - y.keys[index].time) > 1e-6f
                    || Mathf.Abs(x.keys[index].time - z.keys[index].time) > 1e-6f
                    || Mathf.Abs(x.keys[index].time - w.keys[index].time) > 1e-6f) return false;
                var value = new Quaternion(x.keys[index].value, y.keys[index].value, z.keys[index].value, w.keys[index].value);
                if (!IsFinite(value) || Mathf.Abs(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w - 1f) > 0.002f
                    || Quaternion.Angle(value, expected) > 0.2f) return false;
            }
            return true;
        }

        private static void EnsureGuidUnchanged(string assetPath, string originalGuid, string description)
        {
            if (!string.IsNullOrEmpty(originalGuid) && originalGuid != AssetDatabase.AssetPathToGUID(assetPath))
                throw new InvalidOperationException(description + " GUID changed during regeneration.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Quaternion value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        private static bool IsForbiddenGazeBoneId(string boneId)
            => boneId == "head" || boneId == "lEye" || boneId == "rEye";

        private static bool IsForbiddenGazeTransformPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var separator = path.LastIndexOf('/', path.Length - 1);
            var finalSegment = separator >= 0 ? path.Substring(separator + 1) : path;
            return IsForbiddenGazeBoneId(finalSegment);
        }

        internal static AnimationClip BuildCandidateClip(ResolvedUnityPose pose)
        {
            if (pose == null || pose.Definition == null || pose.BindingRoot == null)
                throw new ArgumentNullException(nameof(pose), "A resolved pose and common binding root are required.");
            var clip = new AnimationClip { name = "DAZ Pose Candidate", frameRate = 30f, legacy = false };
            try
            {
                PopulateClip(pose, clip, new List<DazPoseAnimationBindingDiagnostic>(),
                    new List<DazPoseAnimationMorphBindingDiagnostic>());
                return clip;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        private static void PopulateClip(ResolvedUnityPose pose, AnimationClip clip,
            List<DazPoseAnimationBindingDiagnostic> diagnostics, List<DazPoseAnimationMorphBindingDiagnostic> morphDiagnostics)
        {
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            foreach (var bone in pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale)
                         .OrderBy(item => item.AnimationPath, StringComparer.Ordinal))
            {
                ValidateAnimationPath(pose, bone);
                var properties = new List<string>();
                if (bone.HasRotation)
                {
                    AddVector4(bindings, curves, bone.AnimationPath, "m_LocalRotation", Normalize(bone.LocalRotation, bone.DazBoneId));
                    properties.Add("m_LocalRotation.x/y/z/w");
                }
                if (bone.HasPosition)
                {
                    RequireFinite(bone.LocalPosition, bone.DazBoneId + " local position");
                    AddVector3(bindings, curves, bone.AnimationPath, "m_LocalPosition", bone.LocalPosition);
                    properties.Add("m_LocalPosition.x/y/z");
                }
                if (bone.HasScale)
                {
                    RequireFinite(bone.LocalScale, bone.DazBoneId + " local scale");
                    AddVector3(bindings, curves, bone.AnimationPath, "m_LocalScale", bone.LocalScale);
                    properties.Add("m_LocalScale.x/y/z");
                }
                diagnostics.Add(new DazPoseAnimationBindingDiagnostic
                {
                    dazBoneId = bone.DazBoneId,
                    dazBoneName = bone.DazBoneName,
                    unityPath = bone.AnimationPath,
                    properties = properties.ToArray(),
                    rotationSource = bone.HasRotation ? (bone.HasDazRotationChannel ? "DAZ-supported rotation channel, resolved as a complete local quaternion" : "resolved local rotation required by the proven world-pose conversion") : string.Empty,
                    positionSource = bone.HasPosition ? (bone.HasDazTranslationChannel ? "DAZ-supported translation channel, resolved to Unity local meters" : "resolved local position required by the proven world-center conversion") : string.Empty
                });
            }

            var emittedMorphCurves = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in pose.MorphControls.SelectMany(control => control.Bindings.Select(binding => new { Control = control, Binding = binding }))
                         .OrderBy(item => item.Binding.RendererPath, StringComparer.Ordinal)
                         .ThenBy(item => item.Binding.BlendShapeName, StringComparer.Ordinal))
            {
                ValidateMorphBinding(pose, pair.Control, pair.Binding);
                var property = "blendShape." + pair.Binding.BlendShapeName;
                var curveKey = pair.Binding.RendererPath + "|" + property;
                if (!emittedMorphCurves.Add(curveKey))
                    throw new InvalidOperationException("More than one active DAZ control resolves to the same Unity blendshape curve '" + curveKey + "'.");
                AddBlendShapeCurve(bindings, curves, pair.Binding.RendererPath, property, pair.Binding.UnityWeight);
                morphDiagnostics.Add(new DazPoseAnimationMorphBindingDiagnostic
                {
                    sourceControlName = pair.Control.SourceControlName,
                    rawControlId = pair.Control.RawControlId,
                    sourceValue = pair.Control.SourceValue,
                    rendererPath = pair.Binding.RendererPath,
                    meshName = pair.Binding.Renderer.sharedMesh.name,
                    blendShapeName = pair.Binding.BlendShapeName,
                    blendShapeIndex = pair.Binding.BlendShapeIndex,
                    blendShapeFrameCount = pair.Binding.FrameCount,
                    blendShapeFrameWeights = pair.Binding.FrameWeights,
                    unityWeight = pair.Binding.UnityWeight
                });
            }

            if (bindings.Count == 0)
                throw new InvalidOperationException("The resolved pose contains no driven Transform or blendshape values.");
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
        }

        private static DazPoseAnimationClipReport BuildReport(ResolvedUnityPose pose, AnimationClip clip,
            List<DazPoseAnimationBindingDiagnostic> diagnostics, List<DazPoseAnimationMorphBindingDiagnostic> morphDiagnostics,
            DazPoseClipParityResult parity, string assetPath)
        {
            var curveBindings = clip == null ? Array.Empty<EditorCurveBinding>() : AnimationUtility.GetCurveBindings(clip);
            var blendShapeCurves = curveBindings.Where(item => item.type == typeof(SkinnedMeshRenderer)
                && item.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)).ToArray();
            return new DazPoseAnimationClipReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                figureGeneration = "Genesis 8 Female",
                character = pose.CharacterName,
                animationRoot = pose.BindingRoot.name,
                skeletonRoot = pose.SkeletonRoot == null ? string.Empty : DazPoseTransformPath.Get(pose.CharacterRoot, pose.SkeletonRoot),
                bindingRoot = DazPoseTransformPath.Get(pose.CharacterRoot, pose.BindingRoot),
                sourcePoseJson = pose.SourcePoseJsonPath,
                sourceDazPosePath = pose.Definition.source.poseFile,
                sourcePoseAssetId = pose.Definition.source.poseAssetId,
                sourceFigureAssetId = pose.Definition.source.figureAssetId,
                generatedClipPath = assetPath,
                clipName = clip == null ? Path.GetFileNameWithoutExtension(assetPath) : clip.name,
                durationSeconds = clip == null ? 0f : clip.length,
                resolvedBoneCount = pose.Bones.Count,
                poseTargetBoneCount = pose.PoseTargetBoneCount,
                rotationBoneCount = pose.Bones.Count(item => item.HasRotation),
                positionBoneCount = pose.Bones.Count(item => item.HasPosition),
                scaleBoneCount = pose.Bones.Count(item => item.HasScale),
                rotationCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)),
                positionCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)),
                scaleCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal)),
                totalCurveCount = curveBindings.Length,
                unresolvedRequiredBones = pose.UnresolvedRequiredBoneCount,
                ambiguousBones = pose.AmbiguousBoneCount,
                directApplyParityPassed = parity.Passed,
                maximumRotationErrorDegrees = parity.MaximumRotationErrorDegrees,
                maximumPositionErrorMeters = parity.MaximumPositionErrorMeters,
                maximumScaleError = parity.MaximumScaleError,
                maximumBlendShapeWeightError = parity.MaximumBlendShapeWeightError,
                paritySampleTimes = parity.SampleTimes,
                warnings = pose.Warnings,
                bindings = diagnostics.ToArray(),
                morphBindings = morphDiagnostics.ToArray(),
                morphControlCount = pose.MorphControls.Count,
                morphBindingCount = pose.MorphControls.Sum(item => item.Bindings.Count),
                blendShapeCurveCount = blendShapeCurves.Length,
                resolvedMorphNames = pose.MorphControls.Select(item => item.SourceControlName).OrderBy(item => item, StringComparer.Ordinal).ToArray()
            };
        }

        private static void CopyClipContents(AnimationClip source, AnimationClip destination)
        {
            var oldFloatBindings = AnimationUtility.GetCurveBindings(destination);
            if (oldFloatBindings.Length > 0)
                AnimationUtility.SetEditorCurves(destination, oldFloatBindings, new AnimationCurve[oldFloatBindings.Length]);

            foreach (var oldReferenceBinding in AnimationUtility.GetObjectReferenceCurveBindings(destination))
                AnimationUtility.SetObjectReferenceCurve(destination, oldReferenceBinding, null);

            var newBindings = AnimationUtility.GetCurveBindings(source);
            var newCurves = newBindings.Select(binding => AnimationUtility.GetEditorCurve(source, binding)).ToArray();
            AnimationUtility.SetEditorCurves(destination, newBindings, newCurves);
            AnimationUtility.SetAnimationEvents(destination, AnimationUtility.GetAnimationEvents(source));
            destination.frameRate = source.frameRate;
            destination.legacy = source.legacy;
            destination.wrapMode = source.wrapMode;
            destination.name = source.name;
            destination.EnsureQuaternionContinuity();
        }

        private static void EnsurePerformerPoseAsset(string animationAssetPath, AnimationClip clip)
        {
            var poseAssetPath = Path.ChangeExtension(animationAssetPath, ".asset").Replace('\\', '/');
            var pose = AssetDatabase.LoadAssetAtPath<PerformerPose>(poseAssetPath);
            var existingAsset = AssetDatabase.LoadMainAssetAtPath(poseAssetPath);
            if (existingAsset != null && pose == null)
                throw new InvalidOperationException("Cannot create the generated PerformerPose because another asset already occupies " + poseAssetPath + ".");

            var existingGuid = pose == null ? string.Empty : AssetDatabase.AssetPathToGUID(poseAssetPath);
            if (pose == null)
            {
                pose = ScriptableObject.CreateInstance<PerformerPose>();
                pose.name = Path.GetFileNameWithoutExtension(animationAssetPath);
                AssetDatabase.CreateAsset(pose, poseAssetPath);
            }

            var serializedPose = new SerializedObject(pose);
            var clipProperty = serializedPose.FindProperty("clip");
            if (clipProperty == null)
                throw new InvalidOperationException("PerformerPose has no serialized clip field at " + poseAssetPath + ".");
            if (clipProperty.objectReferenceValue != clip)
            {
                clipProperty.objectReferenceValue = clip;
                serializedPose.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pose);
                AssetDatabase.SaveAssets();
            }

            var updatedPose = AssetDatabase.LoadAssetAtPath<PerformerPose>(poseAssetPath);
            if (updatedPose == null || updatedPose.Clip != clip)
                throw new InvalidOperationException("Generated PerformerPose does not reference the regenerated clip at " + poseAssetPath + ".");

            var currentGuid = AssetDatabase.AssetPathToGUID(poseAssetPath);
            if (!string.IsNullOrEmpty(existingGuid) && !string.Equals(existingGuid, currentGuid, StringComparison.Ordinal))
                throw new InvalidOperationException("PerformerPose asset identity changed during clip regeneration. Previous GUID "
                    + existingGuid + ", current GUID " + currentGuid + ".");
        }

        private static void WriteReport(string reportPath, DazPoseAnimationClipReport report)
        {
            var reportFullPath = Path.Combine(ProjectRoot, reportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath));
            File.WriteAllText(reportFullPath, JsonUtility.ToJson(report, true));
        }

        public static AnimationClip LoadClipFromAbsolutePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return null;
            var fullPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            var assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a generated .anim inside this Unity project's Assets folder.");
            var assetPath = "Assets/" + fullPath.Substring(assetsRoot.Length + 1);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        public static string AssetPathFor(ResolvedUnityPose pose)
        {
            var safeName = SanitizeAssetName(DazPoseJsonLoader.PoseName(pose.Definition.source.poseFile, pose.Definition.source.poseAssetId));
            return OutputFolder + "/" + safeName + ".anim";
        }

        public static string SanitizeAssetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "DAZ Pose";
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\n', '\r' };
            var clean = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().Trim('.');
            while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
            return string.IsNullOrWhiteSpace(clean) ? "DAZ Pose" : clean;
        }

        private static void ValidateAnimationPath(ResolvedUnityPose pose, ResolvedBonePose bone)
        {
            if (bone.Transform == null || bone.AnimationPath == null || bone.AnimationPath == "<outside-root>")
                throw new InvalidOperationException("DAZ bone '" + bone.DazBoneId + "' is not beneath the common character binding root.");
            if (bone.AnimationPath.IndexOf("[", StringComparison.Ordinal) >= 0 || bone.AnimationPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Animation binding path contains a scene/sibling index: " + bone.AnimationPath);
            if (bone.Transform != pose.BindingRoot && !bone.Transform.IsChildOf(pose.BindingRoot))
                throw new InvalidOperationException("Animation binding for '" + bone.DazBoneId + "' escapes the common character binding root.");
        }

        private static void ValidateMorphBinding(ResolvedUnityPose pose, ResolvedUnityMorphControl control, ResolvedUnityMorphBinding binding)
        {
            if (binding.Renderer == null || binding.Renderer.sharedMesh == null)
                throw new InvalidOperationException("Resolved morph '" + control.SourceControlName + "' has no live SkinnedMeshRenderer or mesh.");
            if (binding.Renderer.transform != pose.BindingRoot && !binding.Renderer.transform.IsChildOf(pose.BindingRoot))
                throw new InvalidOperationException("Morph renderer path escapes the common animation binding root for '" + control.SourceControlName + "'.");
            var expectedPath = AnimationUtility.CalculateTransformPath(binding.Renderer.transform, pose.BindingRoot);
            if (!string.Equals(expectedPath, binding.RendererPath, StringComparison.Ordinal)
                || binding.RendererPath.IndexOf("[", StringComparison.Ordinal) >= 0
                || binding.RendererPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Morph renderer binding path is not a stable relative path for '" + control.SourceControlName + "': " + binding.RendererPath + ".");
            var mesh = binding.Renderer.sharedMesh;
            if (binding.BlendShapeIndex < 0 || binding.BlendShapeIndex >= mesh.blendShapeCount
                || !string.Equals(mesh.GetBlendShapeName(binding.BlendShapeIndex), binding.BlendShapeName, StringComparison.Ordinal))
                throw new InvalidOperationException("Imported blendshape identity changed after resolving '" + control.SourceControlName + "'.");
            RequireFinite(binding.UnityWeight, control.SourceControlName + " blendshape weight");
        }

        private static void AddVector3(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, Vector3 value)
        {
            AddCurve(bindings, curves, path, property + ".x", value.x);
            AddCurve(bindings, curves, path, property + ".y", value.y);
            AddCurve(bindings, curves, path, property + ".z", value.z);
        }

        private static void AddVector4(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, Quaternion value)
        {
            AddCurve(bindings, curves, path, property + ".x", value.x);
            AddCurve(bindings, curves, path, property + ".y", value.y);
            AddCurve(bindings, curves, path, property + ".z", value.z);
            AddCurve(bindings, curves, path, property + ".w", value.w);
        }

        private static void AddCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float value)
        {
            RequireFinite(value, property);
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(DurationSeconds, value));
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
            curves.Add(curve);
        }

        private static void AddBlendShapeCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float value)
        {
            RequireFinite(value, property);
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(DurationSeconds, value));
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), property));
            curves.Add(curve);
        }

        private static Quaternion Normalize(Quaternion rotation, string boneId)
        {
            RequireFinite(rotation, boneId + " local rotation");
            var norm = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
            if (norm < 1e-8f) throw new InvalidOperationException("DAZ bone '" + boneId + "' resolved to a zero-length quaternion.");
            return new Quaternion(rotation.x / norm, rotation.y / norm, rotation.z / norm, rotation.w / norm);
        }

        private static void RequireFinite(Vector3 value, string description)
        {
            RequireFinite(value.x, description + ".x");
            RequireFinite(value.y, description + ".y");
            RequireFinite(value.z, description + ".z");
        }

        private static void RequireFinite(Quaternion value, string description)
        {
            RequireFinite(value.x, description + ".x");
            RequireFinite(value.y, description + ".y");
            RequireFinite(value.z, description + ".z");
            RequireFinite(value.w, description + ".w");
        }

        private static void RequireFinite(float value, string description)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("Non-finite animation value at " + description + ".");
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static string NormalizeAssetPath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || !normalized.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                || normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidOperationException("Animation output must be an Assets-relative .anim path without traversal segments.");
            return normalized;
        }

        private static string NormalizeProjectRelativePath(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized) || normalized.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(normalized)
                || normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidOperationException("Animation report path must be project-relative and cannot contain traversal segments.");
            return normalized;
        }
    }

    [Serializable]
    internal sealed class DazPoseExpressionImportReport
    {
        public string generatedAtUtc;
        public string assetPath;
        public int sourceMorphControlCount;
        public int emittedMorphCurveCount;
        public int transformCurveCount;
        public string assetKind;
        public int sourceSkeletalChannelCountConsidered;
        public int facialSkeletalChannelCountRetained;
        public int nonfacialSkeletalChannelCountIgnored;
        public int unsupportedFacialSkeletalChannelCount;
        public int sourceMorphControlCountConsidered;
        public int reservedControlCountExcluded;
        public int finalExpressionChannelCountEmitted;
        public int blendShapeChannelCountEmitted;
        public int facialBoneChannelCountEmitted;
        public int blendShapeCurveCount;
        public int rotationCurveCount;
        public int positionCurveCount;
        public int totalCurveCount;
        public string[] emittedChannels;
        public string[] emittedBoneChannels;
        public string[] skeletalChannelDiagnostics;
        public string[] excludedChannels;
    }
}
