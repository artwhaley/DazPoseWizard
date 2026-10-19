using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Game
{
    // Stable catalog order and indices keep recycled rows independent from draft values.
    public static class CatalogSearch
    {
        public static int[] Find<T>(IReadOnlyList<T> entries,string query,Func<T,string> text,
            Func<T,string> name,Func<T,string> id,Func<T,bool> filter=null)
        {
            string[] words=(query??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            return Enumerable.Range(0,entries.Count)
                .Where(i=>(filter==null||filter(entries[i])) &&
                    words.All(w=>(text(entries[i])??"").IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0))
                .OrderBy(i=>name(entries[i]),StringComparer.OrdinalIgnoreCase)
                .ThenBy(i=>id(entries[i]),StringComparer.Ordinal).ToArray();
        }
    }
}
