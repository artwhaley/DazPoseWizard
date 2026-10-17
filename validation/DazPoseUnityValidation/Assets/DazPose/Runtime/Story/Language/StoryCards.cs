using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace DazPose.Story.Language {
 [Serializable] public sealed class CardRequirement {public int Expression;public string Description;public StorySource Source;}
 [Serializable] public sealed class CardDefinition {
  public string Id,ProgramEntry;
  public string[] Tags=Array.Empty<string>();
  public CardRequirement[] Requirements=Array.Empty<CardRequirement>();
  public double BaseWeight=1,Progress;
  public int MaxUsesPerSession=-1,MinCardsBetweenUses;
  public StorySource Source;
 }
 public static class CardCatalog {
  static readonly HashSet<string> controlled=new HashSet<string>(new[]{"Warmup","Main","Standing","Sitting","Resolution","Energetic","Calm","Focus","Recovery"},StringComparer.Ordinal);
  public static void Validate(CardDefinition card) {
   void Invalid(string reason)=>throw new StorySourceException(card.Source,"Invalid card "+card.Id+": "+reason);
   if(string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.ProgramEntry))Invalid("ID and program entry are required.");
   if(double.IsNaN(card.BaseWeight) || double.IsInfinity(card.BaseWeight) || card.BaseWeight<=0 || double.IsNaN(card.Progress) || double.IsInfinity(card.Progress) || card.Progress<0)Invalid("Weight must be positive and progress nonnegative, both finite.");
   if(card.MaxUsesPerSession!=-1 && card.MaxUsesPerSession<1 || card.MinCardsBetweenUses<0 || card.MinCardsBetweenUses>10000)Invalid("Invalid repeat or cooldown count.");
   if(card.Tags.Distinct(StringComparer.Ordinal).Count()!=card.Tags.Length)Invalid("Duplicate tags.");
   foreach(string tag in card.Tags)if(!controlled.Contains(tag) && !ValidOpenTag(tag,"equipment:") && !ValidOpenTag(tag,"theme:"))Invalid("Unknown classification tag '"+tag+"'. Use a known classification or equipment:/theme: tag.");
  }
  static bool ValidOpenTag(string tag,string prefix)=>tag.StartsWith(prefix,StringComparison.Ordinal) && tag.Length>prefix.Length && tag.Substring(prefix.Length).All(c=>char.IsLetterOrDigit(c) || c=='-' || c=='_');
  public static void Validate(CompiledStoryProgram program,IEnumerable<string> equipmentIds=null) {
   var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var card in program.cards) {Validate(card);if(!ids.Add(card.Id))throw new StorySourceException(card.Source,"Duplicate card ID "+card.Id);program.RequireEntry(card.ProgramEntry,"card");}
   if(equipmentIds!=null)foreach(string id in equipmentIds)if(!program.cards.Any(c=>c.Tags.Contains("equipment:"+id)))throw new InvalidOperationException("First Contact lacks an equipment card for '"+id+"'.");
  }
 }
 public sealed class StorySessionRandom {
  public int Seed {get;}
  public Random Setup {get;} public Random Selection {get;} public Random CardLocal {get;}
  public StorySessionRandom(int seed) {Seed=seed;Setup=new Random(seed);Selection=new Random(unchecked(seed^0x517cc1b7));CardLocal=new Random(unchecked(seed^0x369dea0f));}
 }
 public sealed class CardExecutionContext {
  public string CardId,Phase;
  public int SessionSeed,InvocationNumber;
  public double IntensityMultiplier;
  public string[] PreparedEquipment,ActiveEquipment;
  public StorySessionRandom Random;
 }
 public sealed class CardSelectionRequest {
  public string[] RequiredTags=Array.Empty<string>(),EquipmentIds=Array.Empty<string>(),EquipmentTags=Array.Empty<string>();
  public string ExplicitCardId;
  public double Intensity=1,Completion,CompletionTarget;
  public bool RequireProgress,EquipmentRequested;
  public string Phase;
  public override string ToString()=>ExplicitCardId!=null ? "card "+ExplicitCardId : "tags ["+string.Join(", ",RequiredTags)+"]"+(EquipmentRequested ? "; equipment ["+string.Join(", ",EquipmentIds)+"]" : "");
 }
 public sealed class CardUse {public int TimesUsed,LastSelectionIndex=-1;}
 public sealed class CardHistory {
  readonly Dictionary<string,CardUse> uses=new Dictionary<string,CardUse>(StringComparer.Ordinal);
  readonly List<string> recent=new List<string>();
  public int SelectionCount {get;private set;}
  public IReadOnlyList<string> Recent=>recent;
  public CardUse For(string id)=>uses.TryGetValue(id,out var value) ? value : new CardUse();
  public void Record(string id) {
   if(!uses.TryGetValue(id,out var use))uses.Add(id,use=new CardUse());
   use.TimesUsed++;use.LastSelectionIndex=SelectionCount++;recent.Add(id);if(recent.Count>12)recent.RemoveAt(0);
  }
 }
 public sealed class CardCandidate {
  public string Id,Reason;
  public bool Eligible;
  public double FinalWeight;
  public override string ToString()=>Id+": "+(Eligible ? "eligible; weight "+FinalWeight.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture) : "rejected; "+Reason);
 }
 public sealed class DirectorSnapshot {
  public string Query,Selected,Outcome;
  public CardCandidate[] Candidates=Array.Empty<CardCandidate>();
  public string[] Recent=Array.Empty<string>();
  public override string ToString()=> "Query: "+Query+"\nSelected: "+Selected+"\n"+Outcome+"\nRecent: "+string.Join(", ",Recent)+"\n"+string.Join("\n",Candidates.Select(c=>c.ToString()));
 }
 public interface IStoryDirectorObserver {void ObserveDirector(DirectorSnapshot snapshot,CardExecutionContext context);}
 public interface ICardWeightContributor {double Multiplier(CardDefinition card,CardHistory history);}
 public sealed class StoryDirector {
  readonly CompiledStoryProgram program;
  readonly Func<int,StoryData> evaluate;
  readonly Func<string,StoryData> read;
  readonly StorySessionRandom random;
  readonly Action<string> trace;
  public CardHistory History {get;}=new CardHistory();
  public DirectorSnapshot Snapshot {get;private set;}=new DirectorSnapshot();
  public readonly List<ICardWeightContributor> WeightContributors=new List<ICardWeightContributor>();
  int selectionsWithoutProgress;
  public StoryDirector(CompiledStoryProgram program,Func<int,StoryData> evaluate,Func<string,StoryData> read,StorySessionRandom random,Action<string> trace) {this.program=program;this.evaluate=evaluate;this.read=read;this.random=random;this.trace=trace;CardCatalog.Validate(program);}
  public CardDefinition Select(CardSelectionRequest request) {
   if(double.IsNaN(request.Intensity) || double.IsInfinity(request.Intensity) || request.Intensity<=0)throw new InvalidOperationException("Card intensity must be finite and positive.");
   Snapshot=new DirectorSnapshot {Query=request.ToString(),Recent=History.Recent.ToArray()};
   trace("Director query: "+request+"; phase "+request.Phase+"; completion "+request.Completion+"/"+request.CompletionTarget+"; intensity "+request.Intensity+".");
   if(request.EquipmentRequested && request.EquipmentIds.Length==0) {Snapshot.Outcome="Optional equipment beat skipped: no prepared equipment.";trace(Snapshot.Outcome);return null;}
   if(History.SelectionCount>=4096)throw new InvalidOperationException("Director stopped after 4096 selections; check progress and loop conditions.");
   var candidates=new List<CardCandidate>();
   foreach(var card in program.cards) {
    var candidate=new CardCandidate {Id=card.Id};candidates.Add(candidate);
    if(request.ExplicitCardId!=null && card.Id!=request.ExplicitCardId)candidate.Reason="A different explicit card was requested.";
    else if(!request.RequiredTags.All(card.Tags.Contains))candidate.Reason="Missing required classification tags ["+string.Join(", ",request.RequiredTags.Where(t=>!card.Tags.Contains(t)))+"].";
    else if(request.EquipmentRequested && !card.Tags.Intersect(request.EquipmentTags,StringComparer.Ordinal).Any())candidate.Reason="Does not match selected equipment identity/content tags.";
    else {
     foreach(var requirement in card.Requirements) {
      bool legal;
      try {legal=evaluate(requirement.Expression).Boolean;}catch(Exception error) {throw new StorySourceException(requirement.Source,"Cannot evaluate requirement "+requirement.Description+": "+error.Message,error);}
      if(!legal) {
       var keys=program.expressions[requirement.Expression].atoms.Where(a=>a.code==StoryAtomCode.Read).Select(a=>a.text).Distinct();
       candidate.Reason="Required "+requirement.Description+"; current "+string.Join(", ",keys.Select(k=>k+" = "+read(k)))+".";break;
      }
     }
     var use=History.For(card.Id);int between=use.LastSelectionIndex<0 ? int.MaxValue : History.SelectionCount-use.LastSelectionIndex-1;
     if(candidate.Reason==null && card.MaxUsesPerSession>=0 && use.TimesUsed>=card.MaxUsesPerSession)candidate.Reason="Maximum "+card.MaxUsesPerSession+" uses reached.";
     if(candidate.Reason==null && between<card.MinCardsBetweenUses)candidate.Reason="Cooldown: "+between+" cards since use; requires "+card.MinCardsBetweenUses+".";
     if(candidate.Reason==null) {
      double recency=use.LastSelectionIndex<0 ? 1 : Math.Min(1,.25+.15*between);
      double weight=card.BaseWeight*recency;
      if(request.EquipmentRequested && request.EquipmentIds.Any(id=>card.Tags.Contains("equipment:"+id)))weight*=2;
      foreach(var contributor in WeightContributors)weight*=contributor.Multiplier(card,History);
      if(double.IsNaN(weight) || double.IsInfinity(weight) || weight<0)throw new InvalidOperationException("Invalid selection weight for "+card.Id);
      candidate.Eligible=weight>0;candidate.FinalWeight=weight;if(weight==0)candidate.Reason="Final selection weight is zero.";
     }
    }
    trace("Director considered "+candidate+".");
   }
   Snapshot.Candidates=candidates.ToArray();
   var eligible=candidates.Where(c=>c.Eligible).ToArray();
   if(eligible.Length==0 || request.RequireProgress && !eligible.Any(c=>program.cards.Single(d=>d.Id==c.Id).Progress>0))
    throw new InvalidOperationException(request.Phase+" cannot advance: "+eligible.Length+" eligible cards"+(request.RequireProgress ? " with no eligible positive-progress path" : "")+" for "+request+"; completion "+request.Completion+"/"+request.CompletionTarget+".");
   double sum=eligible.Sum(c=>c.FinalWeight);if(double.IsInfinity(sum) || sum<=0)throw new InvalidOperationException("Director selection weight total must be finite and positive.");double draw=random.Selection.NextDouble()*sum;var selected=eligible.Last();
   foreach(var candidate in eligible) {draw-=candidate.FinalWeight;if(draw<0) {selected=candidate;break;}}
   Snapshot.Selected=selected.Id;Snapshot.Outcome="Running selected card.";trace("Director selected "+selected.Id+".");
   return program.cards.Single(c=>c.Id==selected.Id);
  }
  public double Complete(CardDefinition card,double pacing) {
   if(double.IsNaN(pacing) || double.IsInfinity(pacing) || pacing<=0)throw new InvalidOperationException("Pacing multiplier must be finite and positive.");
   double progress=card.Progress*pacing;if(double.IsNaN(progress) || double.IsInfinity(progress))throw new InvalidOperationException("Card progress overflow.");
   History.Record(card.Id);selectionsWithoutProgress=progress>0 ? 0 : selectionsWithoutProgress+1;
   if(selectionsWithoutProgress>64)throw new InvalidOperationException("Director selected 65 consecutive zero-progress cards; check the content pool.");
   Snapshot.Recent=History.Recent.ToArray();Snapshot.Outcome="Succeeded; progress +"+progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+".";
   trace("Card "+card.Id+" succeeded: "+card.Progress+" base progress * "+pacing+" pacing = "+progress+".");return progress;
  }
 }
}
