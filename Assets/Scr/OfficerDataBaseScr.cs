using NUnit.Framework;
using UnityEngine;

public class OfficerDataBaseScr : MonoBehaviour
{
    public Officer[] officers;
    

}

public class Officer 
{
    public string name;
    public int tacticsLv, attackLv, defenseLv, organizationLv;
    public Features[] features;
    public Rank rank;
    public BattalionType officetType;
}
public enum Features
{
    adaptability, charismatic, strict, cautious, risky, stubborn, sycophantic, ambitious, corrupt, superior, mutualRespect
}


public enum Rank
{
    Major, LieutenantColonel, Colonel, General
}
