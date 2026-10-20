using System;
using System.Collections.Generic;

namespace DazPose.Performer
{
    /// <summary>Executes production wardrobe mutations atomically and rolls every attempted stage back in reverse order.</summary>
    public static class WardrobeAtomicCommit
    {
        public readonly struct Step
        {
            public string Name { get; }
            public Action Apply { get; }
            public Action Rollback { get; }
            public Step(string name, Action apply, Action rollback)
            { Name = name ?? string.Empty; Apply = apply ?? throw new ArgumentNullException(nameof(apply)); Rollback = rollback ?? throw new ArgumentNullException(nameof(rollback)); }
        }

        public static void Execute(IReadOnlyList<Step> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var attempted = new List<Step>(steps.Count);
            try
            {
                for (int i = 0; i < steps.Count; ++i)
                {
                    attempted.Add(steps[i]); // Include a partially applied failing stage in rollback.
                    steps[i].Apply();
                }
            }
            catch (Exception applyError)
            {
                var errors = new List<Exception> { applyError };
                for (int i = attempted.Count - 1; i >= 0; --i)
                {
                    try { attempted[i].Rollback(); }
                    catch (Exception rollbackError) { errors.Add(new InvalidOperationException("Wardrobe rollback failed at stage '" + attempted[i].Name + "'.", rollbackError)); }
                }
                if (errors.Count == 1) throw;
                throw new AggregateException("Wardrobe commit failed and one or more rollback stages also failed.", errors);
            }
        }
    }
}
