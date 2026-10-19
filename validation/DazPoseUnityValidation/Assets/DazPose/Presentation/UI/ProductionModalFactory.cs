using System;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Presentation
{
    public sealed class ProductionModalFactory : IStoryModalViewFactory
    {
        static ModalShellView active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { active=null;StoryModalPresentation.Factory=new ProductionModalFactory(); }
        public void Message(Transform body,string message)
        {
            var library=ProductionUiLibrary.Load();
            var row=UnityEngine.Object.Instantiate(library.choiceButton,body,false);
            var rect=(RectTransform)row.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            library.Prepare(row);row.GetComponent<UnityEngine.UI.Button>().enabled=false;
            row.GetComponentInChildren<TMPro.TMP_Text>(true).text=message;row.SetActive(true);
        }
        public void Interests(Transform body,DazPose.Game.IProfileEditDraft draft,KinkCatalog catalog)=>ProfileEditorPresenter.Interests(body,draft,catalog);
        public void Equipment(Transform body,DazPose.Game.IProfileEditDraft draft,EquipmentCatalog catalog)=>ProfileEditorPresenter.Equipment(body,draft,catalog);
        public void Clothing(Transform body,DazPose.Game.ClothingEditSession draft)=>ProfileEditorPresenter.Clothing(body,draft);
        public IStoryModalView Create(Transform parent,string title,string confirm,bool allowCancel)
        {
            if(active!=null)throw new InvalidOperationException("Close the current dialog before opening another.");
            var library=ProductionUiLibrary.Load();
            var view=UnityEngine.Object.Instantiate(library.modalShell,parent,false);
            try
            {
                library.Prepare(view.gameObject);
                view.Initialize(title,confirm,allowCancel);
                active=view;view.Disposed+=()=>{if(active==view)active=null;};
                view.gameObject.SetActive(true);
                return view;
            }
            catch
            {
                if(view!=null)UnityEngine.Object.Destroy(view.gameObject);
                throw;
            }
        }
    }
}
