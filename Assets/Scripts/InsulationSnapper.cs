using UnityEngine;

public class InsulationSnapper : MonoBehaviour
{
    [Tooltip("If empty, this transform is used as the snap point (center).")]
    public Transform snapTarget;
    [Tooltip("Tag used to identify the stick. You can also leave tag blank and check nameContains.")]
    public string stickTag = "InsulationStick";
    [Tooltip("Optional substring check against name if tag not used.")]
    public string nameContains = "InsulationStick";
    [Tooltip("Use trigger events (requires a Trigger collider on this object) or distance-check.")]
    public bool useTrigger = true;
    [Tooltip("Radius used when not using trigger.")]
    public float snapRadius = 0.2f;
    [Tooltip("Make the stick kinematic while snapped to avoid physics fighting the parent.")]
    public bool makeKinematicOnSnap = true;
    [Tooltip("Optional: assign the hole transform here if it exists separately and should become a child of the stick.")]
    public Transform holeTransform;
    public LeftRotationLimiter limiter; // assign in Inspector or auto-find

    public GameObject snappedStick { get; private set; }  // Make getter public but setter private
    Rigidbody snappedRb;
    Vector3 savedRbVelocity;
    Vector3 savedRbAngularVelocity;
    bool savedKinematic;

    void Reset()
    {
        snapTarget = transform;
    }

    void Start()
    {
        if (snapTarget == null) snapTarget = transform;
        if (limiter == null)
            limiter = GetComponentInParent<LeftRotationLimiter>(); // try auto-find
    }

    void Update()
    {
        if (!useTrigger && snappedStick == null)
        {
            // simple proximity check for any candidate with tag/name
            GameObject candidate = FindNearbyStick();
            if (candidate != null) TrySnap(candidate);
        }
    }

    GameObject FindNearbyStick()
    {
        // find objects with tag first
        if (!string.IsNullOrEmpty(stickTag))
        {
            GameObject[] withTag;
            try { withTag = GameObject.FindGameObjectsWithTag(stickTag); }
            catch { withTag = null; }
            if (withTag != null)
            {
                foreach (var g in withTag)
                    if (Vector3.Distance(g.transform.position, snapTarget.position) <= snapRadius) return g;
            }
        }

        // fallback: search by name substring
        if (!string.IsNullOrEmpty(nameContains))
        {
            var all = GameObject.FindObjectsOfType<Transform>();
            foreach (var t in all)
            {
                if (t.name.Contains(nameContains) && Vector3.Distance(t.position, snapTarget.position) <= snapRadius)
                    return t.gameObject;
            }
        }

        return null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger || snappedStick != null) return;
        if (IsStickCandidate(other.gameObject)) TrySnap(other.gameObject);
    }

    bool IsStickCandidate(GameObject g)
    {
        if (!string.IsNullOrEmpty(stickTag) && g.CompareTag(stickTag)) return true;
        if (!string.IsNullOrEmpty(nameContains) && g.name.Contains(nameContains)) return true;
        return false;
    }

    public void TrySnap(GameObject stick)
    {
        if (snappedStick != null || stick == null) return;
        if (Vector3.Distance(stick.transform.position, snapTarget.position) > Mathf.Max(0.01f, snapRadius)) return;

        snappedRb = stick.GetComponent<Rigidbody>();
        if (snappedRb != null)
        {
            savedKinematic = snappedRb.isKinematic;
            snappedRb.constraints = RigidbodyConstraints.FreezePosition | 
                                   RigidbodyConstraints.FreezeRotationX | 
                                   RigidbodyConstraints.FreezeRotationY;
            snappedRb.isKinematic = false;
        }

        stick.transform.SetParent(snapTarget, worldPositionStays: true);
        stick.transform.localPosition = Vector3.zero;

        // Set initial rotation to -180 degrees around Z
        stick.transform.localRotation = Quaternion.Euler(0f, 0f, -180f);

        if (limiter != null)
        {
            limiter.AttachStick(stick.transform);
        }

        if (holeTransform != null)
        {
            holeTransform.SetParent(stick.transform, worldPositionStays: false);
        }

        snappedStick = stick;
    }

    public void ReleaseSnap(bool restorePhysics = true)
    {
        if (snappedStick == null) return;

        // Tell limiter the stick is detached before unparenting (or after — consistent is fine)
        if (limiter != null)
            limiter.DetachStick();

        if (restorePhysics && snappedRb != null)
        {
            snappedRb.isKinematic = savedKinematic;
            snappedRb.linearVelocity = savedRbVelocity;
            snappedRb.angularVelocity = savedRbAngularVelocity;
        }

        snappedStick.transform.SetParent(null, worldPositionStays: true);
        snappedStick = null;
        snappedRb = null;
    }
}