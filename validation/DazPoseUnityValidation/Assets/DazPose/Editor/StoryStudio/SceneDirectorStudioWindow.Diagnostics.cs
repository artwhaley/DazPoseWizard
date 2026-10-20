using System;
using UnityEngine;
namespace DazPose.Editor.Studio {
 [Serializable] public sealed class NativeStudioObservation {
  public string kind,source,cue,composition,observation,failure;
  public int indexBuilds,statements,lookups,generation,staleRequests,fullProjections,spatialProjections,poseSamples,poseUpdates;
  public double indexMilliseconds,lookupMilliseconds,lastProjectionMilliseconds,requestMilliseconds;
  public NativeSeatObservation seating;
 }
 public sealed partial class SceneDirectorStudioWindow {
  internal static event Action<NativeStudioObservation> DiagnosticObserved;
  internal void StopDirecting()=>Release();internal bool IsDirecting=>stage!=null&&stage.Ready;
  void ObserveNative(string kind,string failure=null){DiagnosticObserved?.Invoke(new NativeStudioObservation{kind=kind,source=selectedSource?.ToString(),cue=selectedCue?.Node.Id,composition=stage?.Composition,observation=string.Join("\n",stage?.AuthoringWarnings??Array.Empty<string>()),failure=failure,indexBuilds=indexBuilds,statements=cueIndex?.StatementsAnalyzed??0,lookups=lookupCount,generation=generation,staleRequests=staleRequests,fullProjections=stage?.FullProjectionCount??0,spatialProjections=stage?.SpatialProjectionCount??0,poseSamples=stage?.PoseSampleCount??0,poseUpdates=stage?.AuthoringPoseUpdates??0,indexMilliseconds=cueIndex?.BuildMilliseconds??0,lookupMilliseconds=lookupMilliseconds,lastProjectionMilliseconds=stage?.LastProjectionMilliseconds??0,requestMilliseconds=requestMilliseconds,seating=stage?.LastSeating});}
 }
}
