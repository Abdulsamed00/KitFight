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

        CharacterController cc;
        Animator anim;
        Transform cam;
        Vector2 moveInput;
        Gait gait = Gait.Run;
        float verticalVel;
        float animSpeed;

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

        public void FaceCamera()
        {
            if (!cam) return;
            var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f);
        }

        void Update()
        {
            if (!cam && Camera.main) cam = Camera.main.transform;

            Vector3 dir = Vector3.zero;
            if (cam && moveInput.sqrMagnitude > 0.0001f)
            {
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                dir = (f * moveInput.y + r * moveInput.x).normalized;
            }

            float top = gait == Gait.Sprint ? sprintSpeed : gait == Gait.Walk ? walkSpeed : runSpeed;
            top *= speedMultiplier * (attacking ? attackMoveFactor : 1f);
            float mag = moveInput.magnitude;
            Vector3 horizontal = dir * top * mag;

            if (dir != Vector3.zero && !attacking)
            {
                var target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += gravity * Time.deltaTime;
            cc.Move((horizontal + Vector3.up * verticalVel) * Time.deltaTime);

            // Animator blend value: 0 idle, 1 walk, 2 run, 3 sprint
            float targetBlend = mag < 0.1f ? 0f : (float)gait;
            animSpeed = Mathf.MoveTowards(animSpeed, targetBlend, 8f * Time.deltaTime);
            if (anim) anim.SetFloat(SpeedHash, animSpeed);
        }
    }
}
