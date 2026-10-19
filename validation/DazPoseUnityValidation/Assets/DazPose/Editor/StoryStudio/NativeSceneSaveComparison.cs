using System;
using System.Text.RegularExpressions;
namespace DazPose.Editor.Studio
{
    public static class NativeSceneSaveComparison
    {
        /// <summary>Permit only Unity's empty-string spelling and line-ending/whitespace normalization.</summary>
        public static string Canonical(string yaml)
        {
            string text=(yaml ?? "").Replace("\r\n","\n").TrimStart('\uFEFF');
            var lines=text.Split('\n');
            for(int i=0;i<lines.Length;i++)
            {
                string line=lines[i];
                // Unity rewrites stopBeforePhase: '' to stopBeforePhase: on a plain save.
                lines[i]=Regex.Replace(line,"^([ \t]*[^#:\r\n]+:)[ \t]*(?:''|\"\"|)[ \t]*$","$1");
            }
            return string.Join("\n",lines).TrimEnd('\n');
        }
        public static bool Equivalent(string before,string after)=>string.Equals(Canonical(before),Canonical(after),StringComparison.Ordinal);
    }
}
