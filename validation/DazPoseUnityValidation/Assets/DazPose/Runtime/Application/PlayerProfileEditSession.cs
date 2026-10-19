using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Story;
namespace DazPose.Game
{
    public interface IProfileEditDraft
    {
        KinkPreference GetKink(string id);
        EquipmentPreference GetEquipment(string id);
        void SetFrequency(string id,KinkFrequency value);
        void SetSentiment(string id,KinkSentiment value);
        void SetEquipmentOwnedAndWilling(string id,bool value);
        void SetReward(string id,bool value);
        void SetPunishment(string id,bool value);
    }
    /// <summary>An isolated transaction. Only a successful Commit replaces the current profile.</summary>
    public sealed class PlayerProfileEditSession : IProfileEditDraft, IDisposable
    {
        readonly PlayerProfileService profiles;
        readonly PlayerProfile baseline, draft;
        readonly KinkCatalog scoreCatalog;
        readonly Dictionary<string,KinkPreference> kinks;
        readonly Dictionary<string,EquipmentPreference> equipment;
        readonly List<KinkPreference> kinkOrder;
        readonly List<EquipmentPreference> equipmentOrder;
        public bool IsCommitted {get;private set;}
        public bool IsDiscarded {get;private set;}
        public PlayerProfile Draft {get {SyncArrays();return PlayerProfileService.Clone(draft);}}
        public PlayerProfileEditSession(PlayerProfileService service,KinkCatalog submissionCatalog=null)
        {
            profiles=service??throw new ArgumentNullException(nameof(service));
            baseline=profiles.Current;draft=PlayerProfileService.Clone(baseline);scoreCatalog=submissionCatalog;
            kinkOrder=draft.kinks.ToList();equipmentOrder=draft.equipment.ToList();
            kinks=kinkOrder.ToDictionary(k=>k.id,StringComparer.Ordinal);
            equipment=equipmentOrder.ToDictionary(e=>e.id,StringComparer.Ordinal);
        }
        void Active()
        {
            if(IsDiscarded)throw new ObjectDisposedException(nameof(PlayerProfileEditSession));
            if(IsCommitted)throw new InvalidOperationException("This profile edit has already been applied.");
        }
        static void Id(string id) {if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("A stable catalog ID is required.",nameof(id));}
        public KinkPreference GetKink(string id)
        {
            Active();Id(id);
            if(!kinks.TryGetValue(id,out var value)) {value=new KinkPreference{id=id};kinks.Add(id,value);kinkOrder.Add(value);}
            return value;
        }
        public EquipmentPreference GetEquipment(string id)
        {
            Active();Id(id);
            if(!equipment.TryGetValue(id,out var value)) {value=new EquipmentPreference{id=id};equipment.Add(id,value);equipmentOrder.Add(value);}
            return value;
        }
        public void SetFrequency(string id,KinkFrequency value)
        {
            if(!Enum.IsDefined(typeof(KinkFrequency),value))throw new ArgumentOutOfRangeException(nameof(value));
            GetKink(id).frequency=value;
        }
        public void SetSentiment(string id,KinkSentiment value)
        {
            if(!Enum.IsDefined(typeof(KinkSentiment),value))throw new ArgumentOutOfRangeException(nameof(value));
            GetKink(id).sentiment=value;
        }
        public void SetEquipmentOwnedAndWilling(string id,bool value)=>GetEquipment(id).ownedAndWilling=value;
        public void SetReward(string id,bool value)=>GetEquipment(id).useAsReward=value;
        public void SetPunishment(string id,bool value)=>GetEquipment(id).useAsPunishment=value;
        void SyncArrays() {draft.kinks=kinkOrder.ToArray();draft.equipment=equipmentOrder.ToArray();}
        public bool Commit()
        {
            if(IsCommitted)return false;
            Active();
            if(!ReferenceEquals(profiles.Current,baseline))
                throw new InvalidOperationException("The profile changed while this editor was open. Cancel and reopen it to keep the latest changes.");
            SyncArrays();
            if(scoreCatalog!=null)draft.submissionScore=PlayerProfileService.CalculateSubmissionScore(draft,scoreCatalog);
            PlayerProfileService.Validate(draft);
            if(!Newtonsoft.Json.Linq.JToken.DeepEquals(PlayerProfileJson.Write(baseline),PlayerProfileJson.Write(draft)))
                profiles.Commit(draft);
            IsCommitted=true;return true;
        }
        public void Discard() {if(!IsCommitted)IsDiscarded=true;}
        public void Dispose()=>Discard();
    }
}
