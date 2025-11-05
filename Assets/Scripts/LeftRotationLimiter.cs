using UnityEngine;

public class LeftRotationLimiter : MonoBehaviour
{
    public Transform master;            // master object
    public Transform follower;          // follower object
    public float followSpeed = 200f;    // degrees/sec follower speed
    public float masterClampSpeed = 360f; // degrees/sec master speed

    // pitch limits (degrees) applied to master's X when stick is attached (defaults: -90 .. 0)
    public float minRelativePitch = -90f;
    public float maxRelativePitch = 0f;

    // attachment state
    public bool stickAttached { get; private set; } = false;
    public Transform attachedStick { get; private set; }

    // stored reference pose when stick was attached
    Quaternion masterBaseLocalRot;
    float stickLocalZAtAttach;

    // Add new field to track if being manually rotated
    private bool isManuallyRotating = false;

    void Update()
    {
        if (master == null || follower == null) return;

        if (stickAttached && attachedStick != null)
        {
            // Get current stick rotation
            float currentZ = attachedStick.localEulerAngles.z;
            
            // Only update follower based on stick's current rotation
            float signedZ = Mathf.DeltaAngle(0f, currentZ);
            float clampedZ = Mathf.Clamp(signedZ, minRelativePitch, maxRelativePitch);
            
            // Update follower to match stick's rotation
            follower.localRotation = Quaternion.Euler(-clampedZ, 0f, 90f);
        }
        else
        {
            // when not attached, follower follows master's local X only (smooth) while preserving its Y/Z
            Vector3 followerEuler = follower.localEulerAngles;
            float followerCurX = Mathf.DeltaAngle(0f, followerEuler.x);
            float masterSignedX = Mathf.DeltaAngle(0f, master.localEulerAngles.x);
            Quaternion followerTargetLocal = Quaternion.Euler(masterSignedX, followerEuler.y, followerEuler.z);
            follower.localRotation = Quaternion.RotateTowards(follower.localRotation, followerTargetLocal, followSpeed * Time.deltaTime);
        }
    }

    // Call this after parenting/alignment so localEulerAngles.z is meaningful
    public void AttachStick(Transform stick)
    {
        if (stick == null) return;
        attachedStick = stick;
        stickAttached = true;

        // record baseline stick local Z (-180..180)
        stickLocalZAtAttach = Mathf.DeltaAngle(0f, attachedStick.localEulerAngles.z);

        // record master's base local rotation at attach
        masterBaseLocalRot = master.localRotation;
    }

    public void DetachStick()
    {
        attachedStick = null;
        stickAttached = false;
    }

    // Add method to toggle manual rotation
    public void SetManualRotation(bool isManual)
    {
        isManuallyRotating = isManual;
    }
}
