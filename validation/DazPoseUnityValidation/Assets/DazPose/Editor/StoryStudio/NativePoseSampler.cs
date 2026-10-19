using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace DazPose.Editor.Studio
{
    /// <summary>One manual graph for the lifetime of one preview lease. Suspends without surrendering its identity.</summary>
    internal sealed class NativePoseSampler : IDisposable
    {
        readonly Animator animator;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        AnimationClip currentClip;
        public NativePoseSampler(Animator target)
        {
            animator=target;
            if(!animator.isActiveAndEnabled)throw new InvalidOperationException("Lara's saved Animator must already be active and enabled.");
            // This test belongs ONLY at initial acquisition. Destroy/recreate on each handle move leaves
            // Unity's hasBoundPlayables flag set until a later native update and rejects our own graph.
            if(animator.hasBoundPlayables)throw new InvalidOperationException("Another PlayableGraph already owns Lara's Animator. Stop that preview first.");
            try
            {
                graph=PlayableGraph.Create("DazPose Native Preview Sampling");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                output=AnimationPlayableOutput.Create(graph,"Lara pose",null);
            }
            catch { if(graph.IsValid())graph.Destroy();throw; }
        }
        public void Sample(AnimationClip clip)
        {
            if(!graph.IsValid())throw new InvalidOperationException("Native preview's owned sampling graph was destroyed externally.");
            if(currentClip!=clip)
            {
                output.SetSourcePlayable(Playable.Null);
                if(playable.IsValid())graph.DestroyPlayable(playable);
                playable=AnimationClipPlayable.Create(graph,clip);
                playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
                output.SetSourcePlayable(playable);currentClip=clip;
            }
            playable.SetTime(0);output.SetTarget(animator);graph.Play();
            AnimationMode.BeginSampling();
            try { AnimationMode.SamplePlayableGraph(graph,0,0); }
            finally { AnimationMode.EndSampling(); }
        }
        public void Suspend()
        {
            if(!graph.IsValid())return;
            // Detach explicitly before restoration/serialization. The private graph remains owned and reusable.
            output.SetTarget(null);graph.Stop();
        }
        public void Dispose()
        {
            if(!graph.IsValid())return;
            try { Suspend(); } finally { graph.Destroy(); }
        }
    }
}
