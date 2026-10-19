using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Game {
 public sealed class PlayerProfileService {
  readonly string path;
  bool retainBadFile;
  JObject preservedDocument=new JObject();
  public PlayerProfile Current { get; private set; }=new PlayerProfile();
  public string LastLoadDiagnostic { get; private set; }
  public string LastSaveDiagnostic { get; private set; }
  public string StoragePath => path;
  public PlayerProfileService(string storageDirectory=null) {
   path=Path.Combine(storageDirectory ?? Path.Combine(Application.persistentDataPath,"DazPose"),"player-profile.json");
   if(!File.Exists(path)) return;
   try {
    var json=File.ReadAllText(path);
    var document=JObject.Parse(json);
    if(document["version"]?.Type!=JTokenType.Integer || document["version"].Value<int>()!=1) throw new InvalidDataException("Expected profile version 1.");
    var profile=PlayerProfileJson.Read(document); Validate(profile); Current=profile;preservedDocument=document;
   } catch(Exception error) {
    retainBadFile=true;
    LastLoadDiagnostic=path+": "+error.Message+"; defaults loaded in memory; original file retained.";
    Debug.LogWarning("Player profile fallback: "+LastLoadDiagnostic);
   }
  }
  public static PlayerProfile Clone(PlayerProfile profile) => PlayerProfileJson.Clone(profile);
  public void Update(Action<PlayerProfile> edit) { var next=Clone(Current); edit(next); Commit(next); }
  public void Commit(PlayerProfile next) {
   try {
    Validate(next);
    var detached=Clone(next);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temporary=path+".tmp";
    var document=PlayerProfileJson.Merge(preservedDocument,detached);
    File.WriteAllText(temporary,document.ToString(Formatting.Indented));
    // A deliberate later commit keeps the malformed input as a recovery copy.
    if(File.Exists(path)) File.Replace(temporary,path,retainBadFile ? path+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N") : null);
    else File.Move(temporary,path);
    Current=detached;preservedDocument=document; retainBadFile=false; LastSaveDiagnostic=null;
   } catch(Exception error) {
    LastSaveDiagnostic=path+": "+error.Message; Debug.LogError("Player profile save failed: "+LastSaveDiagnostic); throw;
   }
  }
  public void CalculateSubmissionScore(KinkCatalog catalog) => Update(next=>next.submissionScore=CalculateSubmissionScore(next,catalog));
  public static int CalculateSubmissionScore(PlayerProfile profile,KinkCatalog catalog) {
   int total=catalog.entries.Length;
   int enabled=catalog.entries.Count(def=>profile.kinks.Any(k=>k.id==def.id && k.frequency!=KinkFrequency.No));
   return total==0 ? 0 : (int)Math.Round(100.0*enabled/total,MidpointRounding.AwayFromZero);
  }
  static void Unique<T>(T[] values,Func<T,string> key,string label) where T:class {
   if(values==null) throw new InvalidDataException(label+" array is null.");
   var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var value in values) if(value==null || string.IsNullOrWhiteSpace(key(value)) || !ids.Add(key(value)))
    throw new InvalidDataException(label+" requires unique nonblank IDs.");
  }
  public static void Validate(PlayerProfile p) {
   if(p==null || p.version!=1) throw new InvalidDataException("Expected profile version 1.");
   if(p.submissionScore<0 || p.submissionScore>100 || !float.IsFinite(p.favor)) throw new InvalidDataException("Invalid profile score/favor.");
   Unique(p.kinks,k=>k.id,"kinks"); Unique(p.equipment,e=>e.id,"equipment"); Unique(p.facts,f=>f.key,"facts");
   Unique(p.assignments,a=>a.instanceId,"assignments"); Unique(p.sessionHistory,h=>h.sessionId,"history");
   foreach(var k in p.kinks)
    if(!Enum.IsDefined(typeof(KinkFrequency),k.frequency) || !Enum.IsDefined(typeof(KinkSentiment),k.sentiment))
     throw new InvalidDataException("Invalid kink preference: "+k.id);
   foreach(var f in p.facts) { StoryValue.ValidateType(f.valueType); if(!float.IsFinite(f.numberValue)) throw new InvalidDataException("Nonfinite fact: "+f.key); }
   foreach(var a in p.assignments) {
    if(string.IsNullOrWhiteSpace(a.definitionId) || !DateTimeOffset.TryParse(a.issuedAtUtc,out _) || !Enum.IsDefined(typeof(AssignmentStatus),a.status))
     throw new InvalidDataException("Invalid assignment: "+a.instanceId);
    foreach(var argument in a.parameters ?? Array.Empty<StoryArgument>()) {
     if(argument==null || string.IsNullOrWhiteSpace(argument.name)) throw new InvalidDataException("Invalid assignment parameter.");
     StoryValue.ValidateType(argument.valueType);
    }
   }
   if(p.sessionHistory.Length>128) throw new InvalidDataException("Session history is bounded to 128 summaries.");
  }
 }
}