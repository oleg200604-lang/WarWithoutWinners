using UnityEngine;
using UnityEngine.UI;

public class OfficerDataBaseScr : MonoBehaviour
{
    [Header("Усі офіцери цієї армії")]
    public Officer[] officers;

    public int OfficerCount
    {
        get
        {
            return officers != null
                ? officers.Length
                : 0;
        }
    }

    public Officer GetOfficer(int index)
    {
        if (officers == null)
            return null;

        if (index < 0 || index >= officers.Length)
            return null;

        return officers[index];
    }

    public static float GetRankEfficiency(Rank rank)
    {
        switch (rank)
        {
            case Rank.Major:
                return 1f;

            case Rank.Lieutenant:
                return 0.75f;

            case Rank.Colonel:
                return 0.5f;

            case Rank.General:
                return 0.25f;

            default:
                return 0f;
        }
    }
}

[System.Serializable]
public class Officer
{
    public string name;
    public Sprite photo;
    [Header("Навички")]
    public int tacticsLv;
    public int attackLv;
    public int defenseLv;
    public int organizationLv;
    public bool isSelect;

    [Header("Особливості")]
    public Features[] features;

    [Header("Командування")]
    public Rank rank;
    public BattalionType officetType;

    private const float TacticsPercentPerLevel = 0.10f;
    private const float AttackPercentPerLevel = 0.25f;
    private const float DefensePercentPerLevel = 0.25f;
    private const float OrganizationPercentPerLevel = 0.10f;

    // Для sycophantic/superior (пільгова вартість наказу з дробовим коефіцієнтом,
    // напр. x0.5) чергуємо "закруглення вгору"/"закруглення вниз", щоб довгий
    // рахунок був точним: 1, 0, 1, 0, ... замість завжди 0 або завжди 1.
    [System.NonSerialized] private bool commandCostChargeHighNext = true;


    // =========================================================
    // FEATURES
    // =========================================================

    public bool HasFeature(Features feature)
    {
        if (features == null)
            return false;

        for (int i = 0; i < features.Length; i++)
        {
            if (features[i] == feature)
                return true;
        }

        return false;
    }

