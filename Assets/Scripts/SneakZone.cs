using UnityEngine;
using Fusion;
public class SneakZone : NetworkBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        var nb = other.GetComponent<NetworkObject>();
        if (nb == null)
        {
            return;
        }

        var player = other.GetComponentInParent<VRPlayer>();
        if (player == null)
        {
            return;
        }
        

        if (player.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedSneaking)
        {
            Debug.Log("Networked sneakable player detected.");
            if (player.NetworkedPlayerState == VRPlayer.PlayerState.Sneaking)
            {
                Debug.Log("Is Sneaking");
            }
            else
            {
                Debug.Log("Is Walking");
            }
        }
    }
}
