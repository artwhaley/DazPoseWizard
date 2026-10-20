using System;
using System.IO;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    // Explicit saved-scene checks only. These methods never create scenes or write the public catalog.
    public static class AuthoringBatch
    {
        static double deadline;
        static bool entered;
        const string FirstContact = "Assets/Scenes/FirstContact.unity";
        const string Showcase = "Assets/Examples/Scenes/AuthoringShowcase.unity";

        public static void PlayFirstContact() => PlaySaved(FirstContact, "FirstContact");
        public static void PlayShowcase() => PlaySaved(Showcase, "AuthoringShowcase");

        static void PlaySaved(string path, string reportName)
        {
            if (path != FirstContact && path != Showcase)
                throw new InvalidOperationException("Only saved First Contact and Authoring Showcase checks are supported.");

            AuthoringContentComposer.ValidateSavedContent();
            EditorSceneManager.OpenScene(path);
            SessionState.SetString("DazPose.Authoring.PlayScene", reportName);
            SessionState.SetBool("DazPose.B05.Play", true);
            entered = false;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += ObserveSavedScene;
            EditorApplication.isPlaying = true;
        }

        static void ObserveSavedScene()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isPlaying)
            {
                var hosts = GameSessionSceneSetup.InScene<PerformanceHost>(SceneManager.GetActiveScene());
                if (hosts.Length == 1 && hosts[0].State != PerformanceHostState.Idle)
                {
                    entered = true;
                    if (hosts[0].State == PerformanceHostState.Completed)
                    { Finish(true, "Exact saved scene completed through its saved host."); return; }
                    if (hosts[0].State == PerformanceHostState.Failed)
                    { Finish(false, hosts[0].Failure?.ToString()); return; }
                }
            }
            if (EditorApplication.timeSinceStartup > deadline)
                Finish(false, "Saved scene timed out; entered=" + entered);
        }

        static void Finish(bool passed, string details)
        {
            SessionState.SetBool("DazPose.B05.Play", false);
            EditorApplication.update -= ObserveSavedScene;
            Directory.CreateDirectory("TestOutput/Authoring");
            var name = SessionState.GetString("DazPose.Authoring.PlayScene", "SavedScene");
            File.WriteAllText("TestOutput/Authoring/" + name + ".txt",
                DateTime.UtcNow.ToString("O") + "\n" + passed + "\n" + details);
            Debug.Log("AUTHORING_SAVED_PLAY: " + passed + "; " + details);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}