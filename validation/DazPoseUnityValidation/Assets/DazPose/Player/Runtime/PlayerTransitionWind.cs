using System;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>
 /// A soft wind-blowing swell for camera-height transitions, generated in code so no audio asset is needed.
 /// Low-passed noise whose brightness rises and falls with a gust, under a smooth swell envelope.
 /// </summary>
 public static class PlayerTransitionWind {
  public const int SampleRate=24000;
  /// <summary>Mono samples in -1..1 that start and end at silence.</summary>
  public static float[] Samples(float seconds,int seed=7) {
   if(!(seconds>0f) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
   int count=Mathf.Max(2,Mathf.RoundToInt(seconds*SampleRate));
   var samples=new float[count];
   var random=new System.Random(seed);
   float low=0f,low2=0f,peak=1e-6f;
   for(int i=0;i<count;i++) {
    float t=i/(float)(count-1);
    float swell=Mathf.Sin(Mathf.PI*t); swell*=swell;                       // 0 -> 1 -> 0
    float gust=Mathf.Lerp(0.015f,0.09f,Mathf.Sin(Mathf.PI*t));              // brighter at the peak
    float noise=(float)(random.NextDouble()*2.0-1.0);
    low+=gust*(noise-low); low2+=gust*(low-low2);                           // two one-pole low-pass stages
    float value=low2*swell;
    samples[i]=value; peak=Mathf.Max(peak,Mathf.Abs(value));
   }
   float scale=0.9f/peak;
   for(int i=0;i<count;i++) samples[i]*=scale;
   samples[0]=0f; samples[count-1]=0f;
   return samples;
  }
  public static AudioClip Create(float seconds) {
   var samples=Samples(seconds);
   var clip=AudioClip.Create("Camera Transition Wind",samples.Length,1,SampleRate,false);
   clip.hideFlags=HideFlags.DontSave;
   clip.SetData(samples,0);
   return clip;
  }
 }
}
