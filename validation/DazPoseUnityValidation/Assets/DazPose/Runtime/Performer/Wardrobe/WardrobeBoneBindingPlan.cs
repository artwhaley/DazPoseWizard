using System;
using System.Collections.Generic;
using System.Linq;

namespace DazPose.Performer
{
    public readonly struct WardrobeLocalBoneFact
    {
        public string Name { get; }
        public string Parent { get; }
        public WardrobeLocalBoneFact(string name, string parent) { Name = name ?? string.Empty; Parent = parent ?? string.Empty; }
    }

    /// <summary>Pure canonical/local bone name planner used by WardrobeAttachmentBinder before instance creation.</summary>
    public static class WardrobeBoneBindingPlan
    {
        public static bool TryCreate(IReadOnlyList<string> canonicalBoneNames, IReadOnlyList<string> referencedBoneNames,
            string rootBoneName, IReadOnlyList<WardrobeLocalBoneFact> localBoneFacts,
            out string[] localCreationOrder, out string error)
        {
            localCreationOrder = Array.Empty<string>(); error = null;
            var canonical = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in canonicalBoneNames ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(name) || !canonical.Add(name))
                { error = "Canonical skeleton bone name is missing or ambiguous: " + (name ?? "<null>"); return false; }
            if (string.IsNullOrWhiteSpace(rootBoneName) || !canonical.Contains(rootBoneName))
            { error = "Canonical root bone is missing: " + (rootBoneName ?? "<null>"); return false; }

            var locals = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var fact in localBoneFacts ?? Array.Empty<WardrobeLocalBoneFact>())
                if (string.IsNullOrWhiteSpace(fact.Name) || !locals.TryAdd(fact.Name, fact.Parent) || canonical.Contains(fact.Name))
                { error = "Local bone identity is missing, duplicated or collides with the actor skeleton: " + (fact.Name ?? "<null>"); return false; }

            var resolved = new HashSet<string>(canonical, StringComparer.Ordinal);
            var pending = new HashSet<string>(locals.Keys, StringComparer.Ordinal);
            var order = new List<string>(pending.Count);
            while (pending.Count > 0)
            {
                var ready = pending.Where(name => resolved.Contains(locals[name])).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                if (ready.Length == 0)
                { error = "Local bone parent is missing or cyclic: " + string.Join(", ", pending.OrderBy(x => x, StringComparer.Ordinal)); return false; }
                foreach (var name in ready) { resolved.Add(name); pending.Remove(name); order.Add(name); }
            }
            foreach (var name in referencedBoneNames ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(name) || !resolved.Contains(name))
                { error = "Attachment bone is missing or ambiguous: " + (name ?? "<null>"); return false; }
            localCreationOrder = order.ToArray(); return true;
        }
    }
}
