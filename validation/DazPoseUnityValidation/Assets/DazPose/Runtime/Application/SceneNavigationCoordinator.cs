using System;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Game
{
    /// <summary>One navigation transition at a time; stop local work before invoking the supplied loader.</summary>
    public sealed class SceneNavigationCoordinator
    {
        readonly Action stop;
        readonly Func<string, Task> load;
        readonly Func<string, bool> available;
        int active;
        public bool IsNavigating => Volatile.Read(ref active) != 0;
        public string LastError { get; private set; }
        public SceneNavigationCoordinator(Action stopSession, Func<string, Task> loadScene, Func<string, bool> canLoad)
        {
            stop = stopSession ?? throw new ArgumentNullException(nameof(stopSession));
            load = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
            available = canLoad ?? throw new ArgumentNullException(nameof(canLoad));
        }
        public bool Reject(string message)
        {
            if (!IsNavigating) LastError = message;
            return false;
        }
        public async Task<bool> NavigateAsync(string path)
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0) return false;
            try
            {
                LastError = null;
                if (string.IsNullOrWhiteSpace(path) || !available(path))
                    return RejectUnavailable(path);
                stop();
                await load(path);
                return true;
            }
            catch (OperationCanceledException)
            {
                LastError = "Scene transition was cancelled.";
                return false;
            }
            catch (Exception error)
            {
                LastError = "Could not open " + path + ": " + error.Message;
                return false;
            }
            finally { Volatile.Write(ref active, 0); }
        }
        bool RejectUnavailable(string path)
        {
            LastError = string.IsNullOrWhiteSpace(path) ? "No saved scene was selected." :
                "The saved scene is unavailable in this installation: " + path;
            return false;
        }
    }
}
