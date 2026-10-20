using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public PlayerController target;
    public Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
    public float distance = 5f;
    public float minPitch = -20f;
    public float maxPitch = 60f;

    float yaw;
    float pitch = 20f;

    void Start()
    {
        if (target != null) yaw = target.transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += target.LookInput.x;
            pitch = Mathf.Clamp(pitch - target.LookInput.y, minPitch, maxPitch);
        }

        var rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.transform.position + pivotOffset;
        Vector3 desired = pivot - rotation * Vector3.forward * distance;

        // Pull the camera in front of walls between the pivot and the desired position.
        Vector3 dir = desired - pivot;
        if (Physics.SphereCast(pivot, 0.2f, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerController>() == null
            && hit.collider.GetComponentInParent<CombatController>() == null)
        {
            desired = pivot + dir.normalized * Mathf.Max(0.5f, hit.distance);
        }

        transform.SetPositionAndRotation(desired, rotation);
    }
}
