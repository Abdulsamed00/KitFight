using UnityEngine;
using UnityEngine.InputSystem;

namespace KitFight
{
    /// <summary>Over-the-shoulder orbit camera.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.5f;
        public Vector3 pivotOffset = new Vector3(0.5f, 1.6f, 0f);
        public float sensitivity = 0.12f;
        public float minPitch = -25f;
        public float maxPitch = 60f;
        public LayerMask collisionMask = ~0;

        float yaw, pitch = 15f;

        void Start()
        {
            if (target) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (!target) return;

            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * sensitivity;
                pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
            }

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var pivot = target.position + Quaternion.Euler(0f, yaw, 0f) * pivotOffset;
            float dist = distance;
            if (Physics.SphereCast(pivot, 0.2f, rot * Vector3.back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                dist = Mathf.Max(0.5f, hit.distance);

            transform.SetPositionAndRotation(pivot + rot * Vector3.back * dist, rot);
        }
    }
}
