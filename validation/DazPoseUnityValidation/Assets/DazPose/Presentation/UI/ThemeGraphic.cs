using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public enum UiColorRole { Background, Panel, Control, Accent, Text, Muted, Error }
    public sealed class ThemeGraphic : MonoBehaviour
    {
        public Graphic target;
        public UiColorRole role;
        public void Apply(UiTheme theme)
        {
            if(target == null) target = GetComponent<Graphic>();
            if(target == null) return;
            switch(role)
            {
                case UiColorRole.Background: target.color=theme.background;break;
                case UiColorRole.Panel: target.color=theme.panel;break;
                case UiColorRole.Control: target.color=theme.control;break;
                case UiColorRole.Accent: target.color=theme.accent;break;
                case UiColorRole.Muted: target.color=theme.muted;break;
                case UiColorRole.Error: target.color=theme.error;break;
                default: target.color=theme.text;break;
            }
        }
    }
}
