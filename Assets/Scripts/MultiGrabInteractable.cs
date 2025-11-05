using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Rename to avoid conflict with existing class
public class StickGrabInteractable : XRGrabInteractable
{
    [Tooltip("Possible grab points on the object")]
    public Transform[] grabPoints;
    
    private InsulationSnapper snapper;
    private LeftRotationLimiter rotationLimiter;
    private bool wasSnapped;

    protected override void Awake()
    {
        base.Awake();
        snapper = GetComponent<InsulationSnapper>();
        rotationLimiter = GetComponentInParent<LeftRotationLimiter>();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Find closest grab point to the interactor
        if (grabPoints != null && grabPoints.Length > 0)
        {
            Transform interactorTransform = args.interactorObject.transform;
            Transform closest = grabPoints[0];
            float minDist = Vector3.Distance(interactorTransform.position, closest.position);

            foreach (var p in grabPoints)
            {
                float d = Vector3.Distance(interactorTransform.position, p.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = p;
                }
            }

            attachTransform = closest;
        }

        // Handle snapper state
        if (snapper != null)
        {
            wasSnapped = snapper.snappedStick != null;
            if (wasSnapped)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
                // Tell limiter we're manually rotating
                if (rotationLimiter != null)
                {
                    rotationLimiter.SetManualRotation(true);
                }
            }
        }

        base.OnSelectEntered(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (snapper != null && wasSnapped)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            // Resume automatic rotation control
            if (rotationLimiter != null)
            {
                rotationLimiter.SetManualRotation(false);
            }
        }
    }
}