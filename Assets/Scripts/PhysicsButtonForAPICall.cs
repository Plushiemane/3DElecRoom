using UnityEngine;
using UnityEngine.Networking;

public class PhysicsButtonForAPICall : MonoBehaviour
{
    public Transform buttonTop;
    public Transform buttonLowerLimit;
    public Transform buttonUpperLimit;

    public float threshHold = 0.1f;
    public float springStrength = 200f;
    public float damping = 25f;

    private Rigidbody rb;
    private float upperLowerDiff;
    private bool prevPressedState = false;
    private bool isPressed = false;

    private const string url = "http://192.168.1.10/LED_ON";

    void Start()
    {
        rb = buttonTop.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ButtonTop must have a Rigidbody!");
            return;
        }

        upperLowerDiff = buttonUpperLimit.position.y - buttonLowerLimit.position.y;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Clamp button position
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

        // Spring force
        float displacement = buttonUpperLimit.position.y - buttonTop.position.y;
        float springForce = displacement * springStrength - rb.linearVelocity.y * damping;
        rb.AddForce(Vector3.up * springForce);
    }

    void Update()
    {
        // Keep upright
        buttonTop.localPosition = new Vector3(0, buttonTop.localPosition.y, 0);
        buttonTop.localEulerAngles = Vector3.zero;

        // Detect press
        bool currentlyPressed = Vector3.Distance(buttonTop.position, buttonLowerLimit.position) < upperLowerDiff * threshHold;

        if (currentlyPressed && !prevPressedState)
        {
            isPressed = true;
            Debug.Log("Button Pressed");
            SendGETRequest(); // Send GET once per press
        }
        else if (!currentlyPressed && prevPressedState)
        {
            isPressed = false;
            Debug.Log("Button Released");
        }

        prevPressedState = currentlyPressed;
    }

    void SendGETRequest()
    {
        StartCoroutine(SendRequestCoroutine());
    }

    System.Collections.IEnumerator SendRequestCoroutine()
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("GET request failed: " + request.error);
        }
        else
        {
            Debug.Log("GET request successful: " + request.downloadHandler.text);
        }
    }
}
