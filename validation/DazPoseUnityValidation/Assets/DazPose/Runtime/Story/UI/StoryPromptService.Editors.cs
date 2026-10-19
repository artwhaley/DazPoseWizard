using System.Threading;
using DazPose.Game;
using UnityEngine;
namespace DazPose.Story {
 public sealed partial class StoryPromptService {
  public async Awaitable<StoryPromptResult> ShowKinkEditorAsync(PlayerProfileService profiles,KinkCatalog catalog,CancellationToken cancellation=default) {
   using(var draft=new PlayerProfileEditSession(profiles,catalog))
    return await Show("Interests","Apply interests",parent=>StoryModalPresentation.Factory.Interests(parent,draft,catalog),()=>draft.Commit(),cancellation,true);
  }
  public async Awaitable<StoryPromptResult> ShowEquipmentEditorAsync(PlayerProfileService profiles,EquipmentCatalog catalog,CancellationToken cancellation=default) {
   using(var draft=new PlayerProfileEditSession(profiles))
    return await Show("Equipment","Apply equipment",parent=>StoryModalPresentation.Factory.Equipment(parent,draft,catalog),()=>draft.Commit(),cancellation,true);
  }
  public async Awaitable<StoryPromptResult> ShowClothingEditorAsync(PlayerWorldState world,CancellationToken cancellation=default) {
   using(var draft=new ClothingEditSession(world))
    return await Show("Clothing","Ready",parent=>StoryModalPresentation.Factory.Clothing(parent,draft),()=>draft.Commit(),cancellation,true);
  }
 }
}
