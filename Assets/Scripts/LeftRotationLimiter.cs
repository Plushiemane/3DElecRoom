using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class LeftRotationLimiter : MonoBehaviour
{
    public Transform master;            // master object
    public Transform follower;          // follower object
    public float rotationSpeed = 90f;   // degrees/sec

    // lights (assign in Inspector)
    public GameObject greenLight;
    public GameObject redLight;

    private bool isRotating = false;
    private bool isMovingForward = false;

    private float targetRotation = 0f;
    public string apiUrl = "http://localhost:2020";
    
    void Awake()
    {
        UpdateLights("red");
    }
    public void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
        {
            targetRotation = -90f;
            isRotating = true;
            isMovingForward = true;
        }
        else
        {
            targetRotation = 0f;
            isRotating = true;
            isMovingForward = false;
        }
    }

    void Update()
    {
        // Always update lights to reflect current rest/rotation state

        if (!isRotating || master == null || follower == null) return;
        // Calculate rotation step
        HandleRotation(isMovingForward);
    }

    private IEnumerator SendAPIRequest()
    {
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(apiUrl, ""))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API request failed: {request.error}");
            }
            else
            {
                Debug.Log("API request successful");
            }
        }
    }
    private void HandleRotation(bool isMovingForward)
    {
        Vector3 masterAngles = master.localEulerAngles;
        Vector3 followerAngles = follower.localEulerAngles;
        float step = rotationSpeed * Time.deltaTime;

        if (isMovingForward)
        {
            // use MoveTowardsAngle to handle 0-360 wrap correctly
            float current = masterAngles.x;
            float newX = Mathf.MoveTowardsAngle(current, -90f, step);
            master.localEulerAngles = new Vector3(newX, 0f,90f);
            follower.localEulerAngles = new Vector3(newX, 0f, 90f);

            // update lights immediately for smoother UX

            if (Mathf.Abs(Mathf.DeltaAngle(newX, -90f)) < 0.1f)
            {
                isRotating = false;
                master.localEulerAngles = new Vector3(-90f, 0f, 90f);
                follower.localEulerAngles = new Vector3(-90f, 0f, 90f);
                UpdateLights("green");
                StartCoroutine(SendAPIRequest());
                return;
            }
        }
        else
        {
            // return to 0 around same axis here (use MoveTowardsAngle and positive step)
            float current = masterAngles.x;
            float newX = Mathf.MoveTowardsAngle(current, 0f, step);
            master.localEulerAngles = new Vector3(newX, 0f, 90f);
            follower.localEulerAngles = new Vector3(newX, 0f, 90f);

            // update lights immediately for smoother UX
            UpdateLights("red");

            if (Mathf.Abs(Mathf.DeltaAngle(newX, 0f)) < 0.1f)
            {
                isRotating = false;
                master.localEulerAngles = new Vector3(0f, 0f,90f);
                follower.localEulerAngles = new Vector3(0f, 0f,90f);
                UpdateLights("red");
                return;
            }
        }
    } 

    private void UpdateLights(string GreenOrRed)
    {

        if (GreenOrRed == "green")
        {
            redLight.SetActive(false);
            greenLight.SetActive(true);

        }
        else
        {
            greenLight.SetActive(false);
            redLight.SetActive(true);
        }
    }
}
