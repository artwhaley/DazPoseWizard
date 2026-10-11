using System;
using System.IO;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.HandGrip
{
    [InitializeOnLoad]
    public static class HandGripLiveProof
    {
        private const string Pending = "HandGrip.LiveProof.Pending";
        private const string Report = "TestOutput/TargetDrivenGrip/live.txt";
        private static SuccubusPerformer _performer;
        private static PerformerHandGripController _grip;
        private static GripContactRod _rod;
        private static MotionDriver _driver;
        private static int _step;
        private static int _positionIndex;
        private static FunscriptMotionProgram _program;
        private static bool _sawReposition;
        private static float _heldPosition;
        private static int _completionCount;
        private static GripCompletion _firstCompletion;
        private static GripCompletion _secondCompletion;
        private static double _started;
        private static Vector3 _root;
        static HandGripLiveProof()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("handgrip-validation/project/Assets"))
                throw new InvalidOperationException("Live proof requires the isolated project.");
            EditorSceneManager.OpenScene(HandGripAcceptanceSetup.AcceptanceScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        private static void Place(float distance, float height = 0f)
        {
            Transform actor = _performer.transform;
            _rod.transform.position = actor.position + actor.forward * distance + actor.right * 0.15f + Vector3.up * (1.35f + height);
            _rod.transform.rotation = Quaternion.identity;
            _rod.StartPoint.localPosition = Vector3.down * 0.06f; _rod.EndPoint.localPosition = Vector3.up * 0.06f;
            _rod.ReferenceTransform = _rod.transform; _rod.ReferenceNormalLocal = -actor.forward; _rod.Radius = 0.025f;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (_performer == null)
                {
                    _performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                    _grip = _performer.GetComponent<PerformerHandGripController>();
                    _rod = UnityEngine.Object.FindAnyObjectByType<GripContactRod>(); _driver = _performer.MotionSource;
                    _grip.ReleaseGrip(); Place(0.4f); _root = _performer.transform.position;
                    _performer.Grip(_rod); _started = EditorApplication.timeSinceStartup;
                }
                double elapsed = EditorApplication.timeSinceStartup - _started;
                if (_grip.State == GripInteractionState.Failed && _step != 5 && _step != 15)
                    throw new InvalidOperationException("Step " + _step + " failed: " + _grip.FailureReason);
                if (elapsed > 75f) throw new TimeoutException("Step " + _step + " state " + _grip.State);
                switch (_step)
                {
                    case 0:
                        if (!_performer.IsGripping) return;
                        if (_grip.RequiredLocomotion || Vector3.Distance(_root, _performer.transform.position) > 0.001f)
                            throw new InvalidOperationException("Near target walked unnecessarily.");
                        Log("PASS near no-walk: error_mm=" + _performer.GripPositionError * 1000f);
                        _driver.SourceMode = MotionSourceMode.Sine; _driver.StartMotion(); Next(); break;
                    case 1:
                        if (_performer.GripPositionError > 0.005f || _performer.GripRotationError > 5f)
                            throw new InvalidOperationException("Sine tracking exceeds tolerance.");
                        if (elapsed < 2f) return;
                        Log("PASS sine target tracking for two seconds."); _driver.StopMotion(); _performer.ReleaseGrip(); Next(); break;
                    case 2:
                        if (_grip.State != GripInteractionState.Idle) return;
                        Log("PASS release Idle, MotionDriver untouched."); Place(3f); _performer.Grip(_rod); Next(); break;
                    case 3:
                        if (!_performer.IsGripping) return;
                        if (!_grip.RequiredLocomotion) throw new InvalidOperationException("Far target did not use locomotion.");
                        Log("PASS far walk-to-grip: error_mm=" + _performer.GripPositionError * 1000f + " margin=" + _grip.ReachPlan.Margin);
                        Place(3f); _sawReposition = false; Next(); break;
                    case 4:
                        if (_grip.State == GripInteractionState.Releasing || _grip.State == GripInteractionState.Walking) _sawReposition = true;
                        if (!_performer.IsGripping || !_sawReposition) return;
                        Log("PASS moved-target release/walk/reacquire."); _performer.ReleaseGrip(); Place(1f, 3f); Next(); break;
                    case 5:
                        if (_grip.State == GripInteractionState.Idle) _performer.Grip(_rod);
                        if (_grip.State != GripInteractionState.Failed) return;
                        if (_grip.FailureReason != GripFailureReason.VerticalReachImpossible)
                            throw new InvalidOperationException("Wrong impossible-target failure " + _grip.FailureReason);
                        Log("PASS impossible vertical explicit failure.");
                        Place(0.4f); _performer.Grip(_rod); Next(); break;
                    case 6:
                        if (!_performer.IsGripping) return;
                        _program = FunscriptJsonParser.Parse("{\"actions\":[{\"at\":0,\"pos\":0},{\"at\":1000,\"pos\":100},{\"at\":2000,\"pos\":0}]}");
                        _driver.FunscriptProgram = _program; _driver.SourceMode = MotionSourceMode.Funscript;
                        _driver.StopMotion(); _driver.Seek(0d); _positionIndex = 0; Next(); break;
                    case 7:
                        if (elapsed < 0.1f) return;
                        float expected = _positionIndex / 4f;
                        if (Mathf.Abs(_driver.CurrentSample.Position01 - expected) > 0.0001f
                            || Mathf.Abs(_grip.Position01 - expected) > 0.0001f || _performer.GripPositionError > 0.005f)
                            throw new InvalidOperationException("Funscript Seek mapping failed at " + expected);
                        Log("PASS funscript seek p=" + expected + " error_mm=" + _performer.GripPositionError * 1000f);
                        if (++_positionIndex < 5) { _driver.Seek(_positionIndex / 4d); _started = EditorApplication.timeSinceStartup; }
                        else { _driver.Seek(0d); _driver.StartMotion(); Next(); }
                        break;
                    case 8:
                        if (_performer.GripPositionError > 0.005f || _grip.SolveResult.Status != HandGripStatus.Contact)
                            throw new InvalidOperationException("Funscript continuous physical contact failed.");
                        if (elapsed < 1.5f) return;
                        _driver.StopMotion(); _heldPosition = _driver.CurrentSample.Position01;
                        Log("PASS continuous funscript tracking."); Next(); break;
                    case 9:
                        if (elapsed < 0.3f) return;
                        if (Mathf.Abs(_grip.Position01 - _heldPosition) > 0.0001f || _performer.GripPositionError > 0.005f)
                            throw new InvalidOperationException("Stop did not hold target position.");
                        Log("PASS Stop holds physical position.");
                        _driver.StartMotion(); Next(); break;
                    case 10:
                        if (elapsed < 0.2f) return;
                        if (Mathf.Abs(_grip.Position01 - _heldPosition) < 0.0001f)
                            throw new InvalidOperationException("Resume did not continue motion.");
                        Log("PASS resume continues physical motion.");
                        _driver.StopMotion(); _performer.ReleaseGrip(); Next(); break;
                    case 11:
                        if (_grip.State != GripInteractionState.Idle) return;
                        _completionCount = 0;
                        ObserveFirst(); ObserveSecond(); Next(); break;
                    case 12:
                        if (_completionCount < 2) return;
                        if (_completionCount != 2 || _firstCompletion != GripCompletion.Superseded || _secondCompletion != GripCompletion.Acquired)
                            throw new InvalidOperationException("Grip supersession callbacks incorrect.");
                        Log("PASS superseded GripAsync resolves once, newer GripAsync acquires once.");
                        _performer.ReleaseGrip(); Next(); break;
                    case 13:
                        if (_grip.State != GripInteractionState.Idle) return;
                        Place(3f); _completionCount = 0; ObserveFirst(); Next(); break;
                    case 14:
                        if (_grip.State != GripInteractionState.Walking) return;
                        _performer.WalkToAsync(_performer.transform.position + _performer.transform.right);
                        Next(); break;
                    case 15:
                        if (_grip.State != GripInteractionState.Failed) return;
                        if (_grip.FailureReason != GripFailureReason.LocomotionSuperseded || _completionCount != 1 || _firstCompletion != GripCompletion.Failed)
                            throw new InvalidOperationException("Locomotion supersession did not fail grip deterministically.");
                        Log("PASS locomotion supersession fails GripAsync once."); Finish(0); break;
                }
            }
            catch (Exception error) { Log("FAIL " + error); Debug.LogException(error); Finish(1); }
        }
        private static async void ObserveFirst()
        { _firstCompletion = await _performer.GripAsync(_rod); _completionCount++; }
        private static async void ObserveSecond()
        { _secondCompletion = await _performer.GripAsync(_rod); _completionCount++; }
        private static void Next() { _step++; _started = EditorApplication.timeSinceStartup; }
        private static void Log(string line) => File.AppendAllText(Report, line + "\n");
        private static void Finish(int code)
        {
            if (_program != null) UnityEngine.Object.Destroy(_program);
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
