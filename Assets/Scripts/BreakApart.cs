using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using System.Collections.Generic; // Add this line

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class BreakApart : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionForce = 80f;
    public float explosionRadius = 2f;
    
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    private bool hasExploded = false;
    private GameObject explosionContainer;
    
    private void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }
    
    private void OnEnable()
    {
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
        }
    }
    
    private void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
        }
    }
    
    private void OnGrab(SelectEnterEventArgs args)
    {
        if (!hasExploded)
        {
            StartCoroutine(BreakApartCar());
            hasExploded = true;
        }
    }
    
    IEnumerator BreakApartCar()
    {
        // Create a new container for exploded parts
        explosionContainer = new GameObject($"{name}_ExplodedParts");
        explosionContainer.transform.position = transform.position;
        
        // Move all children to the new container
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
        {
            children.Add(child);
        }
        
        yield return null; // Wait one frame
        
        foreach (Transform child in children)
        {
            child.SetParent(explosionContainer.transform, true);
            
            // Add physics if needed
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = child.gameObject.AddComponent<Rigidbody>();
            }
            
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 5f;
            rb.linearDamping = 1f;
            
            // Ensure collider
            if (child.GetComponent<Collider>() == null)
            {
                MeshCollider collider = child.gameObject.AddComponent<MeshCollider>();
                collider.convex = true;
            }
        }
        
        // Apply explosion force to all parts
        foreach (Transform child in explosionContainer.transform)
        {
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomDir = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.3f, 1f),
                    Random.Range(-1f, 1f)
                ).normalized;
                
                rb.AddForce(randomDir * explosionForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 30f, ForceMode.Impulse);
            }
        }
        
        // Make original object non-interactive
        StartCoroutine(MakeOriginalNonInteractive());
    }
    
    IEnumerator MakeOriginalNonInteractive()
    {
        yield return new WaitForSeconds(0.1f);
        
        // Remove the grab component entirely instead of disabling
        if (grab != null)
        {
            Destroy(grab);
        }
        
        // Hide or destroy the original empty parent
        gameObject.SetActive(false);
        
        // Destroy after delay
        Destroy(gameObject, 5f);
        
        // Also destroy exploded parts after delay
        if (explosionContainer != null)
        {
            Destroy(explosionContainer, 10f);
        }
    }
}