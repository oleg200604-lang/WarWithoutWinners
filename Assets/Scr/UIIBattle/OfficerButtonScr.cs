using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OfficerButtonScr : MonoBehaviour
{
    [Header("UI")]
    public Image imageOfficer;
    public Button selectOfficer;
    public TextMeshProUGUI Name;
    public Button buttonRaise;
    public Button buttonLower;
    public int officerIndex = -1;


    // =========================================================
    // DISPLAY
    // =========================================================

    public void SetOfficerIndex(int index)
    {
        officerIndex = index;
    }

    public void SetOfficerName(string officerName, Sprite sprite)
    {
        if (Name != null)
        {
            Name.text = officerName;
        }

        if(imageOfficer != null)
        {
            imageOfficer.sprite = sprite;
        }
    }

    public void SetInteractable(bool value)
    {
        if (selectOfficer != null)
            selectOfficer.interactable = value;
    }

    public void SetSelectedVisual(bool selected)
    {
        if (selectOfficer == null)
            return;

        Image image = selectOfficer.image;

        if (image != null)
            image.color = selected
                ? Color.yellow
                : Color.white;
    }
}