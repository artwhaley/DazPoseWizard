using DazPose.Game;
namespace DazPose.Player.Tests;
public sealed class CatalogSearchTests {
 [Theory]
 [InlineData(100)]
 [InlineData(300)]
 public void SearchAndFiltersRetainStableCatalogIndices(int count) {
  var rows=Enumerable.Range(0,count).Select(i=>(id:"item"+i,name:"Name "+i,description:i%2==0?"metal restraint":"soft cloth")).ToArray();
  int[] Find(string q,bool metal=false)=>CatalogSearch.Find(rows,q,r=>r.id+" "+r.name+" "+r.description,r=>r.name,r=>r.id,r=>!metal||r.description.StartsWith("metal"));
  Assert.Equal(count,Find("").Length);
  Assert.Equal(count/2,Find("METAL").Length);
  Assert.Empty(Find("not-present"));
  Assert.Single(Find("item99 "));
  Assert.Equal(count/2,Find("",true).Length);
  var changes=new Dictionary<int,bool>{{99,true}};
  _=Find("metal");Assert.True(changes[99]);Assert.Contains(99,Find("item99"));
  Assert.Equal(Find(""),Find(""));
 }
}
