using UnityEngine;
using TMPro;

public class XRDropdownOpener : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    public void Open()
    {
        dropdown.Show();
    }
}
