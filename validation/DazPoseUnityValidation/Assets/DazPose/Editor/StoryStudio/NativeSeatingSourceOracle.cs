using System;
using System.Linq;
namespace DazPose.Editor.Studio
{
    /// <summary>Validates fixed diagnostic locations in an already compiled FirstContact source; not a story interpreter.</summary>
    public static class NativeSeatingSourceOracle
    {
        public static string Describe(string path,string text)
        {
            if(text==null)throw new ArgumentNullException(nameof(text));
            int warm=text.IndexOf("phase Warmup",StringComparison.Ordinal),main=text.IndexOf("phase Main",StringComparison.Ordinal);
            int sit=text.IndexOf("Lara.SitAt(Lounger)",StringComparison.Ordinal),pose=text.IndexOf("Lara.PersistentSitPose(Lounging)",StringComparison.Ordinal);
            int draw=text.IndexOf("tags: [Main, Standing]",StringComparison.Ordinal),dissolve=text.IndexOf("Lara.DissolveTo(Lounger, seated: Lounging)",StringComparison.Ordinal);
            if(warm<0 || sit<warm || pose<sit || main<pose || draw<main || dissolve<draw || !text.Contains("Lara.DissolveTo(MainPosition1)"))
                throw new InvalidOperationException("G3 source oracle changed. Expected Warmup SitAt → PersistentSitPose and Main seated dissolve after the Standing Director loop; update the diagnostic against actual source.");
            int Line(int index)=>text.Substring(0,index).Count(c=>c=='\n')+1;
            return path+": Warmup SitAt line "+Line(sit)+", PersistentSitPose line "+Line(pose)+"; Main Standing draw line "+Line(draw)+", seated dissolve line "+Line(dissolve)+". Fixed endpoints, not replay.";
        }
    }
}
