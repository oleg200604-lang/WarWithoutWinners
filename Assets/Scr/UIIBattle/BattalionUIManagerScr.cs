using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class BattalionUIManagerScr : MonoBehaviour
{

    public GameObject commandPanel;
    public GameObject noneButton, moveButton, attackButton, defendButton;
    [Header("Артилерія: розкладка / обстріл")]
    public GameObject deployButton, undeployButton, rotateButton, bombardButton;

    [Header("Людський ресурс")]
    public Button reinforceButton;
    [Tooltip("Скільки особового складу додається за одне натискання кнопки поповнення.")]
    public int reinforceAmountPerClick = 10;

    [Header("HUD глобальних ресурсів (необов'язково)")]
    public TextMeshProUGUI personnelText;
    public TextMeshProUGUI suppliesText;
    public TextMeshProUGUI commandText;

    [Header("По одному слоту buttonRegiment на кожен можливий полк")]
    public List<RegimentButtonGroup> regimentButtonGroups;

    public BatalionManagerScr batalionManager;

    private void Awake()
    {
        if (reinforceButton != null)
        {
            reinforceButton.onClick.AddListener(() =>
            {
                if (batalionManager.selectBattalion != null)
                {
                    batalionManager.ReinforceBattalion(batalionManager.selectBattalion, reinforceAmountPerClick);
                    CheckButtalion();
                }
            });
        }
    }

    private void Update()
    {
        RefreshResourceHud();
    }

    private void RefreshResourceHud()
    {
        if (batalionManager == null)
            return;

        Ressurs r = batalionManager.ressurs;

        if (personnelText != null)
            personnelText.text = r.personnel.ToString();

        if (suppliesText != null)
            suppliesText.text = r.supplies.ToString();

        if (commandText != null)
            commandText.text =  r.command + " +(" + r.planning  + ")/" + r.commandMax;
    }

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
        Regiment regiment = batalionManager.selectRegiment;

        bool hasBattalionSelected = battalionScr != null;
        bool hasRegimentSelected = regiment != null;

        // Move/Attack/Defend — одна й та сама панель для батальйона і для
        // полку: BatalionManagerScr сам розрізняє ціль всередині обробників.
        bool hasSelection = hasBattalionSelected || hasRegimentSelected;

        bool isNone = hasBattalionSelected && battalionScr.battalion.type == BattalionType.none;
        bool isArtillery = hasBattalionSelected && battalionScr.battalion.type == BattalionType.artillery;
        bool isDeployed = isArtillery && battalionScr.GetProjectedDeployedState(batalionManager.CommandDuty);

        noneButton.SetActive(hasSelection && !isNone);
        moveButton.SetActive(hasSelection && !isNone && !isDeployed);
        attackButton.SetActive(hasSelection && !isNone && !isDeployed);
        defendButton.SetActive(hasSelection && !isNone);

        // Розкладка/обстріл — специфічні для окремого артилерійського
        // батальйону, полк такі накази поки не підтримує.
        deployButton.SetActive(hasBattalionSelected && isArtillery && !isDeployed);
        undeployButton.SetActive(hasBattalionSelected && isDeployed);
        rotateButton.SetActive(hasBattalionSelected && isDeployed);
        bombardButton.SetActive(hasBattalionSelected && isDeployed);

        if (reinforceButton != null)
        {
            bool canReinforce = hasBattalionSelected && !isNone && battalionScr.GetMissingPersonnel() > 0;
            reinforceButton.gameObject.SetActive(canReinforce);
        }

        RefreshRegimentButtons();
    }

    // Єдине місце, що керує всіма buttonRegiment-слотами: показ/приховування,
    // підписки на кнопки та їх interactable-стан. Викликати після будь-якої
    // зміни — вибір батальйону, створення/додавання/видалення з полку.
    public void RefreshRegimentButtons()
    {
        BattalionScr selected = batalionManager.selectBattalion;

        for (int i = 0; i < regimentButtonGroups.Count; i++)
        {
            RegimentButtonGroup group = regimentButtonGroups[i];

            if (group == null || group.root == null)
                continue;

            bool hasRegiment = i < batalionManager.regiments.Count;

            group.root.SetActive(hasRegiment);

            if (!hasRegiment)
                continue;

            Regiment regiment = batalionManager.regiments[i];

            if (group.selectButton != null)
            {
                group.selectButton.onClick.RemoveAllListeners();
                group.selectButton.onClick.AddListener(() =>
                {
                    batalionManager.SelectRegimentUnit(regiment);
                    RefreshRegimentButtons();
                });

                // Полк обрано як юніт командування — підсвітимо, якщо є Image.
                Image selectImage = group.selectButton.image;
                if (selectImage != null)
                    selectImage.color = (batalionManager.selectRegiment == regiment) ? Color.yellow : Color.white;
            }

            if (group.addButton != null)
            {
                group.addButton.onClick.RemoveAllListeners();
                group.addButton.onClick.AddListener(() =>
                {
                    batalionManager.AddRegiment(batalionManager.selectBattalion, regiment);
                    RefreshRegimentButtons();
                });

                group.addButton.interactable = selected != null;
            }

            if (group.removeButton != null)
            {
                group.removeButton.onClick.RemoveAllListeners();
                group.removeButton.onClick.AddListener(() =>
                {
                    batalionManager.RemovRegiment(batalionManager.selectBattalion, regiment);
                    RefreshRegimentButtons();
                });

                bool canRemove = selected != null && selected.regimentredID == i;

                group.removeButton.interactable = canRemove;
            }
        }
    }
}
[System.Serializable]
public class RegimentButtonGroup
{
    public GameObject root;
    public Button selectButton;
    public Button addButton;
    public Button removeButton;
}