    // stubborn (x2) та sycophantic (x0.5) впливають однаково на ВСІ здібності
    // офіцера (тактика/атака/захист/організація), тому винесено окремо.
    private float GetAbilityMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.stubborn))
            multiplier *= 2f;

        if (HasFeature(Features.sycophantic))
            multiplier *= 0.5f;

        return multiplier;
    }

    // risky/cautious перекидають ефективність між атакою та захистом.
    private float GetAttackFeatureMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.risky))
            multiplier *= 1.5f;

        if (HasFeature(Features.cautious))
            multiplier /= 1.5f;

        return multiplier;
    }

    private float GetDefenseFeatureMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.cautious))
            multiplier *= 1.5f;

        if (HasFeature(Features.risky))
            multiplier /= 1.5f;

        return multiplier;
    }

    // ambitious пом'якшує штраф ефективності від звання: 100/75/50/25 -> 100/80/60/40.
    private float GetEffectiveRankEfficiency()
    {
        if (!HasFeature(Features.ambitious))
            return OfficerDataBaseScr.GetRankEfficiency(rank);

        switch (rank)
        {
            case Rank.Major:
                return 1f;

            case Rank.Lieutenant:
                return 0.8f;

            case Rank.Colonel:
                return 0.6f;

            case Rank.General:
                return 0.4f;

            default:
                return 0f;
        }
    }

    // charismatic (x2) / strict (x0.5) — швидкість регенерації організації.
    public float GetOrganizationRegenMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.charismatic))
            multiplier *= 2f;

        if (HasFeature(Features.strict))
            multiplier *= 0.5f;

        return multiplier;
    }

    // superior (x0.75) / mutualRespect (x1.25) — пряма зміна максимальної організації,
    // незалежно від рівня вміння "Організація".
    public float GetOrganizationMaxMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.superior))
            multiplier *= 0.75f;

        if (HasFeature(Features.mutualRespect))
            multiplier *= 1.25f;

        return multiplier;
    }

    // stubborn (x2) / sycophantic (x0.5) / superior (x0.5) / mutualRespect (x2) —
    // вартість наказу для батальйону, яким командує цей офіцер.
    public float GetCommandCostMultiplier()
    {
        float multiplier = 1f;

        if (HasFeature(Features.stubborn))
            multiplier *= 2f;

        if (HasFeature(Features.sycophantic))
            multiplier *= 0.5f;

        if (HasFeature(Features.superior))
            multiplier *= 0.5f;

        if (HasFeature(Features.mutualRespect))
            multiplier *= 2f;

        return multiplier;
    }

    // Дивиться, скільки коштуватиме НАСТУПНИЙ наказ, не змінюючи чергування округлення.
    // Використовувати для перевірки "чи вистачає ресурсу".
    public int PeekCommandCost(int baseCost)
    {
        int lower, upper;
        GetCommandCostBounds(baseCost, out lower, out upper);

        if (lower == upper)
            return lower;

        return commandCostChargeHighNext ? upper : lower;
    }

    // Фактично "витрачає" наказ і просуває чергування округлення далі.
    // Викликати РІВНО ОДИН РАЗ на кожен реально відданий наказ.
    public int ConsumeCommandCost(int baseCost)
    {
        int lower, upper;
        GetCommandCostBounds(baseCost, out lower, out upper);

        if (lower == upper)
            return lower;

        int charge = commandCostChargeHighNext ? upper : lower;
        commandCostChargeHighNext = !commandCostChargeHighNext;
        return charge;
    }

    private void GetCommandCostBounds(int baseCost, out int lower, out int upper)
    {
        float exact = baseCost * GetCommandCostMultiplier();
        lower = Mathf.FloorToInt(exact);
        upper = Mathf.CeilToInt(exact);
    }


    // =========================================================
    // BONUSES
    // =========================================================

    public float GetTacticsBonusPercent()
    {
        return tacticsLv *
               TacticsPercentPerLevel *
               GetAbilityMultiplier() *
               GetEffectiveRankEfficiency();
    }

    public float GetAttackBonusPercent()
    {
        return attackLv *
               AttackPercentPerLevel *
               GetAttackFeatureMultiplier() *
               GetAbilityMultiplier() *
               GetEffectiveRankEfficiency();
    }

    public float GetDefenseBonusPercent()
    {
        return defenseLv *
               DefensePercentPerLevel *
               GetDefenseFeatureMultiplier() *
               GetAbilityMultiplier() *
               GetEffectiveRankEfficiency();
    }

    public float GetOrganizationBonusPercent()
    {
        return organizationLv *
               OrganizationPercentPerLevel *
               (HasFeature(Features.strict) ? 2f : 1f) *
               GetAbilityMultiplier() *
               GetEffectiveRankEfficiency();
    }


    // =========================================================
    // RANK
    // =========================================================

    public void Raise()
    {
        switch (rank)
        {
            case Rank.Major:
                rank = Rank.Lieutenant;
                break;

            case Rank.Lieutenant:
                rank = Rank.Colonel;
                break;

            case Rank.Colonel:
                rank = Rank.General;
                break;

            case Rank.General:
                break;
        }
    }

    public void Lower()
    {
        switch (rank)
        {
            case Rank.Major:
                break;

            case Rank.Lieutenant:
                rank = Rank.Major;
                break;

            case Rank.Colonel:
                rank = Rank.Lieutenant;
                break;

            case Rank.General:
                rank = Rank.Colonel;
                break;
        }
    }
}
public enum Features
{
    adaptability, charismatic, strict, cautious, risky, stubborn, sycophantic, ambitious, corrupt, superior, mutualRespect
}


public enum Rank
{
    Major, Lieutenant, Colonel, General
}