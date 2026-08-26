using UnityEngine;

public class NextMobeButtonScr : MonoBehaviour
{
    public BatalionManagerScr batalionManager;
    private void OnMouseDown()
    {
        batalionManager.NextMove();
    }
}
