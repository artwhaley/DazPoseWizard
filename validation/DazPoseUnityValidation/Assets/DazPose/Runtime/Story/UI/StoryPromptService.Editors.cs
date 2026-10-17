using System.Threading;
using DazPose.Game;
using UnityEngine;
namespace DazPose.Story {
 public sealed partial class StoryPromptService {
  public Awaitable<StoryPromptResult> ShowKinkEditorAsync(PlayerProfileService profiles,KinkCatalog catalog,CancellationToken cancellation=default) {
   var draft=PlayerProfileService.Clone(profiles.Current);
   return Show("Interests","Apply interests",parent=>{
    if(profiles.LastLoadDiagnostic!=null) StoryUi.Label(parent,profiles.LastLoadDiagnostic,16);
    StoryInterestsPanel.Build(parent,draft,catalog);
   },()=>{
    draft.submissionScore=PlayerProfileService.CalculateSubmissionScore(draft,catalog);
    profiles.Commit(draft);
   },cancellation);
  }
  public Awaitable<StoryPromptResult> ShowEquipmentEditorAsync(PlayerProfileService profiles,EquipmentCatalog catalog,CancellationToken cancellation=default) {
   var draft=PlayerProfileService.Clone(profiles.Current);
   return Show("Equipment","Apply equipment",parent=>StoryEquipmentPanel.Build(parent,draft,catalog),()=>profiles.Commit(draft),cancellation);
  }
  public Awaitable<StoryPromptResult> ShowClothingEditorAsync(PlayerWorldState world,CancellationToken cancellation=default) {
   var draft=JsonUtility.FromJson<PlayerWorldState>(JsonUtility.ToJson(world));
   return Show("Clothing","Ready",parent=>StoryClothingPanel.Build(parent,draft),()=>{
    foreach(var slot in PlayerWorldState.ClothingSlots) world.SetClothing(slot,draft.GetClothing(slot));
   },cancellation);
  }
 }
}