using System;
using DazPose.Story;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public sealed class ModalShellView : MonoBehaviour, IStoryModalView
    {
        public TMP_Text titleText, errorText;
        public RectTransform body;
        public Button applyButton, cancelButton;
        bool disposed, allowCancel, focusPending;
        GameObject previousFocus;
        public event Action Accepted;
        public event Action Cancelled;
        public event Action Disposed;
        public GameObject Root => gameObject;
        public Transform Body => body;
        public void Initialize(string title,string confirm,bool cancel)
        {
            disposed=false;allowCancel=cancel;focusPending=true;
            previousFocus=EventSystem.current!=null ? EventSystem.current.currentSelectedGameObject : null;
            titleText.text=title; errorText.text="";
            applyButton.GetComponentInChildren<TMP_Text>(true).text=confirm;
            cancelButton.gameObject.SetActive(cancel);
            applyButton.onClick.RemoveAllListeners(); cancelButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(()=>Accepted?.Invoke());
            cancelButton.onClick.AddListener(()=>Cancelled?.Invoke());
        }
        void Update()
        {
            if(focusPending && EventSystem.current!=null)
            {
                var search=body.GetComponentInChildren<TMPro.TMP_InputField>(true);
                var fallback=allowCancel ? cancelButton : applyButton;
                var target=search!=null ? search.gameObject : fallback.gameObject;
                EventSystem.current.SetSelectedGameObject(target);focusPending=false;
            }
            if(!disposed && allowCancel && Input.GetKeyDown(KeyCode.Escape)) Cancelled?.Invoke();
        }
        public void SetError(string message) => errorText.text=message??"";
        public void SetBusy(bool value) { applyButton.interactable=!value;cancelButton.interactable=!value; }
        void OnDisable() { if(!disposed) Cancelled?.Invoke(); }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            applyButton.onClick.RemoveAllListeners();cancelButton.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
            if(previousFocus!=null && previousFocus.activeInHierarchy && EventSystem.current!=null)
                EventSystem.current.SetSelectedGameObject(previousFocus);
            Disposed?.Invoke(); Accepted=null;Cancelled=null;Disposed=null;
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
    }
}
