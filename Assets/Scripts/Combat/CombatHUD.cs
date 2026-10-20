using UnityEngine;

/// <summary>Throwaway IMGUI HUD for prototyping: player bars plus floating enemy health bars.</summary>
public class CombatHUD : MonoBehaviour
{
    public CombatController player;

    GUIStyle label;

    void OnGUI()
    {
        label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        var cam = Camera.main;

        foreach (var h in Health.All)
        {
            if (h == null || (player != null && h.gameObject == player.gameObject) || h.IsDead || cam == null) continue;
            Vector3 p = cam.WorldToScreenPoint(h.transform.position + Vector3.up * 2.3f);
            if (p.z <= 0f) continue;
            DrawBar(new Rect(p.x - 30f, Screen.height - p.y, 60f, 6f), h.Current / h.max, Color.red);
        }

        if (player == null || player.kit == null) return;

        GUI.Label(new Rect(20, 14, 400, 24), $"{player.kit.displayName}   [{player.State}]", label);
        DrawBar(new Rect(20, 44, 260, 18), player.Health.Current / player.Health.max, new Color(0.85f, 0.15f, 0.15f));
        DrawBar(new Rect(20, 68, 260, 10), player.Stamina / player.kit.maxStamina, new Color(0.9f, 0.8f, 0.2f));

        string ab = player.AbilityCooldownLeft > 0f
            ? $"{player.kit.abilityName}: {player.AbilityCooldownLeft:0.0}s"
            : $"{player.kit.abilityName}: READY (Q)";
        GUI.Label(new Rect(20, 82, 300, 24), ab, label);

        GUI.Label(new Rect(20, Screen.height - 50, 900, 24),
            "WASD move | LMB light | E heavy | RMB block | Space dodge | Q ability | 1-3 swap kit | Esc free cursor", label);

        if (player.State == CombatState.Dead)
            GUI.Label(new Rect(Screen.width / 2f - 40, Screen.height / 2f, 200, 30), "YOU DIED", label);
    }

    static void DrawBar(Rect r, float fill, Color color)
    {
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
