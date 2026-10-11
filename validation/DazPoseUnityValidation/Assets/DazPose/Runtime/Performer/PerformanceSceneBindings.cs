using System;
using System.Collections.Generic;
using DazPose.Motion;
using DazPose.Player;
using DazPose.Toys;
using DazPose.Performer.HandGrip;
using UnityEngine;

namespace DazPose.Performer
{
    [Flags]
    public enum PerformerCapability
    {
        None = 0, Pose = 1, Expression = 2, Speech = 4, Movement = 8,
        Seating = 16, Visibility = 32, Wardrobe = 64, Contact = 128,
        Playback = 256, Gesture = 512, LipSync = 1024, Teleport = 2048,
        Action = 4096, Magic = 8192
    }

    public sealed class BindingIssue
    {
        public UnityEngine.Object Owner { get; }
        public string PropertyPath { get; }
        public string Message { get; }
        public BindingIssue(UnityEngine.Object owner, string path, string message)
        { Owner = owner; PropertyPath = path; Message = message; }
        public override string ToString() => (Owner != null ? Owner.name : "Missing owner") + "/" + PropertyPath + ": " + Message;
    }

    /// <summary>Explicit service and target references for one performance; legacy diagnostics are optional. Does not discover objects or control playback.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Performer/Performance Scene Bindings")]
    public sealed class PerformanceSceneBindings : MonoBehaviour
    {
        [Serializable] public struct Target { public string id; public Transform transform; }
        [SerializeField] private string sceneId, actorId;
        public string SceneId => sceneId;
        public string ActorId => actorId;
        public void ConfigureIdentity(string scene, string actor) { sceneId=scene; actorId=actor; }
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PlayerController player;
        [SerializeField] private MotionDriver playback;
        [SerializeField, HideInInspector] private ToyControlService toys;
        [SerializeField] private MonoBehaviour contactTarget;
        [SerializeField, HideInInspector] private PerformerCapability requiredCapabilities = PerformerCapability.Pose | PerformerCapability.Speech;
        [SerializeField, HideInInspector] private bool requirePlayer;
        [SerializeField, HideInInspector] private bool requireToys;
        [SerializeField] private Target[] targets = Array.Empty<Target>();
        [SerializeField, HideInInspector] private PerformerPose[] poses = Array.Empty<PerformerPose>();
        [SerializeField, HideInInspector] private PerformerExpression[] expressions = Array.Empty<PerformerExpression>();
        [SerializeField, HideInInspector] private AudioClip[] speechClips = Array.Empty<AudioClip>();
        [SerializeField, HideInInspector] private PerformerSeat[] seats = Array.Empty<PerformerSeat>();
        [SerializeField, HideInInspector] private WardrobePreset[] wardrobePresets = Array.Empty<WardrobePreset>();
        [SerializeField, HideInInspector] private PerformerGesture[] gestures = Array.Empty<PerformerGesture>();
        [SerializeField, HideInInspector] private PerformerAction[] actions = Array.Empty<PerformerAction>();
        [SerializeField, HideInInspector] private PerformerSpell[] spells = Array.Empty<PerformerSpell>();
        [SerializeField, HideInInspector] private PerformerAura[] auras = Array.Empty<PerformerAura>();

