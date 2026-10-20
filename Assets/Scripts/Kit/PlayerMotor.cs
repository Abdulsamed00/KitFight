using UnityEngine;

namespace KitFight
{
    /// <summary>Camera-relative movement. Takes a move vector + gait, contains no input code.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public enum Gait { Walk = 1, Run = 2, Sprint = 3 }

        public float walkSpeed = 2f;
        public float runSpeed = 4.5f;
        public float sprintSpeed = 7f;
        public float turnSpeed = 14f;
        public float gravity = -20f;
        [Range(0f, 1f)] public float attackMoveFactor = 0.25f;

        [HideInInspector] public float speedMultiplier = 1f;
        [HideInInspector] public bool attacking;
        [HideInInspector] public bool blocking;
        [HideInInspector] public bool locked;          // stunned / dead / round not running
        [HideInInspector] public Transform faceTarget; // opponent

        CharacterController cc;
        Animator anim;
        Transform cam;
        Vector2 moveInput;
        Gait gait = Gait.Run;
        float verticalVel;
        float animSpeed;
        Vector3 impulse;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            if (Camera.main) cam = Camera.main.transform;
        }

        public void SetMove(Vector2 move, Gait g)
        {
            moveInput = Vector2.ClampMagnitude(move, 1f);
            gait = g;
        }

        public void AddImpulse(Vector3 v) => impulse += v;

        public void ClearImpulse() => impulse = Vector3.zero;

        public void Teleport(Vector3 pos, Quaternion rot)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
            verticalVel = 0f;
            impulse = Vector3.zero;
        }

        /// <summary>Snap to face the opponent (used when a swing starts).</summary>
        public void FaceOpponent()
        {
            var f = FlatToTarget();
            if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f);
        }

        Vector3 FlatToTarget()
        {
            if (!faceTarget) return Vector3.zero;
            return Vector3.ProjectOnPlane(faceTarget.position - transform.position, Vector3.up);
        }

        void TurnTowards(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        void Update()
        {
            if (!cam && Camera.main) cam = Camera.main.transform;

            Vector3 dir = Vector3.zero;
            if (cam && moveInput.sqrMagnitude > 0.0001f && !locked)
            {
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                dir = (f * moveInput.y + r * moveInput.x).normalized;
            }

            float top = gait == Gait.Sprint ? sprintSpeed : gait == Gait.Walk ? walkSpeed : runSpeed;
            top *= speedMultiplier * (attacking ? attackMoveFactor : 1f);
            if (blocking || locked) top = 0f;
            float mag = locked ? 0f : moveInput.magnitude;
            Vector3 horizontal = dir * top * mag;

            if (!locked)
            {
                if (attacking || blocking) TurnTowards(FlatToTarget());
                else if (dir != Vector3.zero) TurnTowards(dir);
                else TurnTowards(FlatToTarget());
            }

            if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += gravity * Time.deltaTime;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 22f * Time.deltaTime);
            cc.Move((horizontal + impulse + Vector3.up * verticalVel) * Time.deltaTime);

            // Animator blend value: 0 idle, 1 walk, 2 run, 3 sprint
            float targetBlend = (mag < 0.1f || blocking) ? 0f : (float)gait;
            animSpeed = Mathf.MoveTowards(animSpeed, targetBlend, 8f * Time.deltaTime);
            if (anim) anim.SetFloat(SpeedHash, animSpeed);
        }
    }
}
