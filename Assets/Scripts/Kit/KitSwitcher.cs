using UnityEngine;

namespace KitFight
{
    /// <summary>Swaps the active kit: weapons in hands, animator controller, speed values.</summary>
    public class KitSwitcher : MonoBehaviour
    {
        public KitData[] kits;
        public string weaponSocketName = "B-handProp.R";
        public string offhandSocketName = "B-handProp.L";
        public int startIndex = 0;

        public KitData Current { get; private set; }
        public int CurrentIndex { get; private set; } = -1;

        Animator anim;
        PlayerMotor motor;
        PlayerCombat combat;
        Transform socket, offSocket;
        GameObject weaponInstance, offhandInstance;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            socket = FindDeep(transform, weaponSocketName);
            offSocket = FindDeep(transform, offhandSocketName);
            if (!socket) Debug.LogWarning($"Weapon socket '{weaponSocketName}' not found.");
            if (!offSocket) Debug.LogWarning($"Off-hand socket '{offhandSocketName}' not found.");
        }

        void Start() => Select(startIndex);

        public void Cycle(int delta)
        {
            if (kits == null || kits.Length == 0) return;
            int start = CurrentIndex < 0 ? 0 : CurrentIndex;
            Select((start + delta + kits.Length) % kits.Length);
        }

        public void Select(int index)
        {
            if (kits == null || index < 0 || index >= kits.Length || kits[index] == null) return;
            if (index == CurrentIndex) return;
            if (combat && combat.IsAttacking) return; // never cut an attack animation short

            CurrentIndex = index;
            Current = kits[index];

            if (combat) combat.SetKit(Current);
            if (motor) motor.speedMultiplier = Current.moveSpeedMultiplier;
            if (anim) anim.runtimeAnimatorController = Current.controller;

            if (weaponInstance) Destroy(weaponInstance);
            if (offhandInstance) Destroy(offhandInstance);
            weaponInstance = Spawn(Current.weaponPrefab, socket, Vector3.zero);
            offhandInstance = Spawn(Current.offhandPrefab, offSocket, Current.offhandEuler);
        }

        static GameObject Spawn(GameObject prefab, Transform parent, Vector3 euler)
        {
            if (!prefab || !parent) return null;
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
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
