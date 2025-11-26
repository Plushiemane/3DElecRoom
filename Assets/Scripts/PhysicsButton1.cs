using UnityEngine;
using UnityEngine.Events;

public class PhysicsButton1 : MonoBehaviour
{
    public Transform buttonTop;
    public Transform buttonLowerLimit;
    public Transform buttonUpperLimit;
    public float threshHold = 0.1f;
    public float springStrength = 200f;
    public float damping = 25f;
    public bool isPressed;
    private bool prevPressedState;

    public AudioSource pressedSound;
    public AudioSource releasedSound;
    public Collider[] CollidersToIgnore;
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    private Rigidbody rb;
    private float upperLowerDiff;

    void Start()
    {
        rb = buttonTop.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ButtonTop needs a Rigidbody!");
            return;
        }

        Collider localCollider = GetComponent<Collider>();
        if (localCollider != null)
        {
            Physics.IgnoreCollision(localCollider, buttonTop.GetComponentInChildren<Collider>());

            foreach (Collider singleCollider in CollidersToIgnore)
            {
                Physics.IgnoreCollision(localCollider, singleCollider);
            }
        }

        if (transform.eulerAngles != Vector3.zero)
        {
            Vector3 savedAngle = transform.eulerAngles;
            transform.eulerAngles = Vector3.zero;
            upperLowerDiff = buttonUpperLimit.position.y - buttonLowerLimit.position.y;
            transform.eulerAngles = savedAngle;
        }
        else
        {
            upperLowerDiff = buttonUpperLimit.position.y - buttonLowerLimit.position.y;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // limit button within range
        if (buttonTop.position.y > buttonUpperLimit.position.y)
        {
            buttonTop.position = buttonUpperLimit.position;
            rb.linearVelocity = Vector3.zero;
        }
        else if (buttonTop.position.y < buttonLowerLimit.position.y)
        {
            buttonTop.position = buttonLowerLimit.position;
            rb.linearVelocity = Vector3.zero;
        }

        // apply spring force toward upper limit
        float displacement = buttonUpperLimit.position.y - buttonTop.position.y;
        float springForce = displacement * springStrength - rb.linearVelocity.y * damping;
        rb.AddForce(Vector3.up * springForce, ForceMode.Force);
    }

    void Update()
    {
        // keep button upright
        buttonTop.transform.localPosition = new Vector3(0, buttonTop.transform.localPosition.y, 0);
        buttonTop.transform.localEulerAngles = Vector3.zero;

        // check pressed state
        bool currentlyPressed = Vector3.Distance(buttonTop.position, buttonLowerLimit.position) < upperLowerDiff * threshHold;

        // trigger once per press/release
        if (currentlyPressed && !prevPressedState)
        {
            Pressed();
        }
        else if (!currentlyPressed && prevPressedState)
        {
            Released();
        }

        prevPressedState = currentlyPressed;
    }

    void Pressed()
    {
        isPressed = true;
        pressedSound.pitch = 1f;
        if (pressedSound) pressedSound.Play();
        onPressed.Invoke();
        Debug.Log("Button pressed");
    }

    void Released()
    {
        isPressed = false;
        releasedSound.pitch = Random.Range(1.1f, 1.2f);
        if (releasedSound) releasedSound.Play();
        onReleased.Invoke();
        Debug.Log("Button released");
    }
}
