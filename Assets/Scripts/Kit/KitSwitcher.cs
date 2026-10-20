using UnityEngine;

namespace KitFight
{
    /// <summary>Swaps the active kit: weapon in hand, animator controller, speed values.</summary>
    public class KitSwitcher : MonoBehaviour
    {
        public KitData[] kits;
        public string weaponSocketName = "B-handProp.R";
        public int startIndex = 0;

        public KitData Current { get; private set; }
        public int CurrentIndex { get; private set; } = -1;

        Animator anim;
        PlayerMotor motor;
        PlayerCombat combat;
        Transform socket;
        GameObject weaponInstance;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            socket = FindDeep(transform, weaponSocketName);
            if (!socket) Debug.LogWarning($"Weapon socket '{weaponSocketName}' not found.");
        }

        void Start() => Select(startIndex);

        public void Select(int index)
        {
            if (kits == null || index < 0 || index >= kits.Length || kits[index] == null) return;
            if (index == CurrentIndex) return;

            CurrentIndex = index;
            Current = kits[index];

            if (combat) combat.SetKit(Current);
            if (motor) motor.speedMultiplier = Current.moveSpeedMultiplier;
            if (anim) anim.runtimeAnimatorController = Current.controller;

            if (weaponInstance) Destroy(weaponInstance);
            if (socket && Current.weaponPrefab)
            {
                weaponInstance = Instantiate(Current.weaponPrefab, socket);
                weaponInstance.transform.localPosition = Vector3.zero;
                weaponInstance.transform.localRotation = Quaternion.identity;
            }
        }

        static Transform FindDeep(Transform root, string n)
        {
            if (root.name == n) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, n);
                if (r) return r;
            }
            return null;
        }
    }
}
