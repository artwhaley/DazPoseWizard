using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Motion Set", fileName = "Performer Motion Set")]
    public sealed class PerformerMotionSet : ScriptableObject
    {
        [SerializeField] private PerformerMotionVariant[] variants = System.Array.Empty<PerformerMotionVariant>();

        public PerformerMotionVariant[] Variants => variants == null
            ? System.Array.Empty<PerformerMotionVariant>()
            : (PerformerMotionVariant[])variants.Clone();
        public int VariantCount => Variants.Length;

        public bool IsReady(out string reason)
        {
            if (variants == null || variants.Length == 0)
            {
                reason = "A PerformerMotionSet needs at least one ordered motion variant.";
                return false;
            }
            var seen = new HashSet<PerformerMotionVariant>();
            for (int i = 0; i < variants.Length; i++)
            {
                PerformerMotionVariant variant = variants[i];
                if (variant == null)
                {
                    reason = "Motion set '" + name + "' has an empty variant at index " + i + ".";
                    return false;
                }
                if (!seen.Add(variant))
                {
                    reason = "Motion set '" + name + "' contains the same variant asset more than once.";
                    return false;
                }
                if (!variant.IsReady(out reason)) return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(IList<PerformerMotionVariant> orderedVariants)
        {
            Configure(orderedVariants);
            EditorUtility.SetDirty(this);
        }
#endif

        internal void Configure(IList<PerformerMotionVariant> orderedVariants)
        {
            variants = orderedVariants == null
                ? System.Array.Empty<PerformerMotionVariant>()
                : new List<PerformerMotionVariant>(orderedVariants).ToArray();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
