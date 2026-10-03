using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DazPose.Toys
{
    public sealed class ToyCommandReport
    {
        private readonly ReadOnlyCollection<string> _errors;
        public string Operation { get; }
        public int SuccessfulBindings { get; }
        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        public ToyCommandReport(string operation, int successfulBindings, IEnumerable<string> errors)
        {
            Operation = operation ?? string.Empty;
            SuccessfulBindings = successfulBindings;
            _errors = Array.AsReadOnly((errors ?? Array.Empty<string>()).ToArray());
        }
    }
}
