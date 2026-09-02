using UnityEngine;

public class BarScr : MonoBehaviour
{
    [SerializeField] private Transform organizationBar;


    [SerializeField] private Transform combatCapableBar;
    [SerializeField] private Transform combatCapableNoBar;

    //[SerializeField] private Transform ammoBar;

    public void SetOrganization(float current, float max)
    {

        float normalized = Mathf.Clamp01(current / max);

        Vector3 scale = organizationBar.localScale;
        organizationBar.localScale = new Vector3(1f, normalized, 1f);
    }

    public void SetcombatCapable(float current, float current1, float max)
    {
        float normalized = Mathf.Clamp01(current / max);
        float normalized1 = Mathf.Clamp01((current + current1) / max);

        Vector3 scale = combatCapableBar.localScale;
        combatCapableBar.localScale = new Vector3(1f, normalized, 1f);
        Vector3 scale1 = combatCapableNoBar.localScale;
        combatCapableNoBar.localScale = new Vector3(1f, normalized1, 1f);
    }

    public void SetAmmo(float current, float max)
    {
        //if (ammoBar == null)
          //  return;

        float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        //ammoBar.localScale = new Vector3(1f, normalized, 1f);
    }
}