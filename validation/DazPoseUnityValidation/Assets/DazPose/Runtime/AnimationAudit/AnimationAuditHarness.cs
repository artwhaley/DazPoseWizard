using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DazPose.AnimationAudit
{
    // One actor, ordinary Animator states, and controls for inspecting authored clips.
    public sealed class AnimationAuditHarness : MonoBehaviour
    {
        public AnimationAuditCatalog catalog;
        public Animator animator;
        public RuntimeAnimatorController noFootIKController;
        public RuntimeAnimatorController footIKController;
        public bool useFootIK;
        public Camera previewCamera;
        public Transform floor;
        public Transform seatAnchor;
        public int initialEntryIndex;
        public float playbackSpeed = 1f;
        public int loopCycles = 3;
        public bool repeat = true;
        public bool applyRootMotion;
        public bool mirror;
        public float blendSeconds = 0.08f;

        private AnimationAuditEntry _selected;
        private AnimationAuditEntry _current;
        private AnimationAuditEntry _requestedClip;
        private AnimationAuditPack _pack = AnimationAuditPack.Kawaii;
        private string _category = "All";
        private string _search = "";
        private string _status = "Choose a clip or a family sequence.";
        private string _measurementLabel = "";
        private List<AnimationAuditEntry> _visible = new List<AnimationAuditEntry>();
        private Vector2 _panelScroll, _listScroll;
        private Vector3 _origin, _measurementOrigin, _previousRoot;
        private Quaternion _originRotation;
        private float _previousYaw, _yaw, _distance, _elapsed;
        private Vector3 _previousLeft, _previousRight, _leftVelocity, _rightVelocity;
        private Transform _leftFoot, _rightFoot;
        private bool _playing = true, _paused, _sequenceRunning;
        private Coroutine _sequence;
        public static float PanelWidth => Mathf.Min(380f, Screen.width * 0.38f);

        private void Awake()
        {
            if (animator == null || catalog == null) { enabled = false; return; }
            _origin = animator.transform.position;
            _originRotation = animator.transform.rotation;
            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _current = initialEntryIndex >= 0 && initialEntryIndex < catalog.entries.Count ? catalog.entries[initialEntryIndex] : null;
            _selected = _current;
            _measurementLabel = _current != null ? _current.IdentityName : "";
            if (useFootIK && footIKController != null) animator.runtimeAnimatorController = footIKController;
            // The controller's default breathing state starts normally, as in the simple test.
            animator.applyRootMotion = applyRootMotion;
            animator.SetBool("Mirror", mirror);
            animator.speed = playbackSpeed;
            ResetMetrics();
            RefreshList();
        }

        private void Update()
        {
            if (_requestedClip != null)
            {
                // Apply selection between GUI passes so changing family controls cannot break layout.
                _selected = _requestedClip;
                _requestedClip = null;
                CancelSequence();
                RestoreOrigin();
                PlayClip(_selected, false);
            }
            if (previewCamera != null)
            {
                float left = (PanelWidth + 24f) / Mathf.Max(1, Screen.width);
                previewCamera.rect = new Rect(left, 0f, 1f - left, 1f);
            }
            if (!_playing || _paused || _sequenceRunning || _current == null) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(_current.animatorStateName) || state.normalizedTime < 1f) return;
            if (!repeat) { animator.speed = 0f; _playing = false; }
            else if (!_current.loopTime) animator.Play(StateHash(_current), 0, 0f);
        }

        private void LateUpdate()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            Vector3 position = animator.transform.position;
            if (_playing && !_paused && dt > 0f)
            {
                Vector3 step = position - _previousRoot;
                _distance += new Vector2(step.x, step.z).magnitude;
                _yaw += Mathf.DeltaAngle(_previousYaw, animator.transform.eulerAngles.y);
                _elapsed += dt;
            }
            if (dt > 0f)
            {
                _leftVelocity = _leftFoot != null ? (_leftFoot.position - _previousLeft) / dt : Vector3.zero;
                _rightVelocity = _rightFoot != null ? (_rightFoot.position - _previousRight) / dt : Vector3.zero;
            }
            _previousRoot = position;
            _previousYaw = animator.transform.eulerAngles.y;
            if (_leftFoot != null) _previousLeft = _leftFoot.position;
            if (_rightFoot != null) _previousRight = _rightFoot.position;
        }

        private void OnGUI()
        {
            if (animator == null || catalog == null) return;
            float width = PanelWidth;
            GUI.Box(new Rect(12, 12, width, Screen.height - 24), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 22, width - 20, Screen.height - 44));
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);
            GUILayout.Label("ANIMATION LIBRARY — LARA");
            AnimationAuditPack[] packs = catalog.entries.Select(entry => entry.pack).Distinct().OrderBy(pack => pack).ToArray();
            int packIndex = Array.IndexOf(packs, _pack);
            int nextPack = GUILayout.Toolbar(Mathf.Max(0, packIndex), packs.Select(PackLabel).ToArray());
            if (nextPack >= 0 && nextPack < packs.Length && packs[nextPack] != _pack)
            { _pack = packs[nextPack]; _category = "All"; RefreshList(); }
            string[] categories = new[] { "All" }.Concat(catalog.GetCategories(_pack)).ToArray();
            int categoryIndex = Mathf.Max(0, Array.IndexOf(categories, _category));
            int nextCategory = GUILayout.SelectionGrid(categoryIndex, categories, 3);
            if (nextCategory >= 0 && categories[nextCategory] != _category)
            { _category = categories[nextCategory]; RefreshList(); }
            string search = GUILayout.TextField(_search);
            if (search != _search) { _search = search; RefreshList(); }
            GUILayout.Label(_visible.Count + " matching clips");
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(175));
            foreach (AnimationAuditEntry entry in _visible)
            {
                string label = (entry == _selected ? "▶ " : "") + entry.IdentityName;
                if (GUILayout.Button(label)) _requestedClip = entry;
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.enabled = Playable(_selected);
            if (GUILayout.Button("Play / Restart")) { CancelSequence(); RestoreOrigin(); PlayClip(_selected, false); }
            GUI.enabled = _current != null;
            if (GUILayout.Button(_paused ? "Resume" : "Pause"))
            { _paused = !_paused; if (!_paused) _playing = true; animator.speed = _paused ? 0f : playbackSpeed; }
            GUI.enabled = true;
            if (GUILayout.Button("Stop")) StopPlayback();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset position + heading")) { StopPlayback(); RestoreOrigin(); }
            float nextSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.05f, 2f);
            if (Mathf.Abs(nextSpeed - playbackSpeed) > 0.001f)
            { playbackSpeed = nextSpeed; if (!_paused && _playing) animator.speed = playbackSpeed; }
            GUILayout.Label("Speed: " + playbackSpeed.ToString("0.00") + "×");
            repeat = GUILayout.Toggle(repeat, "Repeat selected clip");
            bool nextRoot = GUILayout.Toggle(applyRootMotion, "Apply actual animation root motion");
            bool nextMirror = GUILayout.Toggle(mirror, "Humanoid mirror (same Lara)");
            GUI.enabled = noFootIKController != null && footIKController != null;
            bool nextFootIK = GUILayout.Toggle(useFootIK, "Unity retarget Foot IK");
            GUI.enabled = true;
            if (nextRoot != applyRootMotion || nextMirror != mirror || nextFootIK != useFootIK)
            {
                CancelSequence();
                if (nextFootIK != useFootIK)
                {
                    useFootIK = nextFootIK;
                    animator.runtimeAnimatorController = useFootIK ? footIKController : noFootIKController;
                }
                applyRootMotion = nextRoot;
                mirror = nextMirror;
                animator.applyRootMotion = applyRootMotion;
                animator.SetBool("Mirror", mirror);
                RestoreOrigin();
                PlayClip(_selected, false);
                if (Playable(_selected)) _status = "Mode changed; selected clip restarted from the original position.";
            }
            if (_current != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                float phase = Mathf.Repeat(state.normalizedTime, 1f);
                if (!_playing && state.normalizedTime >= 1f) phase = 1f;
                float scrub = GUILayout.HorizontalSlider(phase, 0f, 0.999f);
                GUILayout.Label("Pose phase: " + phase.ToString("0.000") + " — scrubbing pauses playback");
                if (Mathf.Abs(scrub - phase) > 0.005f)
                {
                    CancelSequence();
                    animator.Play(StateHash(_current), 0, scrub);
                    animator.speed = 0f;
                    animator.Update(0f);
                    _paused = true;
                }
            }

            DrawSequences();
            DrawMetrics();
            if (_selected != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("Selected: " + _selected.IdentityName, Wrap());
                GUILayout.Label(_selected.assetPath + "\nSubclip: " + _selected.clipName, Wrap());
                GUILayout.Label("Duration: " + _selected.durationSeconds.ToString("0.000") + " s | " + _selected.frameRate.ToString("0.#") + " fps | Import loop: " + _selected.loopTime);
                GUILayout.Label("Rig: " + (_selected.sourceHumanoid ? "Humanoid" : "Generic") + " | Root curves: " + _selected.hasRootMotionCurves);
                if (!Playable(_selected)) GUILayout.Label("This clip is listed but cannot be retargeted by this Humanoid test.", Wrap());
                if (!string.IsNullOrEmpty(_selected.importWarnings)) GUILayout.Label(_selected.importWarnings, Wrap());
                if (!string.IsNullOrEmpty(_selected.importErrors)) GUILayout.Label(_selected.importErrors, Wrap());
            }
            GUILayout.Label(_status, Wrap());
            GUILayout.Label("Right-drag: orbit camera. Mouse wheel: zoom.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSequences()
        {
            GUILayout.Space(8);
            GUILayout.Label("KAWAII Start → Walk → Stop");
            loopCycles = Mathf.RoundToInt(GUILayout.HorizontalSlider(loopCycles, 1, 8));
            GUILayout.Label("Walk cycles: " + loopCycles);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 7; i++)
            {
                int family = i;
                if (GUILayout.Button(i.ToString("00"))) PlayWalk(family, null);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (string turn in new[] { "KA_TurnLeft_90", "KA_TurnRight_90" })
                if (GUILayout.Button(turn.Contains("Left") ? "L90 → Walk" : "R90 → Walk")) PlayWalk(SelectedWalkFamily(), turn);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (string turn in new[] { "KA_TurnLeft_180", "KA_TurnRight_180" })
                if (GUILayout.Button(turn.Contains("Left") ? "L180 → Walk" : "R180 → Walk")) PlayWalk(SelectedWalkFamily(), turn);
            GUILayout.EndHorizontal();
            blendSeconds = GUILayout.HorizontalSlider(blendSeconds, 0f, 0.2f);
            GUILayout.Label("Sequence blend: " + blendSeconds.ToString("0.000") + " s (0 = inspect the raw seam)");
            string stem = SelectedStem();
            string familyStem = StripSuffix(stem);
            if (_selected != null && _selected.pack == AnimationAuditPack.Kawaii &&
                new[] { "Speak", "Sit", "Sleep" }.Any(name => stem.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) &&
                new[] { "_Start", "_Loop", "_End" }.All(suffix => Playable(catalog.FindByStem(AnimationAuditPack.Kawaii, familyStem + suffix))))
            {
                if (GUILayout.Button("Selected family: Start → Loop → End"))
                {
                    _pack = AnimationAuditPack.Kawaii;
                    PlayNamedSequence(new[] { familyStem + "_Start", familyStem + "_Loop", familyStem + "_End" }, new[] { 1, loopCycles, 1 });
                }
            }
            if (_selected != null && _selected.pack == AnimationAuditPack.Kawaii && stem.IndexOf("Sit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (seatAnchor != null && GUILayout.Button("Place at seat approach"))
                {
                    StopPlayback();
                    Vector3 position = seatAnchor.position - seatAnchor.forward * 0.75f;
                    position.y = _origin.y;
                    animator.transform.position = position;
                    animator.transform.rotation = seatAnchor.rotation;
                    _origin = position;
                    _originRotation = seatAnchor.rotation;
                    ResetMetrics();
                }
            }
        }

        private void PlayWalk(int family, string turn)
        {
            _pack = AnimationAuditPack.Kawaii;
            string stem = "KA_Walk" + family.ToString("00");
            var names = new List<string>();
            var cycles = new List<int>();
            if (turn != null) { names.Add(turn); cycles.Add(1); }
            names.AddRange(new[] { stem + "_Start", stem, stem + "_Stop" });
            cycles.AddRange(new[] { 1, loopCycles, 1 });
            PlayNamedSequence(names.ToArray(), cycles.ToArray());
        }

        private void PlayNamedSequence(string[] names, int[] cycles)
        {
            AnimationAuditEntry[] entries = names.Select(name => catalog.FindByStem(_pack, name)).ToArray();
            string[] missing = names.Where((name, index) => !Playable(entries[index])).ToArray();
            if (missing.Length > 0) { _status = "Sequence unavailable: " + string.Join(", ", missing); return; }
            CancelSequence();
            RestoreOrigin();
            _category = "All";
            _search = "";
            RefreshList();
            _measurementLabel = string.Join(" → ", names);
            _sequenceRunning = true;
            _sequence = StartCoroutine(Sequence(entries, cycles));
        }

        private IEnumerator Sequence(AnimationAuditEntry[] entries, int[] cycles)
        {
            for (int step = 0; step < entries.Length; step++)
            {
                for (int pass = 0; pass < Mathf.Max(1, cycles[step]); pass++)
                {
                    _selected = entries[step];
                    PlayClip(entries[step], step > 0 && pass == 0);
                    _status = entries[step].IdentityName + " — cycle " + (pass + 1) + "/" + cycles[step];
                    // Loop boundaries replay the same ordinary Animator state. No importer edits.
                    while (true)
                    {
                        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                        if (!animator.IsInTransition(0) && state.IsName(entries[step].animatorStateName) && state.normalizedTime >= 1f) break;
                        yield return null;
                    }
                }
            }
            _sequenceRunning = false;
            _sequence = null;
            animator.speed = 0f;
            _playing = false;
            _status = "Sequence finished. Root metrics cover the complete sequence.";
            _paused = true;
        }

        private void PlayClip(AnimationAuditEntry entry, bool blend)
        {
            if (!Playable(entry)) { StopPlayback(); _status = "Selected clip has no playable Humanoid state."; return; }
            _current = entry;
            if (!_sequenceRunning) _measurementLabel = entry.IdentityName;
            _paused = false;
            _playing = true;
            animator.speed = playbackSpeed;
            animator.SetBool("Mirror", mirror);
            if (blend && blendSeconds > 0f) animator.CrossFadeInFixedTime(StateHash(entry), blendSeconds, 0, 0f);
            else animator.Play(StateHash(entry), 0, 0f);
            animator.Update(0f);
        }

        private bool Playable(AnimationAuditEntry entry)
        {
            return entry != null && entry.clip != null && !string.IsNullOrEmpty(entry.animatorStateName) &&
                   animator != null && animator.HasState(0, StateHash(entry));
        }

        private void StopPlayback()
        {
            CancelSequence();
            animator.speed = 0f;
            _playing = false;
            _paused = true;
            _status = "Stopped; final pose and measurements held.";
        }

        private void CancelSequence()
        {
            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = null;
            _sequenceRunning = false;
        }

        private void RestoreOrigin()
        {
            animator.transform.SetPositionAndRotation(_origin, _originRotation);
            ResetMetrics();
        }

        private void ResetMetrics()
        {
            _measurementOrigin = _previousRoot = animator.transform.position;
            _previousYaw = animator.transform.eulerAngles.y;
            _elapsed = _distance = _yaw = 0f;
            if (_leftFoot != null) _previousLeft = _leftFoot.position;
            if (_rightFoot != null) _previousRight = _rightFoot.position;
            _leftVelocity = _rightVelocity = Vector3.zero;
        }

        private void DrawMetrics()
        {
            Vector3 delta = animator.transform.position - _measurementOrigin;
            GUILayout.Space(8);
            GUILayout.Label("Playing: " + (_current != null ? _current.IdentityName : "none"), Wrap());
            GUILayout.Label("Measurement: " + _measurementLabel, Wrap());
            GUILayout.Label("Root Δ (m): " + Format(delta) + " | yaw: " + _yaw.ToString("0.0") + "°");
            GUILayout.Label("Planar path: " + _distance.ToString("0.000") + " m | elapsed: " + _elapsed.ToString("0.00") + " s | speed: " + (_elapsed > 0f ? _distance / _elapsed : 0f).ToString("0.000") + " m/s");
            GUILayout.Label("Left foot: " + FootInfo(_leftFoot, _leftVelocity), Wrap());
            GUILayout.Label("Right foot: " + FootInfo(_rightFoot, _rightVelocity), Wrap());
            GUILayout.Label("Planted labels are height/velocity heuristics.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Zero metrics")) ResetMetrics();
            if (GUILayout.Button("Save measurement")) SaveMeasurement();
            GUILayout.EndHorizontal();
        }

        private void SaveMeasurement()
        {
            if (_current == null) return;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "animation-audit", "LocomotionMeasurements.csv"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Use a separate file if a previous audit wrote an incompatible CSV schema.
                const string header = "timestamp_utc,pack,clip,asset_path,root_motion,mirror,foot_ik,elapsed_s,root_dx,root_dy,root_dz,planar_path_m,yaw_degrees";
                if (File.Exists(path) && File.ReadLines(path).FirstOrDefault() != header)
                    path = Path.Combine(Path.GetDirectoryName(path), "SingleLaraFootIKMeasurements.csv");
                if (!File.Exists(path)) File.WriteAllText(path, header + Environment.NewLine);
                Vector3 delta = animator.transform.position - _measurementOrigin;
                string[] fields = { DateTime.UtcNow.ToString("o"), _current.pack.ToString(), _measurementLabel, _current.assetPath, applyRootMotion.ToString(), mirror.ToString(), useFootIK.ToString(), Number(_elapsed), Number(delta.x), Number(delta.y), Number(delta.z), Number(_distance), Number(_yaw) };
                File.AppendAllText(path, string.Join(",", fields.Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")) + Environment.NewLine);
                _status = "Saved measurement: " + path;
            }
            catch (Exception exception) { _status = "Could not save measurement: " + exception.Message; }
        }

        private void RefreshList() { _visible = catalog.Query(_pack, _category, _search).ToList(); _listScroll = Vector2.zero; }
        private string SelectedStem() { return _selected == null ? "" : Path.GetFileNameWithoutExtension(_selected.assetPath).TrimStart('@'); }
        private int SelectedWalkFamily()
        {
            string stem = SelectedStem();
            for (int i = 1; i <= 7; i++) if (stem.Contains("Walk" + i.ToString("00"))) return i;
            return 1;
        }
        private static string StripSuffix(string stem)
        {
            foreach (string suffix in new[] { "_Start", "_Loop", "_End", "_Stop" })
                if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return stem.Substring(0, stem.Length - suffix.Length);
            return stem;
        }
        private string FootInfo(Transform foot, Vector3 velocity)
        {
            if (foot == null) return "unmapped";
            float height = foot.position.y - (floor != null ? floor.position.y : 0f);
            return "height " + height.ToString("0.000") + " m | velocity " + Format(velocity) + " m/s | " + (height < 0.18f && velocity.magnitude < 0.18f ? "planted" : "moving");
        }
        private static int StateHash(AnimationAuditEntry entry) { return Animator.StringToHash("Base Layer." + entry.animatorStateName); }
        private static string PackLabel(AnimationAuditPack pack) { return pack == AnimationAuditPack.Kawaii ? "KAWAII" : "Animset Pro"; }
        private static string Number(float value) { return value.ToString("0.000", CultureInfo.InvariantCulture); }
        private static string Format(Vector3 value) { return "(" + Number(value.x) + ", " + Number(value.y) + ", " + Number(value.z) + ")"; }
        private static GUIStyle Wrap() { return new GUIStyle(GUI.skin.label) { wordWrap = true }; }
    }
}
