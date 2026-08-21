using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

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
        if (isActvie)
        {
            CheckButtalion();
        }

        commandPanel.SetActive(isActvie);
    }
    public void CheckButtalion()
    {
        BattalionScr battalionScr = batalionManager.selectBattalion;

        if (battalionScr == null)
            return;

        bool isNone = battalionScr.battalion.type == BattalionType.none;
        bool isArtillery = battalionScr.battalion.type == BattalionType.artillery;
        bool isDeployed = isArtillery &&  battalionScr.GetProjectedDeployedState(batalionManager.CommandDuty);

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