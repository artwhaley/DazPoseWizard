using System;
using System.Linq;
using UnityEngine;
namespace DazPose.Editor.Studio {
 [Serializable] public sealed class NativeStudioObservation {
  public string kind,source,visit,composition,observation,failure,scenario;
  public int effects,history,seed,directorSelections,restages,fullProjections,spatialProjections,poseSamples;
  public double lastProjectionMilliseconds;
  public NativeSeatObservation seating;
 }
 public sealed partial class SceneDirectorStudioWindow {
  internal static event Action<NativeStudioObservation> DiagnosticObserved;
  int directorSelections,nativeRestages;
  internal void StopDirecting()=>Release();
  internal bool IsDirecting=>stage!=null&&stage.Ready;
  void ObserveNative(string kind,string failure=null) {
   var effect=visualState.Effects.LastOrDefault();
   DiagnosticObserved?.Invoke(new NativeStudioObservation{kind=kind,source=effect?.Source?.ToString(),visit=effect?.Visit?.Key,
    composition=stage?.Composition,observation=visualState.Observation,failure=failure,effects=visualState.Effects.Count,
    history=rehearsal?.History.Count??0,seed=rehearsal?.Scenario.seed??scenario.seed,directorSelections=directorSelections,restages=nativeRestages,
    fullProjections=stage?.FullProjectionCount??0,spatialProjections=stage?.SpatialProjectionCount??0,poseSamples=stage?.PoseSampleCount??0,lastProjectionMilliseconds=stage?.LastProjectionMilliseconds??0,
    scenario=rehearsal?.ExportScenarioJson(),seating=stage?.LastSeating});
  }
 }
}
