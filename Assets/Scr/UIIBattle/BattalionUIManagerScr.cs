using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BattalionUIManagerScr : MonoBehaviour
{

    public GameObject commandPanel;
    public GameObject noneButton, moveButton, attackButton, defendButton;
    [Header("Артилерія: розкладка / обстріл")]
    public GameObject deployButton, undeployButton, rotateButton, bombardButton;

    public List<GameObject> buttonRegiment;
    public List<Button> buttonSelectRegiment;
    public List<Button> buttonAddRegiment;
    public List<Button> buttonRemoveRegiment;
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
        bool isDeployed = isArtillery && battalionScr.GetProjectedDeployedState(batalionManager.CommandDuty);

        noneButton.SetActive(!isNone);
        moveButton.SetActive(!isNone && !isDeployed);
        attackButton.SetActive(!isNone && !isDeployed);
        defendButton.SetActive(!isNone);

        deployButton.SetActive(isArtillery && !isDeployed);
        undeployButton.SetActive(isDeployed);
        rotateButton.SetActive(isDeployed);
        bombardButton.SetActive(isDeployed);

    }

    public void ChekRegiment(int regiment)
    {
        buttonRegiment[regiment].SetActive(true);
        if (batalionManager.regiments[regiment] != null)
        {
            if (batalionManager.selectBattalion == true  )
            {
                if (batalionManager.selectBattalion.regimentredID < 0)
                {
                    buttonAddRegiment[regiment].gameObject.SetActive(true);
                    buttonRemoveRegiment[regiment].gameObject.SetActive(false);
                    buttonAddRegiment[regiment].onClick.AddListener(() => batalionManager.AddRegiment(batalionManager.selectBattalion, batalionManager.regiments[regiment]));
                }
                else if (batalionManager.selectBattalion.regimentredID == regiment)
                {
                    buttonAddRegiment[regiment].gameObject.SetActive(false);
                    buttonRemoveRegiment[regiment].gameObject.SetActive(true);
                    buttonRemoveRegiment[regiment].onClick.AddListener(() => batalionManager.RemovRegiment(batalionManager.selectBattalion, batalionManager.regiments[regiment]));
                }
                else
                {
                    buttonAddRegiment[regiment].gameObject.SetActive(false);
                    buttonRemoveRegiment[regiment].gameObject.SetActive(false);
                }
            }
            else
            {
                buttonAddRegiment[regiment].gameObject.SetActive(false);
                buttonRemoveRegiment[regiment].gameObject.SetActive(false);
            }

            buttonSelectRegiment[regiment].onClick.AddListener(() => batalionManager.SelectRegiment(batalionManager.regiments[regiment]));
        }
    }
}