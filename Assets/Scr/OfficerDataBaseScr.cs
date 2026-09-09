using NUnit.Framework;
using UnityEngine;

public class OfficerDataBaseScr : MonoBehaviour
{
    public Officer[] officers;

    // Ефективність пасивних бонусів вміння залежно від звання.
    // Майор — 100%, підполковник — 75%, полковник — 50%, генерал — 25%.
    public static float GetRankEfficiency(Rank rank)
    {
        switch (rank)
        {
            case Rank.Major:
                return 1f;
            case Rank.LieutenantColonel:
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

public class Officer
{
    public string name;
    public int tacticsLv, attackLv, defenseLv, organizationLv;
    public Features[] features;
    public Rank rank;
    public BattalionType officetType;
    public bool isSelect;
    private const float TacticsPercentPerLevel = 0.10f;
    private const float AttackPercentPerLevel = 0.25f;
    private const float DefensePercentPerLevel = 0.25f;
    private const float OrganizationPercentPerLevel = 0.10f;

    public float GetTacticsBonusPercent()
    {
        return tacticsLv * TacticsPercentPerLevel * OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetAttackBonusPercent()
    {
        return attackLv * AttackPercentPerLevel * OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetDefenseBonusPercent()
    {
        return defenseLv * DefensePercentPerLevel * OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetOrganizationBonusPercent()
    {
        return organizationLv * OrganizationPercentPerLevel * OfficerDataBaseScr.GetRankEfficiency(rank);
    }
}
public enum Features
{
    adaptability, charismatic, strict, cautious, risky, stubborn, sycophantic, ambitious, corrupt, superior, mutualRespect
}


public enum Rank
{
    Major, LieutenantColonel, Colonel, General
}