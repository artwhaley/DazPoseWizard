using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Opacity controls shared by imported uDTU and conventional color-alpha materials.</summary>
    public static class WardrobeMaterialOpacity
    {
        public static bool TryRead(Material material, out float opacity)
        {
            opacity = 1;
            string property = Property(material, out bool scalar);
            if (property == null) return false;
            opacity = scalar ? material.GetFloat(property) : material.GetColor(property).a;
            return true;
        }
        public static bool Apply(Material material, float opacity)
        {
            string property = Property(material, out bool scalar);
            if (property == null) return false;
            if (scalar) material.SetFloat(property, Mathf.Clamp01(opacity));
            else { var color = material.GetColor(property); color.a = Mathf.Clamp01(opacity); material.SetColor(property, color); }
            return true;
        }
        public static bool Apply(MaterialPropertyBlock block, Material material, float opacity)
        {
            string property = Property(material, out bool scalar);
            if (property == null) return false;
            if (scalar) block.SetFloat(property, Mathf.Clamp01(opacity));
            else { var color = material.GetColor(property); color.a = Mathf.Clamp01(opacity); block.SetColor(property, color); }
            return true;
        }
        private static string Property(Material material, out bool scalar)
        {
            scalar = true;
            if (material == null) return null;
            if (material.HasProperty("_Alpha")) return "_Alpha";
            if (material.HasProperty("_AlphaStrength")) return "_AlphaStrength";
            scalar = false;
            if (material.HasProperty("_BaseColor")) return "_BaseColor";
            if (material.HasProperty("_Color")) return "_Color";
            return null;
        }
    }
}
