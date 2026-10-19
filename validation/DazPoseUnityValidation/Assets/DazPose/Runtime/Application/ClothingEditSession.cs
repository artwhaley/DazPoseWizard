using System;
using System.Collections.Generic;
using DazPose.Story;
namespace DazPose.Game
{
    public sealed class ClothingEditSession : IDisposable
    {
        readonly PlayerWorldState world;
        readonly Dictionary<string,bool> draft=new Dictionary<string,bool>(StringComparer.Ordinal);
        public bool IsCommitted {get;private set;}
        public bool IsDiscarded {get;private set;}
        public IReadOnlyList<string> Slots {get;}
        public ClothingEditSession(PlayerWorldState current)
        {
            world=current??throw new ArgumentNullException(nameof(current));
            var slots=(string[])PlayerWorldState.ClothingSlots.Clone();Slots=Array.AsReadOnly(slots);
            foreach(string slot in slots)draft.Add(slot,world.GetClothing(slot));
        }
        void Active() {if(IsDiscarded)throw new ObjectDisposedException(nameof(ClothingEditSession));if(IsCommitted)throw new InvalidOperationException("Clothing choices were already applied.");}
        public bool GetClothing(string slot) {Active();return draft.TryGetValue(slot,out bool value)?value:throw new ArgumentException("Unknown clothing slot: "+slot);}
        public void SetClothing(string slot,bool value) {Active();if(!draft.ContainsKey(slot))throw new ArgumentException("Unknown clothing slot: "+slot);draft[slot]=value;}
        public bool Commit()
        {
            if(IsCommitted)return false;Active();
            foreach(var row in draft)world.SetClothing(row.Key,row.Value);
            IsCommitted=true;return true;
        }
        public void Discard() {if(!IsCommitted)IsDiscarded=true;}
        public void Dispose()=>Discard();
    }
}
