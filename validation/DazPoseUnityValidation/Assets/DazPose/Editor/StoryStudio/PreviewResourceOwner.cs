using System;
namespace DazPose.Editor.Studio
{
    /// <summary>Keep an owned sampling resource across reprojections and save suspension; release only at lease end.</summary>
    public sealed class PreviewResourceOwner<T> : IDisposable where T:class,IDisposable
    {
        T resource;
        bool disposed;
        public T GetOrCreate(Func<T> create)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PreviewResourceOwner<T>));
            return resource ?? (resource=create());
        }
        public void WithExisting(Action<T> action) { if(resource!=null)action(resource); }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            try { resource?.Dispose(); } finally { resource=null; }
        }
    }
}
