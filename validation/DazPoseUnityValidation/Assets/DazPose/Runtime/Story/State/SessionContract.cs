using System;
using System.Linq;
using DazPose.Game;
namespace DazPose.Story {
 [Serializable] public sealed class SessionContract {
  public string sessionId;
  public int randomSeed;
  public float pacingMultiplier=1f;
  public int submissionScore;public float favor;
  public KinkPreference[] kinks=Array.Empty<KinkPreference>();
  public string[] preparedEquipmentIds=Array.Empty<string>();
  public static SessionContract Create(PlayerProfile profile) {
   var id=Guid.NewGuid();
   var contract=new SessionContract { sessionId=id.ToString("N"), randomSeed=BitConverter.ToInt32(id.ToByteArray(),0) };
   contract.RefreshKinks(profile); return contract;
  }
  public void RefreshKinks(PlayerProfile profile) {
   submissionScore=profile.submissionScore;favor=profile.favor;
   kinks=profile.kinks.Select(k=>new KinkPreference { id=k.id,frequency=k.frequency,sentiment=k.sentiment }).ToArray();
  }
 }
}