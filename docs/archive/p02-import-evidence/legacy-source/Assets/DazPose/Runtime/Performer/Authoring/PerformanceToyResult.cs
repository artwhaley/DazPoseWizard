using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DazPose.Performer
{
    /// <summary>Immutable result of one scoped toy delivery command.</summary>
    public sealed class PerformanceToyResult
    {
        private readonly ReadOnlyCollection<string> errors;

        public string Operation { get; }
        public int SuccessfulBindings { get; }
        public IReadOnlyList<string> Errors => errors;
        public bool Succeeded => SuccessfulBindings > 0 && errors.Count == 0;

        internal PerformanceToyResult(string operation, int successfulBindings, IEnumerable<string> failures)
        {
            Operation = operation ?? string.Empty;
            SuccessfulBindings = Math.Max(0, successfulBindings);
            errors = Array.AsReadOnly((failures ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item)).ToArray());
        }
    }
}
