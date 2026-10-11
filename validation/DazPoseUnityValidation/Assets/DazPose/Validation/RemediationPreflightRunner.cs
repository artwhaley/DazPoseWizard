using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.UnityValidation.Partner;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R04 side-effect witness for contact and declared wardrobe preflight.</summary>
    public sealed class RemediationPreflightRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class Report
        {
            public string ticket = "R04", startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PartnerBody partner;
        [SerializeField] private PartnerContactTarget target;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private string reportPath, sourceHash, inputHash;
        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer owner, PartnerBody anatomy, PartnerContactTarget contact,
            PerformanceSceneBindings sceneBindings, string output, string source, string input)
        {
            actor = owner; partner = anatomy; target = contact; bindings = sceneBindings;
            reportPath = output; sourceHash = source; inputHash = input;
        }
#endif

        private async void Start()
        {
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"), sourceHash = sourceHash ?? string.Empty,
                inputAssetHashes = new[] { inputHash ?? string.Empty }
            };
            try
            {
                for (int frame = 0; frame < 240 && (actor == null || !actor.IsRuntimeReady); ++frame)
                    await Awaitable.NextFrameAsync();
                RunContactPreflight();
#if UNITY_EDITOR
                RunWardrobePreflight();
#endif
                RunSpeechOnlyPreflight();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private void RunContactPreflight()
        {
            Record("fixtureReferences", actor != null && partner != null && target != null && bindings != null,
                "Actor, PartnerBody, PartnerContactTarget and PerformanceSceneBindings are required.");
            if (actor == null || partner == null || target == null || bindings == null) return;
            bindings.Configure(actor, bindings.Player, bindings.Playback, bindings.Toys, PerformerCapability.Contact);
            bindings.ConfigureContactTarget(target);

            Vector3 actorPosition = actor.transform.position;
            Quaternion actorRotation = actor.transform.rotation;
            Vector3 partnerPosition = partner.transform.position;
            Quaternion partnerRotation = partner.transform.rotation;
            Vector3 partnerScale = partner.transform.localScale;
            var originalBody = target.Body;
            var originalProfile = target.Profile;
            BindingIssue[] validEdit = bindings.Validate(PerformerCapability.Contact, false);
            Record("validContactEditPreflight", validEdit.Length == 0, Describe(validEdit));
            Record("editPreflightHasNoMutation", Same(actor.transform, actorPosition, actorRotation) &&
                Same(partner.transform, partnerPosition, partnerRotation, partnerScale) &&
                target.Body == originalBody && target.Profile == originalProfile,
                "Transform and target asset references remained unchanged after edit validation.");

            var missingObject = Instantiate(target.gameObject);
            missingObject.name = "R04 missing-body target";
            var missing = missingObject.GetComponent<PartnerContactTarget>();
            missing.Configure(null, originalProfile);
            bindings.ConfigureContactTarget(missing);
            ExpectBindingFailure("missingTargetBody", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "Partner contact target requires a PartnerBody reference");
            Destroy(missingObject);
            bindings.ConfigureContactTarget(target);

            var disabledObject = Instantiate(target.gameObject);
            disabledObject.name = "R04 disabled-body target";
            var disabledBody = disabledObject.GetComponent<PartnerBody>();
            var disabledTarget = disabledObject.GetComponent<PartnerContactTarget>();
            disabledBody.StructuralBody.enabled = false;
            bindings.ConfigureContactTarget(disabledTarget);
            ExpectBindingFailure("disabledTargetBody", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "enabled active structural body");
            Destroy(disabledObject);
            bindings.ConfigureContactTarget(target);

            var changedObject = Instantiate(target.gameObject);
            changedObject.name = "R04 changed-profile target";
            var changedBody = changedObject.GetComponent<PartnerBody>();
            var changedProfile = ScriptableObject.CreateInstance<PartnerContactProfile>();
            changedProfile.Configure(changedBody.ShellRenderer.sharedMesh, originalProfile.ShellMesh,
                originalProfile.CentersRoot, Enumerable.Range(0, originalProfile.CentersRoot.Length)
                    .Select(originalProfile.Radius).ToArray(), originalProfile.SupportedArcSpan,
                originalProfile.MaximumRadialDeviation, originalProfile.MeasurementIdentity,
                originalProfile.SectionTangentRoot, originalProfile.ContactSurface);
            var changedTarget = changedObject.GetComponent<PartnerContactTarget>();
            changedTarget.Configure(changedBody, changedProfile);
            bindings.ConfigureContactTarget(changedTarget);
            ExpectBindingFailure("changedProfileMesh", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "profile body mesh");
            Destroy(changedObject);
            Destroy(changedProfile);
            bindings.ConfigureContactTarget(target);

            float originalErection = partner.Erection01;
            partner.Erection01 = .5f;
            ExpectBindingFailure("unsupportedEndpoint", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "authored erect endpoint");
            partner.Erection01 = originalErection;
            partner.SetCompliance(2f, 0f, 0f);
            ExpectBindingFailure("unsupportedCompliance", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "compliance set to zero");
            partner.SetCompliance(0f, 0f, 0f);
            partner.transform.localScale = partnerScale * 1.1f;
            ExpectBindingFailure("unsupportedScale", "contactTarget", bindings.Validate(PerformerCapability.Contact, false), "unit scale");
            partner.transform.localScale = partnerScale;

            BindingIssue[] validRuntime = bindings.Validate(PerformerCapability.Contact, true);
            Record("validContactRuntimePreflight", validRuntime.Length == 0, Describe(validRuntime));
            target.enabled = false;
            ExpectBindingFailure("liveTargetRemovalAfterBegin", "contactTarget", bindings.Validate(PerformerCapability.Contact, true), "active ready");
            target.enabled = true;
            bindings.ConfigureContactTarget(target);
        }

#if UNITY_EDITOR
        private void RunWardrobePreflight()
        {
            var wardrobe = actor != null ? actor.GetComponent<PerformerWardrobe>() : null;
            var catalog = wardrobe != null ? wardrobe.Catalog : null;
            var declared = catalog != null ? catalog.Presets.FirstOrDefault(item => item != null) : null;
            string validReason = null;
            bool valid = wardrobe != null && declared != null && wardrobe.TryValidateReadiness(false,
                new[] { declared }, out validReason);
            Record("validDeclaredWardrobeSubset", valid, validReason ?? "Declared preset, reachable fits and body/effect ownership are valid.");
            if (wardrobe == null || catalog == null || declared == null) return;

            var wrongConfig = ScriptableObject.CreateInstance<WardrobeConfiguration>();
            wrongConfig.ConfigureGenerated(Array.Empty<WardrobeConfiguration.PieceAssignment>(),
                Array.Empty<WardrobeConfiguration.MaterialOverride>(), Array.Empty<WardrobeConfiguration.FootwearOverride>());
            wrongConfig.CommitRevision("r04-mismatch", wardrobe.Configuration != null ? wardrobe.Configuration.InputHashes : Array.Empty<string>());
            ExpectWardrobeFailure("releasedConfigurationMismatch", wardrobe, catalog, wrongConfig, declared, "generation");

            var staleCatalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
            staleCatalog.ConfigureCandidate(catalog.Presets);
            staleCatalog.SetAuthoringConfiguration(wardrobe.Configuration);
            staleCatalog.SetCompiledConfigurationHash(string.Empty);
            ExpectWardrobeFailure("staleCandidateCatalog", wardrobe, staleCatalog, wardrobe.Configuration, declared, "compile|preview");

            var brokenPreset = Instantiate(declared);
            brokenPreset.name = "R04 invalid declared fit";
            brokenPreset.SetGeneratedFitStates(Array.Empty<WardrobeFitState>());
            ExpectWardrobeFailure("invalidDeclaredFit", wardrobe, catalog, wardrobe.Configuration, brokenPreset, "fit");
            Destroy(brokenPreset);
            Destroy(wrongConfig);
            Destroy(staleCatalog);
        }
#endif

        private void RunSpeechOnlyPreflight()
        {
            var objectRoot = new GameObject("R04 speech-only declaration");
            var speechOnly = objectRoot.AddComponent<PerformanceSceneBindings>();
            speechOnly.Configure(actor, null, null, null, PerformerCapability.Speech);
            BindingIssue[] issues = speechOnly.Validate(PerformerCapability.Speech, false);
            Record("speechOnlyDoesNotRequireOptionalSystems", issues.Length == 0, Describe(issues));
            Destroy(objectRoot);
        }

        private void ExpectBindingFailure(string name, string path, BindingIssue[] issues, string reason)
        {
            string detail = Describe(issues);
            Record(name, issues.Any(issue => issue.PropertyPath == path &&
                (string.IsNullOrEmpty(reason) || issue.Message.IndexOf(reason, StringComparison.OrdinalIgnoreCase) >= 0)), detail);
        }

#if UNITY_EDITOR
        private void ExpectWardrobeFailure(string name, PerformerWardrobe owner, WardrobeCatalog catalog,
            WardrobeConfiguration config, WardrobePreset preset, string reason)
        {
            bool passed = !PerformerWardrobe.TryValidateReadiness(owner, catalog, config, false,
                new[] { preset }, out string actual);
            bool reasonMatched = !string.IsNullOrEmpty(actual) && (reason ?? string.Empty).Split('|')
                .Any(candidate => actual.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
            Record(name, passed && reasonMatched,
                actual ?? "Unexpectedly accepted invalid wardrobe declaration.");
        }
#endif

        private static string Describe(BindingIssue[] issues) => issues == null || issues.Length == 0
            ? "no issues" : string.Join(" | ", issues.Select(issue => issue.ToString()));

        private static bool Same(Transform value, Vector3 position, Quaternion rotation) => value != null &&
            Vector3.Distance(value.position, position) <= .000001f && Quaternion.Angle(value.rotation, rotation) <= .001f;

        private static bool Same(Transform value, Vector3 position, Quaternion rotation, Vector3 scale) =>
            Same(value, position, rotation) && Vector3.Distance(value.localScale, scale) <= .000001f;

        private void Record(string name, bool passed, string actual)
        { report.assertions.Add(new Assertion { name = name, expected = "pass", actual = actual ?? string.Empty, passed = passed }); }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true; report.exception = exception; report.passed = passed; report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R04.json") : reportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(passed ? 0 : 1);
#endif
        }
    }
}
