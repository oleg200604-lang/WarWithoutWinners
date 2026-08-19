using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BattalionUIManagerScr : MonoBehaviour
{

    public GameObject commandPanel;
    public GameObject noneButton, moveButton, attackButton, defendButton, addrRgiment, removeRegiment;

    public List<Button> buttonSelectRegiment;
    public BatalionManagerScr batalionManager;
    public void CommandPanel(bool isActvie)
    {
        // CheckButtalion() читає selectBattalion.battalion.type — це має
        // сенс лише коли панель показуємо (є вибраний батальйон). Коли
        // ховаємо панель, selectBattalion уже може бути null (кінець
        // ходу, повторний клік по батальйону) — виклик тут викликав
        // NullReferenceException.
        if (isActvie)
        {
            CheckButtalion();
        }

        commandPanel.SetActive(isActvie);
    }
    private void CheckButtalion()
    {
        BattalionScr battalionScr = batalionManager.selectBattalion;

        if (battalionScr == null)
            return;

        // Перевірка типу батальйону
        switch (battalionScr.battalion.type)
        {
            case BattalionType.none:
                noneButton.SetActive(false);
                moveButton.SetActive(false);
                attackButton.SetActive(false);
                defendButton.SetActive(false);
                break;

            case BattalionType.infantry:
                noneButton.SetActive(true);
                moveButton.SetActive(true);
                attackButton.SetActive(true);
                defendButton.SetActive(true);
                break;
        }

        if (batalionManager.regiments.Count > 0)
        {
            if (battalionScr.regimentredID < 0)
            {
                addrRgiment.SetActive(true);
            }
            else
            {
                removeRegiment.SetActive(true);
            }
        }

    }

    public void ChekRegiment(int regiment)
    {
        if (batalionManager.regiments[regiment] != null)
        {
            buttonSelectRegiment[regiment].gameObject.SetActive(true);
            buttonSelectRegiment[regiment].onClick.AddListener(() => batalionManager.SelectRegiment(batalionManager.regiments[regiment]));
        }
    }


}