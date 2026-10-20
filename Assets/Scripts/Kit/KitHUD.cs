using UnityEngine;

namespace KitFight
{
    /// <summary>Throwaway IMGUI hint: current kit + key list.</summary>
    public class KitHUD : MonoBehaviour
    {
        KitSwitcher switcher;

        void Awake() => switcher = GetComponent<KitSwitcher>();

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            string kitName = switcher && switcher.Current ? switcher.Current.displayName : "-";
            GUI.Label(new Rect(12, 8, 600, 30), "Kit: " + kitName, new GUIStyle(style) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(12, 40, 800, 30),
                "WASD hareket | Shift sprint | Ctrl yurume | LMB saldiri (combo) | 1-5 kit | Esc imleci birak", style);
        }
    }
}
