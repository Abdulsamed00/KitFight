using UnityEngine;

namespace KitFight
{
    /// <summary>One playable kit: a weapon, its animator and its attack combo.</summary>
    [CreateAssetMenu(menuName = "KitFight/Kit", fileName = "Kit_New")]
    public class KitData : ScriptableObject
    {
        public string displayName = "Kit";
        public GameObject weaponPrefab;
        public RuntimeAnimatorController controller;

        [Tooltip("Combo order. Clip i is played by animator state \"Attack{i}\".")]
        public AnimationClip[] comboClips;

        public float attackSpeed = 1f;
        public float moveSpeedMultiplier = 1f;
    }
}
