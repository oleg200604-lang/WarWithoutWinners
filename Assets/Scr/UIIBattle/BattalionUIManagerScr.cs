using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattalionUIManagerScr : MonoBehaviour
{
    public GameObject commandPanel;
    public GameObject officerPanel;

    public GameObject noneButton;
    public GameObject moveButton;
    public GameObject attackButton;
    public GameObject defendButton;

    [Header("Артилерія: розкладка / обстріл")]
    public GameObject deployButton;
    public GameObject undeployButton;
    public GameObject rotateButton;
    public GameObject bombardButton;

    [Header("Людський ресурс")]
    public Button reinforceButton;

    [Tooltip("Скільки особового складу додається за одне натискання кнопки поповнення.")]
    public int reinforceAmountPerClick = 10;

    [Header("HUD глобальних ресурсів")]
    public TextMeshProUGUI personnelText;
    public TextMeshProUGUI suppliesText;
    public TextMeshProUGUI commandText;

    [Header("Полки")]
    public List<RegimentButtonGroup> regimentButtonGroups;

    public BatalionManagerScr batalionManager;

    [Header("UI-слоти офіцерів (фіксовані, прив'язуються в інспекторі)")]
    public OfiicerButtonScr[] officers;

    [Tooltip("Кнопка, яка відкриває/закриває панель вибору офіцера.")]
    public Button officerSelect;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        SetupOfficerSlots();

        if (reinforceButton != null)
        {
            reinforceButton.onClick.AddListener(() =>
            {
                if (batalionManager.selectBattalion != null)
                {
                    batalionManager.ReinforceBattalion(
                        batalionManager.selectBattalion,
                        reinforceAmountPerClick
                    );

                    CheckButtalion();
                }
            });
        }

        if (officerSelect != null)
        {
            officerSelect.onClick.AddListener(
                SelectOfficerPanel
            );
        }
    }


    private void Update()
    {
        RefreshResourceHud();
    }


    // =========================================================
    // OFFICER SETUP
    // =========================================================

    private void SetupOfficerSlots()
    {
        if (officers == null)
            return;

        // Фіксовані слоти прив'язані вручну в інспекторі — кількість тут МАЄ
        // збігатись з кількістю офіцерів у OfficerDataBaseScr (єдине джерело
        // даних, batalionManager.OfficerCount). Якщо ні — попереджаємо одразу,
        // а не даємо цьому мовчки розʼїхатись у порожні/биті кнопки.
        if (batalionManager != null &&
            officers.Length != batalionManager.OfficerCount)
        {
            Debug.LogWarning(
                "BattalionUIManagerScr: кількість UI-слотів офіцерів (" +
                officers.Length +
                ") не збігається з кількістю в OfficerDataBaseScr (" +
                batalionManager.OfficerCount +
                "). Додай/прибери слоти в інспекторі."
            );
        }

        for (int i = 0; i < officers.Length; i++)
        {
            OfiicerButtonScr slot = officers[i];

            if (slot == null)
                continue;

            int index = i;

            slot.SetOfficerIndex(index);

            // Слот без відповідного офіцера в базі — ховаємо, а не лишаємо
            // "живим" з порожнім/некоректним станом.
            bool hasOfficer = GetOfficer(index) != null;

            slot.gameObject.SetActive(hasOfficer);

            if (!hasOfficer)
                continue;

            if (slot.selectOfficer != null)
            {
                slot.selectOfficer.onClick.RemoveAllListeners();

                slot.selectOfficer.onClick.AddListener(
                    () => SelectOfficer(index)
                );
            }

            if (slot.buttonRaise != null)
            {
                slot.buttonRaise.onClick.RemoveAllListeners();

                slot.buttonRaise.onClick.AddListener(
                    () => RaiseOfficer(index)
                );
            }

            if (slot.buttonLower != null)
            {
                slot.buttonLower.onClick.RemoveAllListeners();

                slot.buttonLower.onClick.AddListener(
                    () => LowerOfficer(index)
                );
            }
        }
    }


    // =========================================================
    // OFFICER DATABASE
    // =========================================================
    // Єдиний шлях до бази: UI -> BatalionManagerScr -> OfficerDataBaseScr.
    // Сам BattalionUIManagerScr бази не тримає, щоб не було двох паралельних
    // посилань на одні й ті самі дані.

    public Officer GetOfficer(int index)
    {
        if (batalionManager == null)
            return null;

        return batalionManager.GetOfficer(index);
    }


    // =========================================================
    // SELECT OFFICER
    // =========================================================

    public void SelectOfficer(int officerIndex)
    {
        if (batalionManager == null)
            return;

        Officer officer =
            GetOfficer(officerIndex);

        if (officer == null)
            return;

        bool success = false;

        if (batalionManager.selectBattalion != null)
        {
            success =
                batalionManager.AssignOfficerToBattalion(
                    officer,
                    batalionManager.selectBattalion
                );
        }
        else if (batalionManager.selectRegiment != null)
        {
            success =
                batalionManager.AssignOfficerToRegiment(
                    officer,
                    batalionManager.selectRegiment
                );
        }

        if (!success)
            return;

        RefreshOfficerButtons();
        CheckButtalion();
    }


    // =========================================================
    // RANK
    // =========================================================

    private void RaiseOfficer(int officerIndex)
    {
        Officer officer =
            GetOfficer(officerIndex);

        if (officer == null)
            return;

        officer.Raise();

        batalionManager
            ?.RecalculateStatsForOfficer(officer);

        RefreshOfficerButtons();
        CheckButtalion();
    }


    private void LowerOfficer(int officerIndex)
    {
        Officer officer =
            GetOfficer(officerIndex);

        if (officer == null)
            return;

        officer.Lower();

        batalionManager
            ?.RecalculateStatsForOfficer(officer);

        RefreshOfficerButtons();
        CheckButtalion();
    }


    // =========================================================
    // OFFICER PANEL
    // =========================================================

    public void SelectOfficerPanel()
    {
        if (officerPanel == null)
            return;

        bool willBeActive =
            !officerPanel.activeSelf;

        officerPanel.SetActive(
            willBeActive
        );

        if (willBeActive)
            RefreshOfficerButtons();
    }


    public void RefreshOfficerButtons()
    {
        if (officers == null)
            return;

        BattalionType currentType =
            BattalionType.none;

        bool hasTarget = false;

        if (batalionManager == null)
            return;

        if (batalionManager.selectBattalion != null)
        {
            BattalionScr battalion =
                batalionManager.selectBattalion;

            if (battalion.battalion != null)
            {
                currentType =
                    battalion.battalion.type;

                hasTarget = true;
            }
        }
        else if (batalionManager.selectRegiment != null)
        {
            currentType =
                batalionManager.selectRegiment.battalionType;

            hasTarget = true;
        }


        for (int i = 0; i < officers.Length; i++)
        {
            OfiicerButtonScr slot =
                officers[i];

            if (slot == null)
                continue;

            Officer officer =
                GetOfficer(i);

            if (officer == null)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            slot.SetOfficerIndex(i);

            if (slot.Name != null)
            {
                slot.Name.text =
                    GetOfficerLabel(officer);
            }

            bool matchesType =
                officer.officetType ==
                currentType;

            bool assigned =
                batalionManager.IsOfficerAssigned(
                    officer
                );

            bool canAssign =
                hasTarget &&
                matchesType &&
                batalionManager.CanAssignOfficerToCurrentSelection(
                    officer
                );

            slot.SetInteractable(
                canAssign
            );

            slot.SetSelectedVisual(
                assigned
            );
        }
    }


    // =========================================================
    // OFFICER LABEL
    // =========================================================

    private string GetOfficerLabel(
        Officer officer)
    {
        if (officer == null)
            return "";

        return   GetRankLabel(officer.rank)+ " " + officer.name;
    }


    private string GetRankLabel(
        Rank rank)
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


    // =========================================================
    // RESOURCES
    // =========================================================

    private void RefreshResourceHud()
    {
        if (batalionManager == null)
            return;

        Ressurs r =
            batalionManager.ressurs;

        if (personnelText != null)
            personnelText.text =
                r.personnel.ToString();

        if (suppliesText != null)
            suppliesText.text =
                r.supplies.ToString();

        if (commandText != null)
        {
            commandText.text =
                r.command +
                " +(" +
                r.planning +
                ")/" +
                r.commandMax;
        }
    }


    // =========================================================
    // COMMAND PANEL
    // =========================================================

    public void CommandPanel(
        bool isActvie)
    {
        if (isActvie)
            CheckButtalion();

        if (commandPanel != null)
            commandPanel.SetActive(
                isActvie
            );
    }


    public void CheckButtalion()
    {
        if (batalionManager == null)
            return;

        BattalionScr battalionScr =
            batalionManager.selectBattalion;

        Regiment regiment =
            batalionManager.selectRegiment;

        bool hasBattalionSelected =
            battalionScr != null;

        bool hasRegimentSelected =
            regiment != null;

        bool hasSelection =
            hasBattalionSelected ||
            hasRegimentSelected;

        bool isNone =
            hasBattalionSelected &&
            battalionScr.battalion.type ==
            BattalionType.none;

        bool isArtillery =
            hasBattalionSelected &&
            battalionScr.battalion.type ==
            BattalionType.artillery;

        bool isDeployed =
            isArtillery &&
            battalionScr.GetProjectedDeployedState(
                batalionManager.CommandDuty
            );


        noneButton.SetActive(
            hasSelection && !isNone
        );

        moveButton.SetActive(
            hasSelection &&
            !isNone &&
            !isDeployed
        );

        attackButton.SetActive(
            hasSelection &&
            !isNone &&
            !isDeployed
        );

        defendButton.SetActive(
            hasSelection &&
            !isNone
        );


        deployButton.SetActive(
            hasBattalionSelected &&
            isArtillery &&
            !isDeployed
        );

        undeployButton.SetActive(
            hasBattalionSelected &&
            isDeployed
        );

        rotateButton.SetActive(
            hasBattalionSelected &&
            isDeployed
        );

        bombardButton.SetActive(
            hasBattalionSelected &&
            isDeployed
        );


        if (reinforceButton != null)
        {
            bool canReinforce =
                hasBattalionSelected &&
                !isNone &&
                battalionScr.GetMissingPersonnel() > 0;

            reinforceButton.gameObject.SetActive(
                canReinforce
            );
        }


        if (officerSelect != null)
        {
            officerSelect.gameObject.SetActive(
                hasSelection
            );
        }

        if (officerPanel != null &&
            officerPanel.activeSelf)
        {
            RefreshOfficerButtons();
        }

        RefreshRegimentButtons();
    }


    // =========================================================
    // REGIMENT BUTTONS
    // =========================================================

    public void RefreshRegimentButtons()
    {
        if (batalionManager == null ||
            regimentButtonGroups == null)
        {
            return;
        }

        BattalionScr selected =
            batalionManager.selectBattalion;

        for (int i = 0;
             i < regimentButtonGroups.Count;
             i++)
        {
            RegimentButtonGroup group =
                regimentButtonGroups[i];

            if (group == null ||
                group.root == null)
            {
                continue;
            }

            bool hasRegiment =
                i < batalionManager.regiments.Count;

            group.root.SetActive(
                hasRegiment
            );

            if (!hasRegiment)
                continue;

            Regiment regiment =
                batalionManager.regiments[i];


            if (group.selectButton != null)
            {
                group.selectButton.onClick
                    .RemoveAllListeners();

                group.selectButton.onClick.AddListener(
                    () =>
                    {
                        batalionManager
                            .SelectRegimentUnit(
                                regiment
                            );

                        RefreshRegimentButtons();
                    }
                );

                Image selectImage =
                    group.selectButton.image;

                if (selectImage != null)
                {
                    selectImage.color =
                        batalionManager.selectRegiment ==
                        regiment
                            ? Color.yellow
                            : Color.white;
                }
            }


            if (group.addButton != null)
            {
                group.addButton.onClick
                    .RemoveAllListeners();

                group.addButton.onClick.AddListener(
                    () =>
                    {
                        batalionManager.AddRegiment(
                            batalionManager.selectBattalion,
                            regiment
                        );

                        RefreshRegimentButtons();
                    }
                );

                group.addButton.interactable =
                    selected != null;
            }


            if (group.removeButton != null)
            {
                group.removeButton.onClick
                    .RemoveAllListeners();

                group.removeButton.onClick.AddListener(
                    () =>
                    {
                        batalionManager.RemovRegiment(
                            batalionManager.selectBattalion,
                            regiment
                        );

                        RefreshRegimentButtons();
                    }
                );

                bool canRemove =
                    selected != null &&
                    selected.regimentredID == i;

                group.removeButton.interactable =
                    canRemove;
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