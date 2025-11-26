using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
public class BreakApart : MonoBehaviour
{
   public float explosionForce = 300f;
   public float explosionRadius = 3f;
   UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
   private void Awake()
   {
       grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
   }
   private void OnEnable()
   {
       grab.selectEntered.AddListener(OnGrab);
   }
   private void OnDisable()
   {
       grab.selectEntered.RemoveListener(OnGrab);
   }
   private void OnGrab(SelectEnterEventArgs args)
   {
       Explode();
   }
   void Explode()
   {
       foreach (Transform child in transform)
       {
           Rigidbody rb = child.GetComponent<Rigidbody>();
           if (rb != null)
           {
               rb.isKinematic = false;
               rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
           }
       }
       // Można wyłączyć XR Grab, żeby obiekt nie był dalej trzymany
       grab.enabled = false;
   }
}