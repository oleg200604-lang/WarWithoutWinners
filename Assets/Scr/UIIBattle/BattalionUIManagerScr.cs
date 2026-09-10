using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class BattalionUIManagerScr : MonoBehaviour
{
    public GameObject commandPanel;
    public GameObject officerPanel;
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

    [Header("Офіцери (фіксовані слоти під усіх офіцерів з бази)")]
    public OfiicerButton[] officers;
    [Tooltip("Кнопка, яка відкриває/закриває панель вибору офіцера.")]
    public Button officerSelect;


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

        if (officerSelect != null)
        {
            officerSelect.onClick.AddListener(SelectOfficerPanel);
        }

        // Кожен слот у officers — фіксована UI-кнопка під конкретного офіцера з бази.
        // Підписуємось один раз тут, індекс захоплюємо в локальну змінну (замикання).
        if (officers != null)
        {
            for (int i = 0; i < officers.Length; i++)
            {
                int index = i;
                OfiicerButton slot = officers[index];

                if (slot == null)
                    continue;

                if (slot.selectOfficer != null)
                {
                    slot.selectOfficer.onClick.RemoveAllListeners();
                    slot.selectOfficer.onClick.AddListener(() => SelectOfficer(index));
                }

                if (slot.buttonRaise != null)
                {
                    slot.buttonRaise.onClick.RemoveAllListeners();
                    slot.buttonRaise.onClick.AddListener(() => OnOfficerRankButton(slot, raise: true));
                }

                if (slot.buttonLower != null)
                {
                    slot.buttonLower.onClick.RemoveAllListeners();
                    slot.buttonLower.onClick.AddListener(() => OnOfficerRankButton(slot, raise: false));
                }
            }
        }
    }

    private void Update()
    {
        RefreshResourceHud();
    }

    // Відкриває/закриває панель вибору офіцера та оновлює список слотів під поточний вибір.
    public void SelectOfficerPanel()
    {
        if (officerPanel == null)
            return;


        officerPanel.SetActive(!officerPanel.activeSelf);
        RefreshOfficerButtons();
    }

    // Викликається кнопкою конкретного слота (officers[officerIndex]).
    // Куди призначити офіцера, вирішується поточним вибором:
    // обрано батальйон -> офіцер іде батальйону, обрано полк -> офіцер іде полку.
    public void SelectOfficer(int officerIndex)
    {
        if (officers == null || officerIndex < 0 || officerIndex >= officers.Length)
            return;

        Officer officer = officers[officerIndex].officer;

        if (officer == null)
            return;

        if (batalionManager.selectBattalion != null)
        {
            batalionManager.selectBattalion.SelectOfficer(officer);
        }
        else if (batalionManager.selectRegiment != null)
        {
            batalionManager.selectRegiment.SelectOfficer(officer);
        }
        else
        {
            return;
        }

        RefreshOfficerButtons();
        CheckButtalion();
    }

    // Кнопки підвищення/пониження звання конкретного офіцера. Звання впливає на
    // ефективність пасивних бонусів, тож перераховуємо статистику всіх батальйонів,
    // де цей офіцер зараз призначений (особисто або як офіцер полку).
    private void OnOfficerRankButton(OfiicerButton slot, bool raise)
    {
        if (slot == null || slot.officer == null)
            return;

        if (raise)
            slot.ButtonRaise();
        else
            slot.ButtonLower();

        RecalculateBattalionsForOfficer(slot.officer);
        RefreshOfficerButtons();
    }

    private void RecalculateBattalionsForOfficer(Officer officer)
    {
        BattalionScr[] allBattalions = FindObjectsOfType<BattalionScr>();

        for (int i = 0; i < allBattalions.Length; i++)
        {
            BattalionScr battalionScr = allBattalions[i];

            if (battalionScr.officer == officer || battalionScr.officerRegiment == officer)
                battalionScr.RecalculateStats();
        }
    }

    // Оновлює вигляд усіх слотів офіцерів під поточний вибір батальйона/полку:
    // підпис, підсвітка вже призначеного, доступність кнопки вибору.
    private void RefreshOfficerButtons()
    {
        if (officers == null)
            return;

        BattalionType currentType = BattalionType.none;
        bool hasTarget = false;

        if (batalionManager.selectBattalion != null)
        {
            currentType = batalionManager.selectBattalion.battalion.type;
            hasTarget = true;
        }
        else if (batalionManager.selectRegiment != null)
        {
            currentType = batalionManager.selectRegiment.battalionType;
            hasTarget = true;
        }

        for (int i = 0; i < officers.Length; i++)
        {
            OfiicerButton slot = officers[i];

            if (slot == null || slot.officer == null)
                continue;

            if (slot.Name != null)
                slot.Name.text = GetOfficerLabel(slot.officer);

            if (slot.selectOfficer != null)
            {
                bool matchesType = slot.officer.officetType == currentType;

                slot.selectOfficer.interactable = hasTarget && matchesType && !slot.officer.isSelect;

                Image selectImage = slot.selectOfficer.image;
                if (selectImage != null)
                    selectImage.color = slot.officer.isSelect ? Color.yellow : Color.white;
            }
        }
    }

    private string GetOfficerLabel(Officer officer)
    {
        return officer.name + " (" + GetRankLabel(officer.rank) + ")";
    }

    private string GetRankLabel(Rank rank)
    {
        switch (rank)
        {
            case Rank.Major:
                return "Майор";
            case Rank.LieutenantColonel:
                return "Підполковник";
            case Rank.Colonel:
                return "Полковник";
            case Rank.General:
                return "Генерал";
            default:
                return rank.ToString();
        }
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
            commandText.text = r.command + " +(" + r.planning + ")/" + r.commandMax;
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

        bool hasSelection = hasBattalionSelected || hasRegimentSelected;

        bool isNone = hasBattalionSelected && battalionScr.battalion.type == BattalionType.none;
        bool isArtillery = hasBattalionSelected && battalionScr.battalion.type == BattalionType.artillery;
        bool isDeployed = isArtillery && battalionScr.GetProjectedDeployedState(batalionManager.CommandDuty);

        noneButton.SetActive(hasSelection && !isNone);
        moveButton.SetActive(hasSelection && !isNone && !isDeployed);
        attackButton.SetActive(hasSelection && !isNone && !isDeployed);
        defendButton.SetActive(hasSelection && !isNone);

        deployButton.SetActive(hasBattalionSelected && isArtillery && !isDeployed);
        undeployButton.SetActive(hasBattalionSelected && isDeployed);
        rotateButton.SetActive(hasBattalionSelected && isDeployed);
        bombardButton.SetActive(hasBattalionSelected && isDeployed);

        if (reinforceButton != null)
        {
            bool canReinforce = hasBattalionSelected && !isNone && battalionScr.GetMissingPersonnel() > 0;
            reinforceButton.gameObject.SetActive(canReinforce);
        }

        if (officerSelect != null)
            officerSelect.gameObject.SetActive(hasSelection);

        if (officerPanel != null && officerPanel.activeSelf)
            RefreshOfficerButtons();

        RefreshRegimentButtons();
    }

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


[System.Serializable]
public class OfiicerButton
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