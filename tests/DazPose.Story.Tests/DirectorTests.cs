using DazPose.Story.Language;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Story.Tests;
public sealed class DirectorTests {
 internal class Host : IStoryCardPresentationHost,IStoryExecutionHost,IStoryDirectorObserver,IStoryPhaseObserver {
  public readonly DazPose.Tests.RecordingCardPresentation Presentation=new();
  public Task<IStoryCardPresentationScope> EnterCardAsync(CardPresentationRequest request,CancellationToken cancellation)=>Presentation.EnterAsync(request,cancellation);
  public readonly Dictionary<string,StoryData> State=new() {
   ["Session.Mood"]=new(50d),["Session.Phase"]=new("Setup"),["Session.PhaseCompletion"]=new(0d),["Session.IntensityMultiplier"]=new(1d),["Contract.PacingMultiplier"]=new(1d),
   ["Lara.Posture"]=new("Standing"),["Player.Posture"]=new("Lying"),["World.Hands"]=new("Free"),["World.Ankles"]=new("Free"),["World.Oral"]=new("Free"),["World.Anal"]=new("Free"),["World.Genital"]=new("Free"),
   ["Session.PreparedEquipment"]=new(Array.Empty<string>()),["Session.PreparedEquipment.Count"]=new(0d),["Session.ActiveEquipment"]=new(Array.Empty<string>())
  };
  public readonly HashSet<string> Clothing=new(),Active=new();
  public readonly List<StoryCommand> Commands=new();
  public readonly List<string> TraceLines=new(),Selected=new(),Rejected=new();
  public readonly Dictionary<string,double> PhaseCompletion=new();
  public Func<StoryCommand,CancellationToken,Task<StoryData>> Handler;
  public string Equipment="rope";public bool Early=true,Cancelled;public double MoodAtWarmup;
  public StoryData Read(string key) => key=="Completion" ? State["Session.PhaseCompletion"] : key=="World.Clothing.Count" ? new((double)Clothing.Count) : State[key];
  public void Write(string key,StoryData value)=>State[key]=value;
  public void LeavePhase(string phase)=>PhaseCompletion[phase]=Read("Completion").Numeric;
  public void ObserveDirector(DirectorSnapshot snapshot,CardExecutionContext context) {
   if(snapshot.Selected!=null && snapshot.Outcome.StartsWith("Succeeded"))Selected.Add(snapshot.Selected);
   foreach(var c in snapshot.Candidates.Where(c=>!c.Eligible))Rejected.Add(c.Id+": "+c.Reason);
  }
  public Task<StoryData> InvokeAsync(StoryCommand c,CancellationToken token) {
   Commands.Add(c);
   if(Handler!=null)return Handler(c,token);
   switch(c.Verb) {
    case "Session.SelectPreparedEquipment":State["Session.PreparedEquipment"]=new(Equipment==null ? Array.Empty<string>() : new[]{Equipment});State["Session.PreparedEquipment.Count"]=new(Equipment==null ? 0d : 1d);break;
    case "Lara.SitAt":MoodAtWarmup=Read("Session.Mood").Numeric;State["Lara.Posture"]=new("Sitting");break;
    case "Lara.PersistentPose":State["Lara.Posture"]=new("Standing");break;
    case "Lara.PersistentSitPose":State["Lara.Posture"]=new("Sitting");break;
    case "Lara.DissolveTo":State["Lara.Posture"]=new(c.Arguments.ContainsKey("seated") ? "Sitting" : "Standing");break;
    case "World.Clothing.MarkRemoved":Clothing.Remove(c.Require("item").String);break;
    case "World.Clothing.Clear":Clothing.Clear();break;
    case "Equipment.Activate":Active.Add(c.Require("equipment").String);State["Session.ActiveEquipment"]=new(Active.ToArray());break;
   }
   return Task.FromResult(default(StoryData));
  }
  public Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation)=>Task.FromResult(new StoryData(Early ? "UI.Pressed:ICame" : "MotionDriver.Ended"));
  public StoryData Function(string name,StoryData[] args)=>name switch {
   "round"=>new(Math.Round(args[0].Numeric,MidpointRounding.AwayFromZero)),
   "World.Clothing.LeastRisqueWorn"=>new(new[]{"shoes","socks","shirt","pants","bra","underwear"}.First(Clothing.Contains)),
   "UI.Pressed"=>new("UI.Pressed:"+args[0].String),
   "Equipment.Tags"=>new(new[]{"equipment:"+args[0].String}),
   "Equipment.IsPrepared"=>new(State["Session.PreparedEquipment"].Items.Contains(args[0].String)),
   "Equipment.ActiveHasTag"=>new(Active.Any(id=>"equipment:"+id==args[0].String)),
   _=>throw new InvalidOperationException(name)
  };
  public Task YieldAsync(CancellationToken cancellation)=>Task.CompletedTask;
  public void CancelOperations()=>Cancelled=true;
  public void Trace(StorySource source,string message)=>TraceLines.Add(message);
 }
 [Fact] public async Task ExtractedGlobalCardsPreservePreCleanupSeed82017() {
  var host=new Host();await new StoryMachine(Actual(),host,new StorySessionRandom(82017)).RunAsync("FirstContact");
  Assert.Equal(new[]{"WarmupCheckIn","WarmupBreathing","WarmupCounting","WarmupCheckIn","WarmupCounting","MainStandingVariation","MainStandingCheckIn","EquipmentRope","MainSeatedPause","MainSeatedCheckIn","MainSeatedRhythm","ResolutionReflection"},host.Selected);
  Assert.Equal(110,host.PhaseCompletion["Warmup"]);Assert.Equal(105,host.PhaseCompletion["Main"]);
 }
 static CompiledStoryProgram Actual()=>AuthoredStoryTestContent.Compile();
 static CompiledStoryProgram Compile(string source)=>DazStoryCompiler.Compile(source,"cards.dazstory");
 [Theory][InlineData(0,"rope",true)][InlineData(2,null,false)][InlineData(4,"ice",true)]
 public async Task FullAuthoredSourceCompletesAllPhasesAndExactBranches(int clothing,string equipment,bool early) {
  var host=new Host {Equipment=equipment,Early=early};foreach(var slot in new[]{"shoes","socks","shirt","pants"}.Take(clothing))host.Clothing.Add(slot);
  var machine=new StoryMachine(Actual(),host,new StorySessionRandom(82017));await machine.RunAsync("FirstContact");
  Assert.Equal(new[]{"Warmup","Main","Climax","Resolution"},host.PhaseCompletion.Keys);
  Assert.True(host.PhaseCompletion["Warmup"]>=100);Assert.True(host.PhaseCompletion["Main"]>=100);Assert.Empty(host.Clothing);
  Assert.Equal(clothing==0 ? 55 : 50,host.MoodAtWarmup);
  Assert.Equal(equipment!=null,host.Commands.Any(c=>c.Verb=="Popup.ShowNeededEquipment"));
  Assert.Equal(clothing>2,host.Commands.Any(c=>c.Verb=="Lara.PlayOneShotGesture"));
  Assert.Equal(clothing>0,host.Commands.Any(c=>c.Verb=="World.Clothing.Clear"));
  Assert.Equal(!early,host.Commands.Any(c=>c.Verb=="MotionDriver.PlayFunscript" && c.Require("program").String=="FinishLoop"));
  Assert.Equal(equipment!=null,host.Active.Count>0);
  Assert.Contains(host.Rejected,r=>r.StartsWith("WarmupStandingOnly: Required Lara.Posture == Standing"));
  Assert.DoesNotContain(host.Commands,c=>c.Source.declaration=="card WarmupStandingOnly");
  Assert.Contains(host.Selected,id=>id.StartsWith("Resolution"));
  Assert.Equal(1.4,host.State["Session.IntensityMultiplier"].Numeric,6);
  Assert.Equal(clothing==0 ? 16 : 17,host.Commands.Count(c=>c.Verb=="Lara.Say"));
  Assert.Contains(host.TraceLines,t=>t.StartsWith("Story completed: FirstContact"));
 }
 [Theory][InlineData("butt-plug")][InlineData("rope")][InlineData("dildo-anal")][InlineData("dildo-oral")][InlineData("ice")]
 public async Task EveryPreparedItemHasAnExecutableEquipmentCard(string equipment) {
  var program=Actual();CardCatalog.Validate(program,new[]{"butt-plug","rope","dildo-anal","dildo-oral","ice"});
  var host=new Host {Equipment=equipment};await new StoryMachine(program,host,new StorySessionRandom(9)).RunAsync("FirstContact");
  Assert.Contains(equipment,host.Active);
 }
 [Fact]public async Task SameSeedAndPathReproduceSelectedCardsAndLocalRandomness() {
  var a=new Host();var b=new Host();var program=Actual();
  await new StoryMachine(program,a,new StorySessionRandom(41)).RunAsync("FirstContact");await new StoryMachine(program,b,new StorySessionRandom(41)).RunAsync("FirstContact");
  Assert.Equal(a.Selected,b.Selected);
  Assert.Equal(a.TraceLines.Where(l=>l.StartsWith("Set beats") || l.StartsWith("Set pause")),b.TraceLines.Where(l=>l.StartsWith("Set beats") || l.StartsWith("Set pause")));
  var x=new StorySessionRandom(99);var y=new StorySessionRandom(99);for(int i=0;i<100;i++)x.CardLocal.Next();Assert.Equal(x.Selection.Next(),y.Selection.Next());
 }
 [Theory][InlineData("progress -1")][InlineData("weight 0")][InlineData("repeat 0")][InlineData("cooldown -1")][InlineData("tags [Warmpu]")][InlineData("requires random(1..2) > 0")][InlineData("requires 10")]
 public void InvalidMetadataFailsBeforePlay(string metadata)=>Assert.Throws<StorySourceException>(()=>Compile("card A\n"+metadata+"\n{\nwait 1s\n}\nsequence Test {\nplay A\n}\n"));
 [Fact]public async Task CooldownAndOnceAreHardRejectionsAndPacingOnlyScalesProgress() {
  var source="card A\ntags [Warmup]\nprogress 10\nrepeat once\ncooldown 1\n{\nSession.Mood += 3\n}\ncard B\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\nsequence Test {\nphase Warmup {\nawait Director.Execute(A)\nawait Director.DrawAndExecute(tags: [Warmup])\n}\n}\n";
  var host=new Host();host.State["Contract.PacingMultiplier"]=new(.33d);var machine=new StoryMachine(Compile(source),host);await machine.RunAsync("Test");
  Assert.Equal(6.6,host.Read("Completion").Numeric,8);Assert.Equal(53,host.Read("Session.Mood").Numeric);Assert.Equal(1,machine.Director.History.For("A").TimesUsed);Assert.Contains(host.Rejected,r=>r.Contains("Maximum 1 uses reached"));
 }
 [Fact]public async Task BackgroundCardWorkDrainsBeforeProgressAndFailureReceivesNoCredit() {
  var source=Compile("card A\ntags [Warmup]\nprogress 20\n{\nLara.PersistentPose(SexyVamp)\n}\nsequence Test {\nphase Warmup {\nawait Director.Execute(A)\n}\n}\n");
  var pending=new TaskCompletionSource<StoryData>();var host=new Host();host.Handler=(c,t)=>{t.Register(()=>pending.TrySetCanceled(t));return pending.Task;};
  var machine=new StoryMachine(source,host);var run=machine.RunAsync("Test");Assert.False(run.IsCompleted);Assert.Equal(0,host.Read("Completion").Numeric);
  pending.SetException(new InvalidOperationException("pose failed"));await Assert.ThrowsAsync<StorySourceException>(()=>run);Assert.Equal(0,host.Read("Completion").Numeric);Assert.Equal(0,machine.Director.History.SelectionCount);
 }
 [Fact]public async Task ZeroProgressCompletionPoolFailsBeforeExecutingAndExplainsQuery() {
  var host=new Host();var program=Compile("card Zero\ntags [Warmup]\nprogress 0\n{\nwait 1s\n}\nsequence Test {\nphase Warmup {\nwhile Completion < 40% {\nawait Director.DrawAndExecute(tags: [Warmup])\n}\n}\n}\n");
  var error=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(program,host).RunAsync("Test"));Assert.Contains("Warmup cannot advance",error.Message);Assert.Contains("completion 0/40",error.Message);Assert.Empty(host.Commands);
 }
 [Fact]public async Task RacesRemoveLosersDoNotReplayOldEventsAndCancelCleanly() {
  var hub=new StorySignalHub();hub.Emit("old");var cancel=new CancellationTokenSource();
  var race=hub.WaitAnyAsync(new[]{"motion","button"},cancel.Token);Assert.Equal(2,hub.SubscriptionCount);hub.Emit("button");Assert.Equal("button",await race);Assert.Equal(0,hub.SubscriptionCount);
  hub.Emit("motion");var next=hub.WaitAnyAsync(new[]{"motion","button"},cancel.Token);Assert.False(next.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>next);Assert.Equal(0,hub.SubscriptionCount);
 }
 [Fact]public async Task CooldownRejectsExplicitSelectionUntilEnoughOtherCardsComplete() {
  var source=Compile("card A\ntags [Warmup]\nprogress 10\ncooldown 2\n{\nwait 1s\n}\ncard B\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\nsequence Test {\nphase Warmup {\nawait Director.Execute(A)\nawait Director.Execute(B)\nawait Director.Execute(A)\n}\n}\n");
  var host=new Host();var machine=new StoryMachine(source,host);var error=await Assert.ThrowsAsync<StorySourceException>(()=>machine.RunAsync("Test"));
  Assert.Contains("0 eligible",error.Message);Assert.Contains(host.Rejected,r=>r.Contains("Cooldown: 1 cards since use; requires 2"));Assert.Equal(20,host.Read("Completion").Numeric);
 }
 [Fact]public async Task ExplicitCardStillRejectsIllegalPhysicalRequirements() {
  var host=new Host();host.State["World.Ankles"]=new("Restricted");
  var source=Compile("card Jump\ntags [Warmup]\nprogress 20\nrequires World.Ankles == Free\n{\nwait 1s\n}\nsequence Test {\nawait Director.Execute(Jump)\n}\n");
  await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(source,host).RunAsync("Test"));
  Assert.Empty(host.Commands);Assert.Contains(host.Rejected,r=>r.Contains("World.Ankles = Restricted"));
 }
 [Fact]public async Task StopCancelsAnOwnedCardAndDoesNotCreditProgress() {
  var host=new Host();var stop=new CancellationTokenSource();var pending=new TaskCompletionSource<StoryData>();
  host.Handler=(c,t)=> {t.Register(()=>pending.TrySetCanceled(t));return pending.Task;};
  var source=Compile("card A\ntags [Warmup]\nprogress 20\n{\nwait 10s\n}\nsequence Test {\nphase Warmup {\nawait Director.Execute(A)\n}\n}\n");
  var machine=new StoryMachine(source,host);var run=machine.RunAsync("Test",stop.Token);stop.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run);
  Assert.Equal(0,host.Read("Completion").Numeric);Assert.Equal(0,machine.Director.History.SelectionCount);Assert.True(host.Cancelled);
 }

 [Fact]public async Task EmptyOptionalEquipmentDrawSkipsWithoutInventingCardsOrProgress() {
  var host=new Host {Equipment=null};var source=Compile("sequence Test {\nawait Director.DrawAndExecute(equipment: [])\n}\n");
  var machine=new StoryMachine(source,host);await machine.RunAsync("Test");
  Assert.Empty(host.Commands);Assert.Equal(0,machine.Director.History.SelectionCount);Assert.Equal(0,host.Read("Completion").Numeric);Assert.Contains(host.TraceLines,t=>t.Contains("Optional equipment beat skipped"));
 }
 [Fact]public async Task CardInvocationAndHistoryPersistAcrossPhasesAndIntensityIsAnInput() {
  var host=new Host();host.State["Contract.PacingMultiplier"]=new(.5d);
  var source=Compile("card A\nprogress 10\n{\nSession.Mood += Card.Intensity\nwait Card.Invocation\n}\nsequence Test {\nphase Warmup {\nawait Director.Execute(A)\n}\nSession.IntensityMultiplier = 1.4\nphase Main {\nawait Director.Execute(A)\n}\n}\n");
  var machine=new StoryMachine(source,host);await machine.RunAsync("Test");
  Assert.Equal(52.4,host.Read("Session.Mood").Numeric,8);Assert.Equal(5,host.Read("Completion").Numeric);Assert.Equal(2,machine.Director.History.For("A").TimesUsed);
  Assert.Equal(new[]{1d,2d},host.Commands.Where(c=>c.Verb=="Wait").Select(c=>c.Require("seconds").Numeric));
 }
 [Theory][InlineData("card A\nrequires Equipment.IsPrepared()\n{\nwait 1s\n}\n")][InlineData("sequence Test {\nawait Director.Execute(UnknownCard)\n}\n")]
 public void InvalidRequirementsAndUnresolvedExplicitCardsFailBeforePlay(string source)=>Assert.Throws<StorySourceException>(()=>Compile(source));

}
