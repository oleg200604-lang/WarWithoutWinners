using UnityEngine;

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


    // =========================================================
    // BONUSES
    // =========================================================

    public float GetTacticsBonusPercent()
    {
        return tacticsLv *
               TacticsPercentPerLevel *
               OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetAttackBonusPercent()
    {
        return attackLv *
               AttackPercentPerLevel *
               OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetDefenseBonusPercent()
    {
        return defenseLv *
               DefensePercentPerLevel *
               OfficerDataBaseScr.GetRankEfficiency(rank);
    }

    public float GetOrganizationBonusPercent()
    {
        return organizationLv *
               OrganizationPercentPerLevel *
               OfficerDataBaseScr.GetRankEfficiency(rank);
    }


    // =========================================================
    // RANK
    // =========================================================

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
                break;
        }
    }

    public void Lower()
    {
        switch (rank)
        {
            case Rank.Major:
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