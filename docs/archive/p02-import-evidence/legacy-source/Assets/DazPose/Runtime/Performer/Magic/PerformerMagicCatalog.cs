using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Magic/Catalog", fileName = "Performer Magic Catalog")]
    public sealed class PerformerMagicCatalog : ScriptableObject
    {
        [SerializeField] private List<PerformerSpell> spells = new List<PerformerSpell>();
        [SerializeField] private List<PerformerAura> auras = new List<PerformerAura>();

        public IReadOnlyList<PerformerSpell> Spells => spells;
        public IReadOnlyList<PerformerAura> Auras => auras;

        public bool IsReady(out string reason)
        {
            if (spells == null || spells.Count == 0)
            {
                reason = "Magic Catalog '" + name + "' must list at least one Spell.";
                return false;
            }
            if (auras == null || auras.Count == 0)
            {
                reason = "Magic Catalog '" + name + "' must list at least one Aura.";
                return false;
            }
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i] == null)
                {
                    reason = "Magic Catalog contains a missing Spell at index " + i + ".";
                    return false;
                }
                if (!spells[i].IsReady(out reason))
                {
                    return false;
                }
            }
            for (int i = 0; i < auras.Count; i++)
            {
                if (auras[i] == null)
                {
                    reason = "Magic Catalog contains a missing Aura at index " + i + ".";
                    return false;
                }
                if (!auras[i].IsReady(out reason))
                {
                    return false;
                }
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(IList<PerformerSpell> spellAssets, IList<PerformerAura> auraAssets)
        {
            if (spells == null) spells = new List<PerformerSpell>();
            if (auras == null) auras = new List<PerformerAura>();
            AppendMissing(spells, spellAssets);
            AppendMissing(auras, auraAssets);
        }

        private static void AppendMissing<T>(List<T> destination, IList<T> additions) where T : Object
        {
            if (additions == null) return;
            foreach (T item in additions)
            {
                if (item != null && !destination.Contains(item)) destination.Add(item);
            }
        }
#endif
    }
}
