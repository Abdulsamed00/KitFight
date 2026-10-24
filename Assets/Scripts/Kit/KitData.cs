using UnityEngine;

namespace KitFight
{
    /// <summary>One playable kit: weapons, animator, attack combo and combat numbers.</summary>
    [CreateAssetMenu(menuName = "KitFight/Kit", fileName = "Kit_New")]
    public class KitData : ScriptableObject
    {
        public string displayName = "Kit";
        public GameObject weaponPrefab;
        public RuntimeAnimatorController controller;

        [Tooltip("Combo order. Clip i is played by animator state \"Attack{i}\".")]
        public AnimationClip[] comboClips;

        [Header("Off hand (left hand socket)")]
        public GameObject offhandPrefab;
        public Vector3 offhandEuler;

        [Header("Block / parry: hold to guard, animator states \"Block\" and \"BlockHit\"")]
        public AnimationClip blockClip;
        public AnimationClip blockHitClip;

        [Header("Combat")]
        public float damage = 12f;
        public float range = 2f;
        [Range(30f, 360f)] public float arc = 120f;
        [Tooltip("Normalized attack time at which the hit lands.")]
        [Range(0f, 1f)] public float hitStart = 0.35f;
        public float knockback = 3f;

        [Header("Speed")]
        public float attackSpeed = 1f;
        public float moveSpeedMultiplier = 1f;
        [Tooltip("Attack can be pressed during a dodge to cancel the dash into a swing (keeps the momentum).")]
        public bool dashAttack;
    }
}
