using System;
using System.Linq;
using DazPose.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DazPose.Story;
namespace DazPose.Presentation
{
    public sealed class SceneBrowserView : MonoBehaviour
    {
        public RecycledListView scenes;
        public TMP_Text selectedTitle, selectedDescription, errorText;
        public Button startButton, settingsButton, quitButton;
        GameApplication app;
        SceneCatalog.Entry selected;
        SettingsMenuController settings;
        string localError;
        bool settingsWasActive;
        public void Initialize(GameApplication application)
        {
            app=application;
            var entries=app.Catalog?.entries?.Where(e=>e!=null && e.enabled).ToArray()??Array.Empty<SceneCatalog.Entry>();
            selected=entries.FirstOrDefault();
            var library=ProductionUiLibrary.Load();
            scenes.SetItems(entries.Length,library.sceneCard,(i,row)=>{
                var entry=entries[i];bool available=Available(entry);
                string summary=available ? entry.description : "Unavailable · "+UnavailableReason(entry);
                row.GetComponent<SceneCardView>().Bind(entry.title,summary,entry==selected,()=>{selected=entry;ShowSelection();});
            },true);
            startButton.onClick.RemoveAllListeners();startButton.onClick.AddListener(StartSelected);
            settingsButton.onClick.RemoveAllListeners();settingsButton.onClick.AddListener(OpenSettings);
            quitButton.onClick.RemoveAllListeners();quitButton.onClick.AddListener(Application.Quit);
            ShowSelection();
            FocusInitialControl();
        }
        void FocusInitialControl()
        {
            if(EventSystem.current==null)return;
            var firstScene=scenes.GetComponentInChildren<SceneCardView>(true);
            var target=firstScene!=null && firstScene.gameObject.activeInHierarchy ? firstScene.selectButton.gameObject : settingsButton.gameObject;
            EventSystem.current.SetSelectedGameObject(target);
        }
        static bool Available(SceneCatalog.Entry entry) => entry!=null && !string.IsNullOrWhiteSpace(entry.scenePath) && Application.CanStreamedLevelBeLoaded(entry.scenePath);
        static string UnavailableReason(SceneCatalog.Entry entry) => entry==null || string.IsNullOrWhiteSpace(entry.scenePath)
            ? "No saved scene path is configured."
            : "The saved scene is not included in this installation: "+entry.scenePath;
        void ShowSelection()
        {
            selectedTitle.text=selected?.title??"No published scenes";
            selectedDescription.text=selected==null ? "" : Available(selected) ? (selected.description??"") : (selected.description??"")+"\n"+UnavailableReason(selected);
            startButton.interactable=Available(selected) && app!=null && !app.IsNavigating;
            scenes.RefreshBindings();
            RefreshError();
        }
        async void StartSelected()
        {
            if(selected==null || app==null || app.IsNavigating)return;
            if(!Available(selected)){localError=UnavailableReason(selected);RefreshError();return;}
            localError=null;RefreshError();
            try { await app.StartSceneAsync(selected.id); }
            catch(Exception error){localError=error.Message;RefreshError();}
        }
        void OpenSettings()
        {
            localError=null;
            try
            {
                if(settings==null)
                {
                    var library=ProductionUiLibrary.Load();
                    if(library.settings==null)throw new InvalidOperationException("The Settings template is unavailable.");
                    settings=Instantiate(library.settings,transform,false);
                    settings.FitToParentCanvas();
                }
                settings.gameObject.SetActive(true);
                settingsWasActive=true;
                FocusSettings();
            }
            catch(Exception error){localError=error.Message;}
            RefreshError();
        }
        void FocusSettings()
        {
            if(EventSystem.current==null || settings==null)return;
            var control=settings.GetComponentInChildren<Selectable>(false);
            if(control!=null && control.IsInteractable())EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
        bool StoryPromptOpen()
        {
            foreach(var prompt in FindObjectsByType<StoryPromptService>())if(prompt!=null && prompt.IsOpen)return true;
            return false;
        }
        void RefreshError()
        {
            if(errorText==null)return;
            string navigation=app!=null ? app.NavigationError : null;
            errorText.text=!string.IsNullOrWhiteSpace(localError)?localError:(navigation??"");
        }
        void Update()
        {
            if(app==null)return;
            startButton.interactable=Available(selected) && !app.IsNavigating;
            RefreshError();
            bool active=settings!=null && settings.gameObject.activeSelf;
            if(settingsWasActive && !active && EventSystem.current!=null)
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            settingsWasActive=active;
            if(Input.GetKeyDown(KeyCode.Escape) && active && !StoryPromptOpen())settings.gameObject.SetActive(false);
        }
    }
}
