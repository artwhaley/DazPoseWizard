using DazPose.Game;
using DazPose.Story;
namespace DazPose.Player.Tests;
public sealed class LargeCatalogTransactionTests {
 [Theory]
 [InlineData(100)]
 [InlineData(300)]
 public void FilteredDraftEditsSurviveSearchAndApplyOnce(int count) {
  using var directory=new TemporaryProfileDirectory();var profiles=new PlayerProfileService(directory.Path);
  var interests=Enumerable.Range(0,count).Select(i=>new KinkDefinition{id="interest-"+i,displayName="Interest "+i,description="test catalog"}).ToArray();
  var equipment=Enumerable.Range(0,count).Select(i=>new EquipmentDefinition{id="equipment-"+i,displayName="Equipment "+i}).ToArray();
  var catalog=new KinkCatalog{entries=interests};var original=PlayerProfileJson.Write(profiles.Current).ToString();
  using(var cancelled=new PlayerProfileEditSession(profiles,catalog)) {
   foreach(var item in interests)cancelled.SetFrequency(item.id,KinkFrequency.VeryFrequent);
   foreach(var item in equipment)cancelled.SetReward(item.id,true);
  }
  Assert.Equal(original,PlayerProfileJson.Write(profiles.Current).ToString());Assert.False(File.Exists(profiles.StoragePath));
  using var edit=new PlayerProfileEditSession(profiles,catalog);
  var indices=CatalogSearch.Find(interests,"interest-99",d=>d.id,d=>d.displayName,d=>d.id);Assert.Single(indices);
  string id=interests[indices[0]].id;edit.SetFrequency(id,KinkFrequency.VeryFrequent);edit.SetSentiment(id,KinkSentiment.Punishment);
  edit.SetFrequency(id,KinkFrequency.No);Assert.Equal(KinkSentiment.Punishment,edit.GetKink(id).sentiment);
  edit.SetFrequency(id,KinkFrequency.Normal);
  edit.SetEquipmentOwnedAndWilling(equipment[99].id,true);edit.SetReward(equipment[99].id,true);edit.SetPunishment(equipment[99].id,true);edit.SetEquipmentOwnedAndWilling(equipment[99].id,false);
  Assert.Empty(CatalogSearch.Find(interests,"no results here",d=>d.id,d=>d.displayName,d=>d.id));
  Assert.Equal(KinkFrequency.Normal,edit.GetKink(id).frequency);Assert.True(edit.Commit());Assert.False(edit.Commit());
  var reloaded=new PlayerProfileService(directory.Path);
  Assert.Equal(KinkSentiment.Punishment,Assert.Single(reloaded.Current.kinks).sentiment);
  var savedEquipment=Assert.Single(reloaded.Current.equipment);Assert.False(savedEquipment.ownedAndWilling);Assert.True(savedEquipment.useAsReward);Assert.True(savedEquipment.useAsPunishment);
  Assert.Equal(PlayerProfileService.CalculateSubmissionScore(reloaded.Current,catalog),reloaded.Current.submissionScore);
 }
}
