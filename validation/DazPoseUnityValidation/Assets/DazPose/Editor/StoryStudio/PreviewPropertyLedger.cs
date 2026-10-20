using System;
using System.Collections.Generic;
namespace DazPose.Editor.Studio
{
    /// <summary>Plain managed property ownership. Capture before sampling; restore only writes made by this lease.</summary>
    public sealed class PreviewPropertyLedger
    {
        interface IEntry { string Name { get; } string Group {get;} bool Conflict { get; } void Applied(); void Prepare(); void Restore(); }
        sealed class Entry<T> : IEntry
        {
            readonly Func<T> get; readonly Action<T> set; readonly Func<T,T,bool> equal;
            T baseline, applied, restore; bool owned;
            public string Name { get; }
            public string Group { get; }
            public bool Conflict { get { try { return owned && !equal(get(),applied); } catch { return true; } } }
            public Entry(string name, Func<T> read, Action<T> write, Func<T,T,bool> compare,string group)
            { Name=name;Group=group;get=read;set=write;equal=compare;baseline=applied=restore=get(); }
            public void Applied() { try { applied=get();owned=!equal(applied,baseline); } catch { owned=true; } }
            public void Prepare()
            {
                // Capture external values BEFORE AnimationMode stops (which may restore its own baseline).
                try { restore=Conflict || !owned ? get() : baseline; }
                catch { restore=baseline; } // Deleted objects cannot prevent restoration of surviving objects.
                baseline=restore;
            }
            public void Restore()
            {
                bool write;
                try { write=!equal(get(),restore); } catch { write=true; }
                if(write)set(restore);
                applied=baseline=restore;owned=false;
            }
        }
        readonly List<IEntry> entries=new List<IEntry>();
        public void Capture<T>(string name,Func<T> read,Action<T> write,Func<T,T,bool> compare=null,string group=null)
            => entries.Add(new Entry<T>(name,read,write,compare ?? EqualityComparer<T>.Default.Equals,group));
        public void RecordApplied(string group=null) { foreach(var entry in entries)if(group==null||entry.Group==group)entry.Applied(); }
        public string[] Conflicts(string group=null)
        { var result=new List<string>();foreach(var e in entries) if((group==null||e.Group==group)&&e.Conflict)result.Add(e.Name);return result.ToArray(); }
        public void PrepareRestore() { foreach(var e in entries)e.Prepare(); }
        public void Restore()
        {
            // Try every property even if an object was deleted or a setter failed.
            List<Exception> errors=null;
            foreach(var e in entries) try { e.Restore(); } catch(Exception error) { (errors ??= new List<Exception>()).Add(new InvalidOperationException(e.Name,error)); }
            if(errors!=null)throw new AggregateException("Native preview restoration errors",errors);
        }
    }
}
