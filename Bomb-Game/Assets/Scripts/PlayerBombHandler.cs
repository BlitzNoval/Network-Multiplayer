using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBombHandler : MonoBehaviour
{
    private Bomb currentBomb; // Reference to the bomb this player is holding

    public void SetBomb(Bomb bomb)
    {
        currentBomb = bomb;
    }

    public void ClearBomb()
    {
        currentBomb = null;
    }

    public void OnSwapBomb(bool isPressed)
    {
        if (isPressed && currentBomb != null)
        {
            currentBomb.SwapHoldPoint();
        }
    }

   public void OnThrow(bool isPressed)
    {
        if (isPressed && currentBomb != null)
        {
            currentBomb.ThrowBomb();
        }
    }
}