using UnityEngine;

namespace KitFight
{
    /// <summary>Gives each player's body a team colour so the two fighters can be told apart.</summary>
    public class PlayerTint : MonoBehaviour
    {
        public Color color = Color.white;

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = r.material; // instance
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            }
        }
    }
}
