using UnityEngine;

namespace KitFight
{
    /// <summary>Combo state machine driven by the Attack() command. Contains no input code.</summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Range(0.2f, 1f)] public float comboWindowStart = 0.45f;
        [Range(0.5f, 1f)] public float attackEnd = 0.9f;
        public float blendTime = 0.1f;

        public bool IsAttacking => step >= 0;
        public int ComboStep => step;

        KitData kit;
        Animator anim;
        PlayerMotor motor;
        int step = -1;
        float time;
        bool queued;

        static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
        }

        public void SetKit(KitData k)
        {
            Cancel();
            kit = k;
        }

        public void Attack()
        {
            if (kit == null || kit.comboClips == null || kit.comboClips.Length == 0) return;
            if (step < 0) Begin(0);
            else queued = true;
        }

        public void Cancel()
        {
            step = -1;
            queued = false;
            if (motor) motor.attacking = false;
            if (anim && anim.runtimeAnimatorController) anim.CrossFadeInFixedTime("Locomotion", blendTime);
        }

        void Begin(int i)
        {
            step = i;
            time = 0f;
            queued = false;
            if (motor) { motor.attacking = true; motor.FaceCamera(); }
            if (!anim) return;
            anim.SetFloat(AttackSpeedHash, kit.attackSpeed);
            anim.CrossFadeInFixedTime("Attack" + i, blendTime, 0, 0f);
        }

        void Update()
        {
            if (step < 0) return;
            var clip = kit.comboClips[step];
            float len = clip.length / Mathf.Max(0.01f, kit.attackSpeed);
            time += Time.deltaTime;
            float n = time / len;

            if (queued && n >= comboWindowStart)
            {
                Begin((step + 1) % kit.comboClips.Length);
                return;
            }
            if (n >= attackEnd) Cancel();
        }
    }
}
