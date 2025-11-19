using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRToggleProxy : MonoBehaviour
{
    public UnityEngine.UI.Toggle uiToggle; // drag your Toggle here

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    void Start()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        interactable.selectEntered.AddListener(OnSelect);
    }

    private void OnSelect(SelectEnterEventArgs args)
    {
        uiToggle.isOn = !uiToggle.isOn;   // flip it each time you "touch" it
    }
}
