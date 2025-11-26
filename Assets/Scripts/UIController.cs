using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Toggle rotationToggle;
    public LeftRotationLimiter rotationLimiter;

    void Start()
    {
        if (rotationToggle != null)
        {
            rotationToggle.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    void OnToggleChanged(bool isOn)
    {
        if (rotationLimiter != null)
        {
            rotationLimiter.OnToggleValueChanged(isOn);
        }
    }

    void OnDestroy()
    {
        if (rotationToggle != null)
        {
            rotationToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}