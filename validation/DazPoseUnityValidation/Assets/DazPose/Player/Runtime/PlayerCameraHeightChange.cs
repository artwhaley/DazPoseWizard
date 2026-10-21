using System;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>Anything that can darken the whole view: 0 is clear, 1 is fully black.</summary>
 public interface IScreenFade { float Alpha { get; set; } }
 public enum PlayerCameraHeightResult { Completed, Cancelled }
 /// <summary>
 /// Fade to black, move, fade back in. Pure sequencing driven by Advance(deltaTime), so tests need no rendering.
 /// The fade is always left clear when the change ends for any reason.
 /// </summary>
 public sealed class PlayerCameraHeightChange {
  public enum Stage { FadingOut, Holding, FadingIn, Done }
  readonly IScreenFade fade;
  readonly Action move;
  readonly float fadeOutSeconds,fadeInSeconds,holdSeconds;
  float elapsed;
  public PlayerCameraHeightChange(IScreenFade screenFade,Action moveWhileBlack,float fadeOut,float fadeIn,float hold=0f) {
   fade=screenFade ?? throw new ArgumentNullException(nameof(screenFade));
   move=moveWhileBlack ?? throw new ArgumentNullException(nameof(moveWhileBlack));
   if(!(fadeOut>=0f) || float.IsInfinity(fadeOut) || !(fadeIn>=0f) || float.IsInfinity(fadeIn) || !(hold>=0f) || float.IsInfinity(hold)) throw new ArgumentOutOfRangeException(nameof(fadeOut),"Fade and hold durations must be finite and non-negative.");
   fadeOutSeconds=fadeOut; fadeInSeconds=fadeIn; holdSeconds=hold;
  }
  public Stage Current { get; private set; } = Stage.FadingOut;
  public bool IsDone => Current==Stage.Done;
  public PlayerCameraHeightResult Result { get; private set; }
  /// <summary>Set when the move itself failed; the change then ends Cancelled with the fade clear.</summary>
  public Exception Error { get; private set; }
  public bool Moved { get; private set; }
  public event Action<PlayerCameraHeightChange> Finished;
  public void Advance(float deltaTime) {
   if(IsDone) return;
   if(deltaTime>0f) elapsed+=deltaTime;
   while(!IsDone) {
    if(Current==Stage.FadingOut) {
     if(fadeOutSeconds>0f && elapsed<fadeOutSeconds) { fade.Alpha=Smooth(elapsed/fadeOutSeconds); return; }
     elapsed=fadeOutSeconds>0f ? elapsed-fadeOutSeconds : elapsed;
     fade.Alpha=1f;
     try { move(); Moved=true; }
     catch(Exception error) { Error=error; End(PlayerCameraHeightResult.Cancelled); return; }
     Current=Stage.Holding;
    } else if(Current==Stage.Holding) {
     // A short beat of full black after the move, so the cut is never seen.
     if(holdSeconds>0f && elapsed<holdSeconds) return;
     elapsed=holdSeconds>0f ? elapsed-holdSeconds : elapsed;
     Current=Stage.FadingIn;
    } else {
     if(fadeInSeconds>0f && elapsed<fadeInSeconds) { fade.Alpha=1f-Smooth(elapsed/fadeInSeconds); return; }
     End(PlayerCameraHeightResult.Completed); return;
    }
   }
  }
  /// <summary>Stops wherever it is and clears the fade immediately. Does nothing once finished.</summary>
  public void Cancel() { if(!IsDone) End(PlayerCameraHeightResult.Cancelled); }
  void End(PlayerCameraHeightResult result) {
   Current=Stage.Done; Result=result; fade.Alpha=0f;
   Finished?.Invoke(this);
  }
  /// <summary>Smootherstep: gentler at both ends than smoothstep, so the fade eases in and out softly.</summary>
  static float Smooth(float t) { t=Mathf.Clamp01(t); return t*t*t*(t*(t*6f-15f)+10f); }
 }
}
