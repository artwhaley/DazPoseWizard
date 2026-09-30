using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrazyMinnow.SALSA;
using DazPose.Performer;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Importing
{
    internal static class PerformerLipSyncAudit
    {
        private const string ValidationModelPath = "Assets/TestCharacter/lara.fbx";
        private const string RequiredMorphManifestPath = ".dazposewizard/required-morphs.json";
        private const string ExportRulesPath = ".dazposewizard/DazPoseWizard-MorphExportRules.csv";

        public static string WriteAudit(SuccubusPerformer performer)
        {
            if (performer == null) throw new ArgumentNullException(nameof(performer));
            var report = BuildReport(performer);
            var reportPath = Path.Combine(ProjectRoot, ".dazposewizard", "reports", "P0.9B-lipsync-audit.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            Debug.Log("P0.9B SALSA lip-sync audit written to " + reportPath + "\n" + report, performer);
            return reportPath;
        }

        private static string BuildReport(SuccubusPerformer performer)
        {
            var report = new StringBuilder();
            var animator = performer.GetComponent<Animator>();
            var bridge = performer.GetComponent<PerformerSalsaLipSync>();
            var salsa = performer.GetComponent<Salsa>();
            var queueProcessor = performer.GetComponent<QueueProcessor>();
            var speechSource = performer.SpeechAudioSource;
            report.AppendLine("P0.9B Lara SALSA Realtime Lip-Sync Audit");
            report.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Unity version: " + Application.unityVersion);
            report.AppendLine("SALSA version: " + ReadVersion());
            report.AppendLine("Validation scene: " + SceneManager.GetActiveScene().path);
            report.AppendLine("Validation model asset: " + ValidationModelPath);
            report.AppendLine("Performer root: " + GetScenePath(performer.transform));
            report.AppendLine("AudioSource identity: " + (speechSource == null ? "<missing>" : GetScenePath(speechSource.transform)));
            report.AppendLine("AudioSource is head-mounted: " + (speechSource != null && speechSource.transform.parent != null
                && string.Equals(speechSource.transform.parent.name, "head", StringComparison.Ordinal)));
            if (speechSource != null)
            {
                report.AppendLine("AudioSource global identity: " + GlobalObjectId.GetGlobalObjectIdSlow(speechSource));
                report.AppendLine("Audio settings: volume=" + speechSource.volume.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + " pitch=" + speechSource.pitch.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + " spatialBlend=" + speechSource.spatialBlend.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + " mixer=" + (speechSource.outputAudioMixerGroup == null ? "<none>" : speechSource.outputAudioMixerGroup.name)
                    + " rolloff=" + speechSource.rolloffMode + " min=" + speechSource.minDistance
                    + " max=" + speechSource.maxDistance + " doppler=" + speechSource.dopplerLevel
                    + " spatialize=" + speechSource.spatialize + " playOnAwake=" + speechSource.playOnAwake
                    + " loop=" + speechSource.loop);
            }

            report.AppendLine();
            report.AppendLine("Exact speech catalog (indexes resolved by name at runtime/editor setup):");
            report.AppendLine("Candidate preview: 18 actual Lara eCTRLv full-form shapes, 17 jaw-only variants, and 17 lips-only variants were previewed at 25 and 50 Unity units from front and three-quarter views; the selected eight full-form shapes were also checked at 100 units, which was too exaggerated.");
            report.AppendLine("Scale contract: SALSA maxShape is a normalized 0–1 amount. The current SALSA maximum of 1 maps to Unity blendshape weight 100; the previous maximum of 0.5 mapped to 50. Writing 50 into SALSA maxShape overshoots the intended Unity weight by 100x.");
            report.AppendLine("Live-strength tuning: the maximum was doubled to 1 SALSA amount (Unity weight 100) after the 0.5 setting looked too subtle. A static full-weight preview at 100 looked exaggerated, so verify actual audio-driven weights and appearance in Play Mode before further increasing it.");
            report.AppendLine("Candidate selection: W, F, T, TH, OW, EE, UW, and AA retain broad visible lip, dental, rounding, and jaw-opening forms. M was omitted because amplitude-only triggering cannot distinguish a bilabial closure from low energy or silence. SH, S, L, K, IY, IH, ER, and EH were omitted because their distinctions are phoneme-specific and cannot be identified from amplitude alone.");
            if (animator == null)
                report.AppendLine("FAIL: Animator is missing.");
            else if (!PerformerLipSyncMorphCatalog.TryResolveBindings(animator, out var bindings, out var bindingFailure))
                report.AppendLine("FAIL: " + bindingFailure);
            else
            {
                foreach (var binding in bindings)
                    report.AppendLine(binding.Definition.Viseme + "\t" + PerformerLipSyncMorphCatalog.RendererPath
                        + "\t" + binding.Index + "\t" + binding.Definition.BlendShapeName
                        + "\tsource=" + binding.Definition.SourceControlName
                        + "\trestSALSA=" + binding.Definition.RestSalsaAmount
                        + "\tmaxSALSA=" + binding.Definition.MaximumSalsaAmount
                        + "\tmaxUnity=" + binding.Definition.MaximumUnityWeight
                        + "\ton/hold/off=" + binding.Definition.DurationOn + "/" + binding.Definition.DurationHold
                        + "/" + binding.Definition.DurationOff);
            }

            report.AppendLine();
            report.AppendLine("SALSA setup:");
            report.AppendLine("Salsa components under performer: " + performer.GetComponentsInChildren<Salsa>(true).Length);
            report.AppendLine("QueueProcessor components under performer: " + performer.GetComponentsInChildren<QueueProcessor>(true).Length);
            report.AppendLine("EmoteR components under performer: " + performer.GetComponentsInChildren<Emoter>(true).Length);
            report.AppendLine("SALSA Eyes components under performer: " + performer.GetComponentsInChildren<Eyes>(true).Length);
            report.AppendLine("SALSA internal analysis: " + (salsa != null && !salsa.useExternalAnalysis));
            report.AppendLine("SALSA global persistence disabled: " + (salsa != null && !salsa.usePersistence));
            report.AppendLine("SALSA audio reference matches SpeechAudioSource: " + (salsa != null && salsa.audioSrc == speechSource));
            report.AppendLine("SALSA queue reference matches root QueueProcessor: " + (salsa != null && salsa.queueProcessor == queueProcessor));
            report.AppendLine("QueueProcessor enabled: " + (queueProcessor != null && queueProcessor.enabled));
            report.AppendLine("SALSA configReady: " + (salsa != null && salsa.configReady));
            report.AppendLine("Bridge present: " + (bridge != null));
            report.AppendLine("Bridge validation: " + (bridge == null ? "FAIL: bridge missing"
                : bridge.ValidateConfiguration(out var bridgeFailure) ? "PASS" : "FAIL: " + bridgeFailure));
            if (salsa != null)
            {
                report.AppendLine("Viseme count: " + (salsa.visemes == null ? 0 : salsa.visemes.Count));
                report.AppendLine("Analysis settings: autoAdjust=" + salsa.autoAdjustAnalysis + " delay=" + salsa.audioUpdateDelay
                    + " lo=" + salsa.loCutoff + " hi=" + salsa.hiCutoff + " advanced=" + salsa.useAdvDyn
                    + " primaryBias=" + salsa.advDynPrimaryBias + " jitter=" + salsa.useAdvDynJitter
                    + " jitterAmount=" + salsa.advDynJitterAmount + " jitterProbability=" + salsa.advDynJitterProb
                    + " secondaryMixEnabled=" + salsa.useAdvDynSecondaryMix + " secondaryMix=" + salsa.advDynSecondaryMix);
                if (salsa.visemes != null)
                    foreach (var viseme in salsa.visemes)
                    {
                        report.AppendLine("Viseme: " + (viseme == null || viseme.expData == null ? "<invalid>" : viseme.expData.name)
                            + " trigger=" + (viseme == null ? 0f : viseme.trigger));
                        if (viseme?.expData == null) continue;
                        var count = Math.Min(viseme.expData.components?.Count ?? 0, viseme.expData.controllerVars?.Count ?? 0);
                        for (var index = 0; index < count; index++)
                        {
                            var component = viseme.expData.components[index];
                            var helper = viseme.expData.controllerVars[index];
                            report.AppendLine("  " + component.controlType + "\t" + (helper.smr == null ? "<missing renderer>" : GetRelativePath(animator.transform, helper.smr.transform))
                                + "\t" + helper.blendIndex + "\tminSALSA=" + helper.minShape + "\tmaxSALSA=" + helper.maxShape
                                + "\ton/hold/off=" + component.durationOn + "/" + component.durationHold + "/" + component.durationOff);
                        }
                    }
            }

            report.AppendLine();
            report.AppendLine("Expression assets and clip ownership:");
            AppendExpressionAudit(report);
            AppendAnimationClipAudit(report);
            AppendExportPolicyAudit(report);
            report.AppendLine();
            report.AppendLine("BodyPose ownership: speech PropertyStreamHandles are left unbound; all three existing graph jobs skip invalid handles. Snapshot capture writes configured speech rest values, pose sampling saves/restores transient speech weights, and neutral/pose restores skip catalog-owned channels.");
            report.AppendLine("Expression facial-bone policy: retained under upperFaceRig/lowerJaw; gaze-owned head/lEye/rEye remain excluded. SALSA catalog has Shape controllers only.");
            report.AppendLine("Composition status: actual SALSA/audio and combined-expression Play Mode review is a separate manual gate; this static audit does not claim visual acceptance.");
            return report.ToString();
        }

        private static void AppendExpressionAudit(StringBuilder report)
        {
            var guids = AssetDatabase.FindAssets("t:PerformerExpression");
            report.AppendLine("PerformerExpression assets: " + guids.Length);
            var descriptorConflicts = 0;
            var clipConflicts = 0;
            foreach (var guid in guids.OrderBy(value => value, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>(path);
                var channels = expression?.Channels ?? Array.Empty<PerformerExpressionChannel>();
                var conflicts = channels.Where(channel => PerformerLipSyncMorphCatalog.IsOwnedBinding(channel.RendererPath,
                    channel.BlendShapeName)).ToArray();
                descriptorConflicts += conflicts.Length;
                report.AppendLine(path + " morphs=" + channels.Length + " bones=" + (expression?.BoneChannels?.Length ?? 0)
                    + " speechDescriptorConflicts=" + conflicts.Length + " clip="
                    + (expression?.Clip == null ? "<missing>" : AssetDatabase.GetAssetPath(expression.Clip)));
                if (expression?.Clip == null) continue;
                var clipBindings = AnimationUtility.GetCurveBindings(expression.Clip)
                    .Where(binding => binding.type == typeof(SkinnedMeshRenderer)
                        && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                    .Where(binding => PerformerLipSyncMorphCatalog.IsOwnedBinding(binding.path,
                        binding.propertyName.Substring("blendShape.".Length))).ToArray();
                clipConflicts += clipBindings.Length;
                foreach (var conflict in clipBindings)
                    report.AppendLine("  FAIL clip speech curve: " + conflict.path + "|" + conflict.propertyName);
            }
            report.AppendLine("Speech descriptor conflicts: " + descriptorConflicts);
            report.AppendLine("Speech expression-clip curve conflicts: " + clipConflicts);
        }

        private static void AppendAnimationClipAudit(StringBuilder report)
        {
            var conflicts = new List<string>();
            var clipCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip").OrderBy(value => value, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    clipCount++;
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.type != typeof(SkinnedMeshRenderer)
                            || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
                        var shapeName = binding.propertyName.Substring("blendShape.".Length);
                        if (PerformerLipSyncMorphCatalog.IsOwnedBinding(binding.path, shapeName))
                            conflicts.Add(path + "\t" + clip.name + "\t" + binding.path + "|" + shapeName);
                    }
                }
            }
            report.AppendLine("AnimationClip subassets audited: " + clipCount);
            report.AppendLine("All-clip speech-curve conflicts: " + conflicts.Count);
            foreach (var conflict in conflicts) report.AppendLine("  FAIL " + conflict);
        }

        private static void AppendExportPolicyAudit(StringBuilder report)
        {
            var manifestPath = Path.Combine(ProjectRoot, RequiredMorphManifestPath);
            var rulesPath = Path.Combine(ProjectRoot, ExportRulesPath);
            report.AppendLine("Always-Export source-control audit:");
            if (!File.Exists(manifestPath))
            {
                report.AppendLine("FAIL: Required Morph Manifest is missing.");
                return;
            }
            var manifest = JsonUtility.FromJson<DazPoseRequiredMorphManifest>(File.ReadAllText(manifestPath));
            var rules = File.Exists(rulesPath) ? File.ReadAllLines(rulesPath) : Array.Empty<string>();
            foreach (var definition in PerformerLipSyncMorphCatalog.Definitions)
            {
                var matches = (manifest?.items ?? Array.Empty<DazPoseRequiredMorphManifestItem>()).Where(item => item != null
                    && string.Equals(item.name, definition.SourceControlName, StringComparison.Ordinal)).ToArray();
                var item = matches.Length == 1 ? matches[0] : null;
                var rule = "\"" + definition.SourceControlName + "\",\"Export\"";
                var csvExport = rules.Any(line => string.Equals(line.Trim(), rule, StringComparison.Ordinal));
                report.AppendLine(definition.SourceControlName + "\tmanifestEntries=" + matches.Length
                    + "\tstate=" + (item == null ? "<missing/ambiguous>" : item.state)
                    + "\trequiredByContent=" + (item != null && item.requiredByContent)
                    + "\tAlwaysExport=" + (item != null && item.alwaysExport) + "\tCSV Export=" + csvExport);
            }
        }

        private static string ReadVersion()
        {
            var path = Path.Combine(Application.dataPath, "Plugins", "Crazy Minnow Studio", "SALSA LipSync", "Editor", "version.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "<version.txt missing>";
        }

        private static string GetRelativePath(Transform root, Transform target)
            => PerformerLipSyncMorphCatalog.GetRelativePath(root, target);

        private static string GetScenePath(Transform target)
        {
            if (target == null) return "<missing>";
            var segments = new System.Collections.Generic.Stack<string>();
            for (var current = target; current != null; current = current.parent) segments.Push(current.name);
            return string.Join("/", segments);
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    }
}
