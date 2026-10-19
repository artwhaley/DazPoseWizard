#if UNITY_EDITOR
using System;
using UnityEngine;
namespace DazPose.Performer
{
    // Editor-only cooperation: no serialized switches, disabled components, or runtime behavior changes.
    public static class NativePreviewWriterGuard
    {
        static Transform owner;
        public static bool Holds(Component writer) => owner != null && writer != null
            && (writer.transform == owner || writer.transform.IsChildOf(owner));
        public static IDisposable Acquire(Transform root)
        {
            if (owner != null) throw new InvalidOperationException("Another native preview owns Lara's editor writers.");
            owner = root;
            return new Release(root);
        }
        sealed class Release : IDisposable
        {
            readonly Transform root;
            public Release(Transform value) { root = value; }
            public void Dispose() { if (owner == root) owner = null; }
        }
    }
}
#endif
