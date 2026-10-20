using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 720f;
    public float gravity = -25f;

    [HideInInspector] public float speedMultiplier = 1f;

    CharacterController controller;
    Vector3 moveDir;
    float verticalVelocity;
    Vector3 dashVelocity;
    float dashTimeLeft;
    Vector3 knockVelocity;

    void Awake() => controller = GetComponent<CharacterController>();

    public void SetMove(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        moveDir = Vector3.ClampMagnitude(worldDirection, 1f);
    }

    public void FaceTowards(Vector3 direction, bool instant = false)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        var target = Quaternion.LookRotation(direction);
        transform.rotation = instant
            ? target
            : Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    public void Dash(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (duration <= 0f || distance <= 0f || direction.sqrMagnitude < 0.0001f) return;
        dashVelocity = direction.normalized * (distance / duration);
        dashTimeLeft = duration;
    }

    public void CancelDash() => dashTimeLeft = 0f;

    public void Knockback(Vector3 velocity)
    {
        velocity.y = 0f;
        knockVelocity = velocity;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        Vector3 velocity = moveDir * (moveSpeed * speedMultiplier);

        if (dashTimeLeft > 0f)
        {
            velocity += dashVelocity;
            dashTimeLeft -= dt;
        }

        velocity += knockVelocity;
        knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 25f * dt);

        velocity.y = verticalVelocity;
        controller.Move(velocity * dt);
    }
}
