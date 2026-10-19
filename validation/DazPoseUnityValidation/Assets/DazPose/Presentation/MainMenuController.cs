using System;
using DazPose.Game;
using UnityEngine;

namespace DazPose.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] SceneBrowserView view;
        bool initialized;

        void Awake()
        {
            if (view == null) view = GetComponent<SceneBrowserView>();
        }

        void Start() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            if (view == null)
                throw new InvalidOperationException("MainMenuScreen requires a SceneBrowserView on the same object.");
            var library = ProductionUiLibrary.Load();
            library.Prepare(view.gameObject);
            var application = GameApplication.EnsureStarted();
            initialized = true;
            view.Initialize(application);
        }
    }
}