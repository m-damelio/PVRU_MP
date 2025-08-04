using UnityEngine;
using Fusion;
using TMPro;

public class HackDevice : NetworkBehaviour, ILevelResettable
{
    [Header("Activation Button")]
    [SerializeField] private NetworkedButton activationButton;

    [Header("Laser Control")]
    [SerializeField] private GameObject laserObject;

    [Header("Combination Settings")]
    [SerializeField] private int[] correctCombination = new int[4] { 1, 2, 3, 4 };
    [Networked, Capacity(4)] public NetworkArray<int> currentCombination => default;

    [Header("UI References")]
    [SerializeField] private TextMeshPro[] digitDisplays;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.cyan;

    [Header("State")]
    [Networked] public bool IsUnlocked { get; set; }
    [Networked] public bool IsHackActive { get; set; }
    private int selectedIndex = -1;

    public override void Spawned()
    {
        for (int i = 0; i < 4; i++)
        {
            currentCombination.Set(i, 0);
        }

        if (activationButton != null)
        {
        }

        InitializeDigitDisplays();
        UpdateVisuals();
    }

    public void SetInitialState()
    {
        //Initial state is always the sa,e
    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            for (int i = 0; i < currentCombination.Length; i++)
            {
                currentCombination.Set(i, 0);
            }
        }
        IsUnlocked = false;
        IsHackActive = false;
        selectedIndex = -1; // Reset local state

        // The laser should be active again if it was deactivated
        if (laserObject != null && !laserObject.activeSelf)
        {
            laserObject.SetActive(true);
        }

        RPC_UpdateVisuals();
    }
    private void InitializeDigitDisplays()
    {
        for (int i = 0; i < digitDisplays.Length; i++)
        {
            if (digitDisplays[i] != null)
            {
                digitDisplays[i].text = "0";
                digitDisplays[i].color = normalColor;
            }
        }
    }

    public void ActivateHack()
    {
        if (Object.HasStateAuthority)
        {
            IsHackActive = true;
            selectedIndex = 0;
            Debug.Log($"{name}: Hack aktiviert! Slot {selectedIndex} ausgewählt.");
            RPC_UpdateVisuals();
        }
    }

    public void SelectSlot(int index)
    {
        if (!IsHackActive || IsUnlocked) return;
        selectedIndex = Mathf.Clamp(index, 0, 3);
        Debug.Log($"{name}: Slot {selectedIndex} ausgewählt");
        UpdateVisuals();
    }

    public void AdjustActiveSlot(int direction)
    {
        if (!IsHackActive || IsUnlocked) return;
        RPC_AdjustSlot(selectedIndex, direction);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AdjustSlot(int slotIndex, int direction)
    {
        int value = currentCombination.Get(slotIndex);
        value = (value + direction + 10) % 10;
        currentCombination.Set(slotIndex, value);

        CheckCombination();
        RPC_UpdateVisuals();
    }

    private void CheckCombination()
    {
        for (int i = 0; i < 4; i++)
        {
            if (currentCombination.Get(i) != correctCombination[i])
                return;
        }

        
        IsUnlocked = true;
        IsHackActive = false;
        selectedIndex = -1;
        Debug.Log($"{name}: Richtige Kombination! Hack beendet.");

        RPC_DeactivateLaser();
        RPC_UpdateVisuals();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateVisuals()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < digitDisplays.Length; i++)
        {
            if (digitDisplays[i] != null)
            {
                string displayText = currentCombination.Get(i).ToString();
                bool isActiveSlot = IsHackActive && (i == selectedIndex);

                digitDisplays[i].text = isActiveSlot ? $"[{displayText}]" : displayText;
                digitDisplays[i].color = isActiveSlot ? activeColor : normalColor;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DeactivateLaser()
    {
        if (laserObject != null)
        {
            laserObject.SetActive(false);
            Debug.Log($"{name}: deactivate mirror to activate laser");
        }
    }


    // --------------------------
    // DEBUGGING METHODS
    // --------------------------

    [ContextMenu("Select Slot 0")]
    private void Debug_SelectSlot0() => SelectSlot(0);

    [ContextMenu("Select Slot 1")]
    private void Debug_SelectSlot1() => SelectSlot(1);

    [ContextMenu("Select Slot 2")]
    private void Debug_SelectSlot2() => SelectSlot(2);

    [ContextMenu("Select Slot 3")]
    private void Debug_SelectSlot3() => SelectSlot(3);

    [ContextMenu("Increment Active Slot")]
    private void Debug_IncrementSlot() => AdjustActiveSlot(+1);

    [ContextMenu("Decrement Active Slot")]
    private void Debug_DecrementSlot() => AdjustActiveSlot(-1);

    [ContextMenu("Activate Hack (Debug)")]
    private void Debug_ActivateHack() => ActivateHack();

    [ContextMenu("Reset HackDevice (Debug)")]
    private void Debug_ResetHackDevice()
    {
        if (Object.HasStateAuthority)
        {
            for (int i = 0; i < 4; i++) currentCombination.Set(i, 0);
            IsHackActive = false;
            IsUnlocked = false;
            selectedIndex = -1;
            Debug.Log($"{name}: HackDevice zurückgesetzt.");
            RPC_UpdateVisuals();
        }
    }
}
