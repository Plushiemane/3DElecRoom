using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MultiGrabInteractable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    [Tooltip("Possible grab points on the object")]
    public Transform[] grabPoints;

    protected override void OnSelectEntering(SelectEnterEventArgs args)
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

        base.OnSelectEntering(args);
    }
}
