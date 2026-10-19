using System;
using UnityEngine;
namespace DazPose.Story
{
    public interface IStoryModalView : IDisposable
    {
        GameObject Root { get; }
        Transform Body { get; }
        event Action Accepted;
        event Action Cancelled;
        void SetError(string error);
        void SetBusy(bool busy);
    }
    public interface IStoryModalViewFactory
    {
        IStoryModalView Create(Transform parent, string title, string confirm, bool allowCancel);
        void Message(Transform body,string message);
        void Interests(Transform body,DazPose.Game.IProfileEditDraft draft,KinkCatalog catalog);
        void Equipment(Transform body,DazPose.Game.IProfileEditDraft draft,EquipmentCatalog catalog);
        void Clothing(Transform body,DazPose.Game.ClothingEditSession draft);
    }
    // Presentation installs a provider before scene Awake; runtime owns awaited prompt/session cancellation.
    public static class StoryModalPresentation
    {
        public static IStoryModalViewFactory Factory { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Factory = null;
    }
}
