using UnityEngine;
namespace DazPose.Presentation
{
    [CreateAssetMenu(menuName="DazPose/UI/Production UI Library")]
    public sealed class ProductionUiLibrary : ScriptableObject
    {
        public UiTheme theme;
        public ModalShellView modalShell;
        public RectTransform profilePanel;
        public GameObject interestRow, equipmentRow, clothingRow, sceneCard, choiceButton;
        public SceneBrowserView mainMenu;
        public SettingsMenuController settings;
        public static ProductionUiLibrary Load()
        {
            var value=Resources.Load<ProductionUiLibrary>("DazPose/ProductionUi");
            if(value==null || value.theme==null || value.modalShell==null)
                throw new System.InvalidOperationException("Production UI templates are missing. Check Assets/Production/UI and Resources/DazPose/ProductionUi.");
            return value;
        }
        public void Prepare(GameObject root) => theme.Apply(root);
    }
}
