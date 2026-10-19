using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    [CreateAssetMenu(menuName="DazPose/UI/Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        public Color background = new Color(.025f,.031f,.047f,1);
        public Color panel = new Color(.065f,.073f,.098f,1);
        public Color control = new Color(.12f,.14f,.19f,1);
        public Color accent = new Color(.75f,.36f,.88f,1);
        public Color text = new Color(.95f,.94f,.98f,1);
        public Color muted = new Color(.66f,.67f,.74f,1);
        public Color error = new Color(1f,.48f,.51f,1);
        public Color hover = new Color(.24f,.20f,.31f,1);
        public Color selected = new Color(.38f,.23f,.47f,1);
        public TMP_FontAsset fontAsset;
        public Shader fontShader; // Direct asset reference retains the installed TMP shader in future player builds.
        static TMP_FontAsset generatedFont;
        public TMP_FontAsset Font
        {
            get
            {
                if(fontAsset != null) return fontAsset;
                if(generatedFont == null)
                {
                    generatedFont = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                    if(generatedFont == null) throw new System.InvalidOperationException("The built-in UI font could not be prepared.");
                    generatedFont.hideFlags = HideFlags.HideAndDontSave;
                    if(generatedFont.material != null) generatedFont.material.hideFlags = HideFlags.HideAndDontSave;
                }
                return generatedFont;
            }
        }
        public void Apply(GameObject root)
        {
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = Font;
                label.color = text;
                label.raycastTarget = false;
            }
            foreach(var graphic in root.GetComponentsInChildren<ThemeGraphic>(true)) graphic.Apply(this);
            foreach(var controlView in root.GetComponentsInChildren<Selectable>(true))
            {
                var colors = controlView.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = hover * 1.8f;
                colors.pressedColor = selected * 1.6f;
                colors.selectedColor = selected * 1.6f;
                colors.disabledColor = new Color(.4f,.4f,.45f,.65f);
                colors.colorMultiplier = 1;
                colors.fadeDuration = .08f;
                controlView.colors = colors;
            }
        }
    }
}
