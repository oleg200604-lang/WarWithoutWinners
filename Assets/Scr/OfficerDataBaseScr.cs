using NUnit.Framework;
using UnityEngine;

public class OfficerDataBaseScr : MonoBehaviour
{
    public Officer[] officers;

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
[System.Serializable]
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

    public void Raise()
    {
        switch (rank)
        {
            case Rank.Major:
                rank = Rank.LieutenantColonel;
                break;


            case Rank.LieutenantColonel:
                rank = Rank.Colonel;
                break;

            case Rank.Colonel:
                rank = Rank.General;
                break;

            case Rank.General:
                rank = Rank.General;
                break;
        }
    }

    public void Lower()
    {
        switch (rank)
        {
            case Rank.Major:
                rank = Rank.Major;
                break;

            case Rank.LieutenantColonel:
                rank = Rank.Major;
                break;

            case Rank.Colonel:
                rank = Rank.LieutenantColonel;
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
    Major, LieutenantColonel, Colonel, General
}