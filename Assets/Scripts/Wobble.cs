using UnityEngine;

public class Wobble : MonoBehaviour
{
    private Renderer rend;

    private Vector3 lastPos;
    private Vector3 velocity;

    private Vector3 lastRot;
    private Vector3 angularVelocity;

    [Header("Wobble")]
    [SerializeField] private float MaxWobble = 0.03f;
    [SerializeField] private float WobbleSpeed = 1f;
    [SerializeField] private float Recovery = 1f;

    [Header("Clamps")]
    [SerializeField] private float maxWobbleAmountX = 0.05f;
    [SerializeField] private float maxWobbleAmountZ = 0.05f;

    [SerializeField] private float maxVelocity = 5f;
    [SerializeField] private float maxAngularVelocity = 20f;

    private float wobbleAmountX;
    private float wobbleAmountZ;

    private float wobbleAmountToAddX;
    private float wobbleAmountToAddZ;

    private float pulse;
    private float time = 0.5f;

    private void Start()
    {
        rend = GetComponent<Renderer>();

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }

    private void Update()
    {
        time += Time.deltaTime;

        // Recover wobble back toward zero
        wobbleAmountToAddX = Mathf.Lerp(
            wobbleAmountToAddX,
            0f,
            Time.deltaTime * Recovery
        );

        wobbleAmountToAddZ = Mathf.Lerp(
            wobbleAmountToAddZ,
            0f,
            Time.deltaTime * Recovery
        );

        // Sine wobble
        pulse = 2f * Mathf.PI * WobbleSpeed;

        wobbleAmountX =
            wobbleAmountToAddX *
            Mathf.Sin(pulse * time);

        wobbleAmountZ =
            wobbleAmountToAddZ *
            Mathf.Sin(pulse * time);

        // Final shader safety clamp
        wobbleAmountX = Mathf.Clamp(
            wobbleAmountX,
            -maxWobbleAmountX,
            maxWobbleAmountX
        );

        wobbleAmountZ = Mathf.Clamp(
            wobbleAmountZ,
            -maxWobbleAmountZ,
            maxWobbleAmountZ
        );

        rend.material.SetFloat("_WobbleX", wobbleAmountX);
        rend.material.SetFloat("_WobbleZ", wobbleAmountZ);

        // Movement velocity
        velocity =
            (lastPos - transform.position) /
            Time.deltaTime;

        velocity = Vector3.ClampMagnitude(
            velocity,
            maxVelocity
        );

        // Rotation velocity
        angularVelocity =
            transform.rotation.eulerAngles -
            lastRot;

        angularVelocity.x = Mathf.Clamp(
            Mathf.DeltaAngle(lastRot.x, transform.rotation.eulerAngles.x),
            -maxAngularVelocity,
            maxAngularVelocity
        );

        angularVelocity.y = Mathf.Clamp(
            Mathf.DeltaAngle(lastRot.y, transform.rotation.eulerAngles.y),
            -maxAngularVelocity,
            maxAngularVelocity
        );

        angularVelocity.z = Mathf.Clamp(
            Mathf.DeltaAngle(lastRot.z, transform.rotation.eulerAngles.z),
            -maxAngularVelocity,
            maxAngularVelocity
        );

        // Add wobble force
        wobbleAmountToAddX +=
            Mathf.Clamp(
                (velocity.x + angularVelocity.z * 0.2f) *
                MaxWobble,
                -MaxWobble,
                MaxWobble
            );

        wobbleAmountToAddZ +=
            Mathf.Clamp(
                (velocity.z + angularVelocity.x * 0.2f) *
                MaxWobble,
                -MaxWobble,
                MaxWobble
            );

        // IMPORTANT:
        // Clamp the accumulated wobble too.
        wobbleAmountToAddX = Mathf.Clamp(
            wobbleAmountToAddX,
            -maxWobbleAmountX,
            maxWobbleAmountX
        );

        wobbleAmountToAddZ = Mathf.Clamp(
            wobbleAmountToAddZ,
            -maxWobbleAmountZ,
            maxWobbleAmountZ
        );

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }
}