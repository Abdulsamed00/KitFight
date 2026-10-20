using UnityEngine;

namespace KitFight
{
    /// <summary>
    /// Single shared camera. It always looks perpendicular to the line between the fighters, so P1 stays on the
    /// left and P2 on the right of the screen; left/right on the stick means toward/away from the opponent.
    /// </summary>
    public class FightCamera : MonoBehaviour
    {
        public static FightCamera Instance { get; private set; }

        public Transform p1, p2; // set by GameManager

        public float pitch = 30f;
        public float baseDistance = 7f;
        public float distancePerMeter = 0.8f;
        public float minDistance = 8f;
        public float maxDistance = 22f;
        public float smoothTime = 0.35f;
        public float yawSmoothTime = 0.6f;
        public float minYawSeparation = 1.5f; // closer than this the camera stops turning
        public float yawDeadZone = 8f;        // ignore axis changes smaller than this (degrees)
        public float minYawSpeed = 20f;       // deg/s when fighters are close
        public float maxYawSpeedFar = 90f;    // deg/s when far apart
        public float lookHeight = 1.1f;

        Vector3 basePos, velocity;
        float yaw, targetYaw, yawVelocity;
        float shakeAmp, shakeTime, shakeDuration;
        bool initialized;

        void Awake() => Instance = this;

        public static void Shake(float amplitude, float duration)
        {
            if (!Instance) return;
            if (amplitude >= Instance.shakeAmp * (Instance.shakeTime / Mathf.Max(0.01f, Instance.shakeDuration)))
            {
                Instance.shakeAmp = amplitude;
                Instance.shakeTime = Instance.shakeDuration = duration;
            }
        }

        void LateUpdate()
        {
            if (!p1 || !p2) return;

            // Yaw: camera right = direction P1 -> P2. Up close (lunges, dodges, knockback) the axis swings wildly,
            // so the camera only turns slowly there and ignores small changes.
            var axis = Vector3.ProjectOnPlane(p2.position - p1.position, Vector3.up);
            float sepFlat = axis.magnitude;
            if (sepFlat > minYawSeparation || !initialized)
            {
                var fwd = Vector3.Cross(axis.normalized, Vector3.up);
                float axisYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                if (!initialized || Mathf.Abs(Mathf.DeltaAngle(targetYaw, axisYaw)) > yawDeadZone) targetYaw = axisYaw;
            }
            if (!initialized) yaw = targetYaw;
            float closeness = Mathf.InverseLerp(minYawSeparation, minYawSeparation + 4f, sepFlat);
            float maxYawSpeed = Mathf.Lerp(minYawSpeed, maxYawSpeedFar, closeness);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmoothTime, maxYawSpeed);

            var mid = (p1.position + p2.position) * 0.5f;
            float sep = Vector3.Distance(p1.position, p2.position);
            float dist = Mathf.Clamp(baseDistance + sep * distancePerMeter, minDistance, maxDistance);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var target = mid + Vector3.up * lookHeight + rot * Vector3.back * dist;

            if (!initialized) { basePos = target; initialized = true; }
            basePos = Vector3.SmoothDamp(basePos, target, ref velocity, smoothTime);

            var offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDuration));
                float t = Time.unscaledTime * 25f;
                offset = rot * new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.1f, t) - 0.5f, 0f) * 1.2f * shakeAmp * k * k;
            }

            transform.SetPositionAndRotation(basePos + offset, rot);
        }
    }
}
