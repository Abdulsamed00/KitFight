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
        [Range(0f, 1f)] public float attackMoveFactor = 0.7f;

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
        Vector2 animMove;
        Vector3 impulse;
        float impulseDecay = 22f;

        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveZHash = Animator.StringToHash("MoveZ");

        /// <summary>Lock-on: always face the opponent, moving sideways/back strafes instead of turning.</summary>
        public bool IsLockedOn => lockOn && faceTarget;

        bool lockOn;

        public void ToggleLockOn() => lockOn = !lockOn;

        public void SetLockOn(bool on) => lockOn = on;

        /// <summary>True while the player is actively steering (used to split attacks onto the upper body).</summary>
        public bool HasMoveInput => !locked && !blocking && moveInput.sqrMagnitude > 0.01f;

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

        /// <summary>decay = how fast the impulse fades (m/s per second); higher = shorter, snappier dash.</summary>
        public void AddImpulse(Vector3 v, float decay = 22f)
        {
            impulse += v;
            impulseDecay = decay;
        }

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

        /// <summary>Opponent direction on the ground, or zero if none.</summary>
        public Vector3 DirectionToOpponent() => FlatToTarget().normalized;

        public float DistanceToOpponent() => FlatToTarget().magnitude;

        /// <summary>Camera-relative world direction for a stick/key vector (zero if no input).</summary>
        public Vector3 WorldDirection(Vector2 input)
        {
            if (!cam || input.sqrMagnitude < 0.0001f) return Vector3.zero;
            var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            return (f * input.y + r * input.x).normalized;
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

            // Locked on there are no backward/side sprint clips, so sprinting only counts while moving toward the opponent.
            var g = gait;
            if (IsLockedOn && g == Gait.Sprint && Vector3.Dot(dir, FlatToTarget().normalized) < 0.5f) g = Gait.Run;

            float top = g == Gait.Sprint ? sprintSpeed : g == Gait.Walk ? walkSpeed : runSpeed;
            top *= speedMultiplier * (attacking ? attackMoveFactor : 1f);
            if (blocking || locked) top = 0f;
            float mag = locked ? 0f : moveInput.magnitude;
            Vector3 horizontal = dir * top * mag;

            if (!locked)
            {
                if (attacking || blocking || IsLockedOn) TurnTowards(FlatToTarget());
                else if (dir != Vector3.zero) TurnTowards(dir);
                else TurnTowards(FlatToTarget());
            }

            if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += gravity * Time.deltaTime;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, impulseDecay * Time.deltaTime);
            cc.Move((horizontal + impulse + Vector3.up * verticalVel) * Time.deltaTime);

            // Animator blend: local move direction * gait (0 idle, 1 walk, 2 run, 3 sprint).
            float gaitValue = (mag < 0.1f || blocking || locked) ? 0f : (float)g * Mathf.Clamp01(mag);
            var local = dir == Vector3.zero ? Vector3.zero : transform.InverseTransformDirection(dir);
            var targetMove = new Vector2(local.x, local.z) * gaitValue;
            animMove = Vector2.MoveTowards(animMove, targetMove, 10f * Time.deltaTime);
            if (anim)
            {
                anim.SetFloat(MoveXHash, animMove.x);
                anim.SetFloat(MoveZHash, animMove.y);
            }
        }
    }
}
