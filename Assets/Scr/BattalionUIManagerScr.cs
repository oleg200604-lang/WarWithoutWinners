using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BattalionUIManagerScr : MonoBehaviour
{

    public GameObject commandPanel;
    public GameObject noneButton, moveButton, attackButton, defendButton, addrRgiment, removeRegiment;
    [Header("Артилерія: розкладка / обстріл")]
    public GameObject deployButton, undeployButton, rotateButton, bombardButton;

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

        // Було: switch лише на infantry/none — для cavalry, artillery,
        // mechanically кнопки лишались у попередньому стані (звідси
        // враження, що "все показується"). Тепер стан кнопок рахується
        // з реальних даних батальйону для БУДЬ-ЯКОГО типу.
        bool isNone = battalionScr.battalion.type == BattalionType.none;
        bool isArtillery = battalionScr.battalion.type == BattalionType.artillery;
        bool isDeployed = isArtillery && battalionScr.isDeployed;

        noneButton.SetActive(!isNone);
        moveButton.SetActive(!isNone && !isDeployed);
        attackButton.SetActive(!isNone && !isDeployed);
        defendButton.SetActive(!isNone);

        // Накази, специфічні для розкладеної артилерії.
        deployButton.SetActive(isArtillery && !isDeployed);
        undeployButton.SetActive(isDeployed);
        rotateButton.SetActive(isDeployed);
        bombardButton.SetActive(isDeployed);

        addrRgiment.SetActive(false);
        removeRegiment.SetActive(false);

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