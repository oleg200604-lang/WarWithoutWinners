using UnityEngine;

// Одна спільна "база даних" бонусів для всіх типів рот у грі.
// Налаштовується один раз в інспекторі на одному об'єкті сцени,
// всі батальйони користуються тим самим переліком.
public class CompanyDatabaseScr : MonoBehaviour
{
    public static CompanyDatabaseScr Instance { get; private set; }

    public CompanyDefinition[] companyDefinitions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool TryGetDefinition(CompanyType type, out CompanyDefinition definition)
    {
        if (companyDefinitions != null)
        {
            for (int i = 0; i < companyDefinitions.Length; i++)
            {
                if (companyDefinitions[i] != null &&
                    companyDefinitions[i].type == type)
                {
                    definition = companyDefinitions[i];
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }
}

[System.Serializable]
public class CompanyDefinition
{
    public CompanyType type;

    [Tooltip("Наскільки рота цього типу збільшує максимальну чисельність особового складу батальйону. Діє завжди, незалежно від умови нижче.")]
    public int personnelMaxBonus;

    [Tooltip("Коли діють бойові бонуси (statBonus) цієї роти.")]
    public CompanyBonusCondition condition = CompanyBonusCondition.Always;

    [Tooltip("Додаткові бонуси до бойових характеристик батальйону (додаються до базових значень), діють згідно з умовою condition.")]
    public Battalion statBonus;
}

// Коли саме застосовується бойовий бонус (statBonus) роти.
public enum CompanyBonusCondition
{
    Always,         // завжди
    AttackOnly,     // лише під час атаки/обстрілу
    DefendOnly,     // лише під час захисту
    AttackAndDefend // і під час атаки, і під час захисту (але не поза бойовою дією, якщо колись з'явиться така різниця)
}