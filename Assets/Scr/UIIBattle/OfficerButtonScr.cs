using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OfficerButtonScr : MonoBehaviour
{
    [Header("UI")]
    public Image imageOfficer;
    public Button selectOfficer;
    public TextMeshProUGUI textName;
    public TextMeshProUGUI[] textOfficerStatic;
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
        if (textName != null)
        {
            textName.text = officerName;
        }

        if (imageOfficer != null)
        {
            imageOfficer.sprite = sprite;
        }
    }

    // Заповнює рядки статистики цього слота (тактика/атака/захист/організація).
    // Зайві елементи textOfficerStatic (якщо їх більше, ніж рядків) очищаються.
    public void SetOfficerStats(Officer officer)
    {
        if (officer != null)
        {
            textOfficerStatic[0].text = officer.tacticsLv.ToString();
            textOfficerStatic[1].text = officer.attackLv.ToString();
            textOfficerStatic[2].text = officer.defenseLv.ToString();
            textOfficerStatic[3].text = officer.organizationLv.ToString();
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