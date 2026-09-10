using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class OfiicerButtonScr : MonoBehaviour
{
    public Image imageOfficer;
    public Button selectOfficer;
    public TextMeshProUGUI Name;
    public Officer officer;
    public Button buttonRaise;
    public Button buttonLower;


    public void ButtonRaise()
    {
        officer.Raise();
    }
    public void ButtonLower()
    {
        officer.Lower();
    }
}
