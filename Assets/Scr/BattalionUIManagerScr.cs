using UnityEngine;

public class BattalionUIManagerScr : MonoBehaviour
{

    public GameObject commandPanel;
    public GameObject noneButton, moveButton, attackButton, defendButton, createRegimentButton, addRegimentButton, removRegimentButton, destroyRegimentButton;
    public BatalionManagerScr batalionManager;
    public void CommandPanel(bool isActvie)
    {
        CheckButtalion();
        commandPanel.SetActive(isActvie);

    }
    private void CheckButtalion()
    {
        BattalionScr battalionScr = batalionManager.selectBattalion;

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

        // Початковий стан кнопок роботи з полком
        createRegimentButton.SetActive(true);
        removRegimentButton.SetActive(false);
        destroyRegimentButton.SetActive(false);
        addRegimentButton.SetActive(false);

        // Перевіряємо, чи батальйон вже знаходиться в якомусь полку
        for (int i = 0; i < batalionManager.regiment.Count; i++)
        {
            for (int j = 0; j < batalionManager.regiment[i].battalions.Count; j++)
            {
                if (batalionManager.regiment[i].battalions[j] == battalionScr)
                {
                    // Батальйон вже знаходиться в полку
                    createRegimentButton.SetActive(false);
                    removRegimentButton.SetActive(true);
                    destroyRegimentButton.SetActive(true);
                    addRegimentButton.SetActive(false);

                    return;
                }
            }
        }

        // Батальйон не знаходиться в жодному полку
        createRegimentButton.SetActive(true);
        removRegimentButton.SetActive(false);
        destroyRegimentButton.SetActive(false);
        addRegimentButton.SetActive(true);
    }
}