        public SuccubusPerformer Performer => performer;
        public PlayerController Player => player;
        public MotionDriver Playback => playback;
        public ToyControlService Toys => toys;
        public IGripTarget ContactTarget => contactTarget != null ? contactTarget as IGripTarget : null;
        public PerformerCapability RequiredCapabilities => requiredCapabilities;
        public WardrobePreset[] DeclaredWardrobePresets => (WardrobePreset[])(wardrobePresets ?? Array.Empty<WardrobePreset>()).Clone();
        public PerformerGesture[] DeclaredGestures => (PerformerGesture[])(gestures ?? Array.Empty<PerformerGesture>()).Clone();
        public PerformerAction[] DeclaredActions => (PerformerAction[])(actions ?? Array.Empty<PerformerAction>()).Clone();
        public PerformerSpell[] DeclaredSpells => (PerformerSpell[])(spells ?? Array.Empty<PerformerSpell>()).Clone();
        public PerformerAura[] DeclaredAuras => (PerformerAura[])(auras ?? Array.Empty<PerformerAura>()).Clone();
        public bool RequiresPlayer { get => requirePlayer; set => requirePlayer = value; }
        public bool RequiresToys { get => requireToys; set => requireToys = value; }
        public void ConfigureContactTarget(MonoBehaviour target)
        {
            if (target != null && !(target is IGripTarget)) throw new ArgumentException("Contact target must implement IGripTarget.", nameof(target));
            contactTarget = target;
        }
        public void DeclareAssets(PerformerPose[] sequencePoses, PerformerExpression[] sequenceExpressions, AudioClip[] clips)
        { poses = sequencePoses ?? Array.Empty<PerformerPose>(); expressions = sequenceExpressions ?? Array.Empty<PerformerExpression>(); speechClips = clips ?? Array.Empty<AudioClip>(); }
        public void DeclareSeats(PerformerSeat[] sequenceSeats) => seats = sequenceSeats ?? Array.Empty<PerformerSeat>();
        public void DeclareWardrobePresets(WardrobePreset[] presets) => wardrobePresets = presets ?? Array.Empty<WardrobePreset>();
        public void DeclareContent(PerformerGesture[] sequenceGestures, PerformerAction[] sequenceActions,
            PerformerSpell[] sequenceSpells, PerformerAura[] sequenceAuras)
        {
            gestures = sequenceGestures ?? Array.Empty<PerformerGesture>();
            actions = sequenceActions ?? Array.Empty<PerformerAction>();
            spells = sequenceSpells ?? Array.Empty<PerformerSpell>();
            auras = sequenceAuras ?? Array.Empty<PerformerAura>();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        public void Configure(SuccubusPerformer actor, PlayerController view, MotionDriver clock, ToyControlService devices,
            PerformerCapability capabilities, Target[] sceneTargets = null)
        {
            performer = actor; player = view; playback = clock; toys = devices;
            requiredCapabilities = capabilities; targets = sceneTargets ?? Array.Empty<Target>();
        }

        public Transform TargetNamed(string id)
        {
            Transform found = null;
            foreach (var target in targets ?? Array.Empty<Target>())
                if (string.Equals(target.id, id, StringComparison.Ordinal))
                {
                    if (found != null) throw new InvalidOperationException("Ambiguous scene target '" + id + "' on " + name + ".");
                    if (target.transform == null) throw new InvalidOperationException("Missing scene target '" + id + "' on " + name + ".");
                    found = target.transform;
                }
            return found != null ? found : throw new InvalidOperationException("Unknown scene target '" + id + "' on " + name + ".");
        }

        public BindingIssue[] Validate(bool runtime = false) => Validate(requiredCapabilities, runtime);
        public BindingIssue[] Validate(PerformerCapability capabilities, bool runtime = false)
        {
            var issues = new List<BindingIssue>();
            void Add(UnityEngine.Object owner, string path, string message) => issues.Add(new BindingIssue(owner, path, message));
            bool Needs(PerformerCapability capability) => (capabilities & capability) != 0;
            if (performer == null) Add(this, "performer", "Assign the performer for this sequence.");
            else
            {
                if (performer.gameObject.scene != gameObject.scene) Add(this, "performer", "Performer belongs to a different scene.");
                var animator = performer.ConfiguredAnimator;
                if (animator == null) Add(performer, "animator", "Assign a same-actor Animator.");
                else if (animator.transform != performer.transform) Add(performer, "animator", "The Generic actor and Animator must share their root.");
                if (runtime && !performer.IsRuntimeReady) Add(performer, "animator", "Actor runtime did not initialize or is disabled.");
                if (Needs(PerformerCapability.Pose) && (performer.InitialPose == null || performer.InitialPose.Clip == null) && (poses?.Length ?? 0) == 0)
                    Add(performer, "initialPose", "Assign an initial pose/clip or declare a sequence pose asset.");
                if (Needs(PerformerCapability.Speech) && performer.SpeechAudioSource == null) Add(performer, "actor/speechAudioSource", "Assign the actor's existing production speech AudioSource reference to actor/speechAudioSource.");
                else if (Needs(PerformerCapability.Speech) && !performer.SpeechAudioSource.transform.IsChildOf(performer.transform)) Add(performer, "actor/speechAudioSource", "The existing production speech AudioSource must be a child of this actor.");
                if (Needs(PerformerCapability.Expression) && !HasExpression(performer.InitialExpression) && !Array.Exists(expressions ?? Array.Empty<PerformerExpression>(), HasExpression))
                    Add(this, "expressions", "Declare an expression with a clip or channels, or assign the actor's initial expression.");
                if (Needs(PerformerCapability.LipSync))
                {
                    var bridge = performer.GetComponent<PerformerSalsaLipSync>();
                    if (bridge == null) Add(performer, "", "This sequence requires a configured SALSA bridge component.");
                    else if (!bridge.ValidateConfiguration(out var error)) Add(bridge, "salsa", error);
                }
                if (Needs(PerformerCapability.Movement) || Needs(PerformerCapability.Seating))
                {
                    if (performer.LocomotionProfile == null) Add(performer, "locomotionProfile", "Assign a locomotion/seating profile.");
                    else if (!performer.LocomotionProfile.IsReady(out var error)) Add(performer, "locomotionProfile", error);
                }
                if (Needs(PerformerCapability.Teleport))
                {
                    if (performer.TeleportProfile == null) Add(performer, "actor/teleportProfile", "Assign the actor's existing production PerformerTeleportProfile reference to actor/teleportProfile.");
                    else if (!performer.TeleportProfile.IsReady(out var error)) Add(performer, "actor/teleportProfile", error);
                }
                if (Needs(PerformerCapability.Visibility))
                {
                    if (performer.DissolveProfile == null) Add(performer, "actor/dissolveProfile", "Assign the actor's existing production PerformerDissolveProfile reference to actor/dissolveProfile.");
                    else if (!performer.DissolveProfile.IsReady(out var error)) Add(performer, "actor/dissolveProfile", error);
                    if (performer.DissolveRig == null) Add(performer, "actor/dissolveRig", "Assign the actor's existing production PerformerDissolveRig reference to actor/dissolveRig.");
                    else if (performer.DissolveProfile != null && !performer.DissolveRig.IsReady(performer.DissolveProfile, out var error)) Add(performer, "actor/dissolveRig", error);
                    if (performer.DissolveRig != null && performer.DissolveRig.GetComponent<SuccubusPerformer>() != performer) Add(performer, "actor/dissolveRig", "The existing production dissolve rig must belong to this actor.");
                    var particles = performer.DissolveRig != null ? performer.DissolveRig.ParticleBody : null;
                    if (particles != null && (particles.TargetRenderer == null || !particles.TargetRenderer.transform.IsChildOf(performer.transform))) Add(performer.DissolveRig, "actor/dissolveRig/particleBody", "The existing production particle body must target this actor's renderer.");
                }
                if (Needs(PerformerCapability.Wardrobe) || (wardrobePresets?.Length ?? 0) > 0)
                {
                    var wardrobe = performer.GetComponent<PerformerWardrobe>();
                    if (wardrobe == null) Add(performer, "", "Add and bind this actor's wardrobe component.");
                    else if (!wardrobe.TryValidateReadiness(runtime, wardrobePresets, out var error)) Add(wardrobe, "catalog", error);
                }
                if (Needs(PerformerCapability.Contact))
                {
                    var grip = performer.GetComponent<PerformerHandGripController>();
                    if (grip == null) Add(performer, "", "Add the same-actor grip controller.");
                    else if (grip.Profile == null) Add(grip, "profile", "Assign the calibrated hand profile.");
                    else if (!grip.Profile.IsReady(animator, out var error)) Add(grip, "profile", error);
                    if (ContactTarget == null) Add(this, "contactTarget", "Assign a supported IGripTarget; prepared anatomy alone is not a contact adapter.");
                    else if (ContactTarget is IGripTargetReadiness readiness && !readiness.IsReadyForContact(runtime, out var readinessError))
                        Add(contactTarget, "contactTarget", readinessError ?? "Declared contact target is not ready.");
                }
                if (Needs(PerformerCapability.Playback))
                {
                    if (playback == null) Add(this, "playback", "Assign this sequence's playback clock.");
                    else if (performer.MotionSource != playback) Add(performer, "motionDriver", "Actor source differs from the declared playback; wire it before enabling the actor.");
                    if (performer.MotionSet == null || !performer.MotionSet.IsReady(out _)) Add(performer, "motionSet", "Assign a validated visual motion set.");
                }
                if (Needs(PerformerCapability.Gesture))
                {
                    if (performer.GestureUpperBodyMask == null) Add(performer, "gestureUpperBodyMask", "Assign the actor's generated Gesture mask.");
                    else if (!PerformerGestureMaskUtility.IsValidMask(animator, performer.GestureUpperBodyMask, out var maskError))
                        Add(performer, "gestureUpperBodyMask", maskError);
                    if ((gestures?.Length ?? 0) == 0) Add(this, "gestures", "Declare at least one PerformerGesture asset for the required Gesture capability.");
                    ValidateDeclaredGestures(issues, gestures);
                }
                else if ((gestures?.Length ?? 0) > 0)
                    ValidateDeclaredGestures(issues, gestures);
                if (Needs(PerformerCapability.Action) && (actions?.Length ?? 0) == 0)
                    Add(this, "actions", "Declare at least one PerformerAction asset for the required Action capability.");
                if (Needs(PerformerCapability.Action) || (actions?.Length ?? 0) > 0)
                    ValidateDeclaredActions(issues, actions, animator);
                if (Needs(PerformerCapability.Magic) && (spells?.Length ?? 0) == 0 && (auras?.Length ?? 0) == 0)
                    Add(this, "magic", "Declare at least one PerformerSpell or PerformerAura asset for the required Magic capability.");
                if (Needs(PerformerCapability.Magic) || (spells?.Length ?? 0) > 0 || (auras?.Length ?? 0) > 0)
                    ValidateDeclaredMagic(issues, spells, auras);
            }
            if (requirePlayer && (player == null || player.View == null || player.MainCamera == null)) Add(this, "player", "Assign a player with its view rig and camera.");
            if (requireToys && toys == null) Add(this, "toys", "This sequence declares required toys; assign its device service.");
            if (toys != null && playback != null && toys.PlaybackDriver != playback) Add(toys, "playbackDriver", "Device service differs from the declared playback clock.");
            if (player != null && toys != null && player.BoundToyService != toys) Add(player, "toyControlService", "Player service differs from this binding's device service.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < (targets?.Length ?? 0); i++)
            {
                if (string.IsNullOrWhiteSpace(targets[i].id) || !ids.Add(targets[i].id)) Add(this, "targets.Array.data[" + i + "].id", "Target IDs must be nonempty and unique.");
                if (targets[i].transform == null) Add(this, "targets.Array.data[" + i + "].transform", "Assign this named target.");
                else if (targets[i].transform.gameObject.scene != gameObject.scene) Add(this, "targets.Array.data[" + i + "].transform", "Target belongs to a different scene.");
            }
            for (int i = 0; i < (poses?.Length ?? 0); i++) if (poses[i] == null || poses[i].Clip == null) Add(this, "poses.Array.data[" + i + "]", "Declared pose/clip is missing.");
            for (int i = 0; i < (expressions?.Length ?? 0); i++) if (!HasExpression(expressions[i])) Add(this, "expressions.Array.data[" + i + "]", "Declared expression/clip/channels are missing.");
            for (int i = 0; i < (speechClips?.Length ?? 0); i++) if (speechClips[i] == null) Add(this, "speechClips.Array.data[" + i + "]", "Declared speech clip is missing.");
            if (Needs(PerformerCapability.Seating) && (seats?.Length ?? 0) == 0) Add(this, "seats", "Declare the sequence's seat components and anchors.");
            for (int i = 0; i < (seats?.Length ?? 0); i++)
            {
                var seat = seats[i];
                if (seat == null) Add(this, "seats.Array.data[" + i + "]", "Declared seat is missing.");
                else if (seat.ApproachAnchor == null) Add(seat, "approachAnchor", "Assign this seat's approach anchor.");
                else if (seat.SeatAnchor == null) Add(seat, "seatAnchor", "Assign this seat's contact anchor.");
                else if (seat.SeatingProfile == null) Add(seat, "seatingProfile", "Assign the baked seating profile.");
                else if (!seat.TryCaptureFrames(out _, out _, out _, out _, out var error)) Add(seat, "approachAnchor", error);
                else if (seat.gameObject.scene != gameObject.scene) Add(this, "seats.Array.data[" + i + "]", "Seat belongs to a different scene.");
            }
            return issues.ToArray();
        }

        private void ValidateDeclaredGestures(List<BindingIssue> issues, PerformerGesture[] declared)
        {
            for (int i = 0; i < (declared?.Length ?? 0); i++)
            {
                PerformerGesture gesture = declared[i];
                if (gesture == null) { issues.Add(new BindingIssue(this, "gestures.Array.data[" + i + "]", "Declare a PerformerGesture asset.")); continue; }
                if (!gesture.IsReady(out string reason))
                    issues.Add(new BindingIssue(gesture, "gestures.Array.data[" + i + "]", reason));
            }
        }

        private void ValidateDeclaredActions(List<BindingIssue> issues, PerformerAction[] declared, Animator animator)
        {
            if (animator == null)
            {
                issues.Add(new BindingIssue(performer != null ? performer : this, "animator", "Assign the actor Animator before declaring Action assets."));
                return;
            }
            for (int i = 0; i < (declared?.Length ?? 0); i++)
            {
                PerformerAction action = declared[i];
                if (action == null) { issues.Add(new BindingIssue(this, "actions.Array.data[" + i + "]", "Declare a PerformerAction asset.")); continue; }
                if (!action.IsReady(out string reason))
                    issues.Add(new BindingIssue(action, "actions.Array.data[" + i + "]", reason));
                else if (action.BodyClip == null || action.BodyClip.empty || action.BodyClip.legacy || action.BodyClip.isHumanMotion)
                    issues.Add(new BindingIssue(action, "actions.Array.data[" + i + "]", "Action body clip must be a nonempty Generic clip compatible with the actor Animator."));
            }
        }

        private void ValidateDeclaredMagic(List<BindingIssue> issues, PerformerSpell[] declaredSpells, PerformerAura[] declaredAuras)
        {
            for (int i = 0; i < (declaredSpells?.Length ?? 0); i++)
            {
                PerformerSpell spell = declaredSpells[i];
                if (spell == null) { issues.Add(new BindingIssue(this, "spells.Array.data[" + i + "]", "Declare a PerformerSpell asset.")); continue; }
                if (!spell.IsReady(out string reason)) issues.Add(new BindingIssue(spell, "spells.Array.data[" + i + "]", reason));
            }
            for (int i = 0; i < (declaredAuras?.Length ?? 0); i++)
            {
                PerformerAura aura = declaredAuras[i];
                if (aura == null) { issues.Add(new BindingIssue(this, "auras.Array.data[" + i + "]", "Declare a PerformerAura asset.")); continue; }
                if (!aura.IsReady(out string reason)) issues.Add(new BindingIssue(aura, "auras.Array.data[" + i + "]", reason));
            }
        }
        static bool HasExpression(PerformerExpression expression) => expression != null && expression.Clip != null &&
            ((expression.Channels?.Length ?? 0) != 0 || expression.BoneChannels.Length != 0);
    }
}
