using UnityEngine;
using Fusion;

public class NetworkedButton : NetworkBehaviour, ISolvable, ILevelResettable
{   
    //Visuals of the actual button
    [Header("Button Visual Settings")]
    [SerializeField] private Color mainColor;
    [SerializeField] private Color pressedColor;
    [SerializeField] private bool initialPressState;
    [SerializeField] private Renderer buttonTop;
    [SerializeField] private Renderer buttonBottom;
    

    //Visuals of an overlay above the actual button which is supposed to be somewhat transparent and only be visible by either one or none
    [Header("Overlay Visual Settings")]
    [SerializeField] private Renderer buttonBottomOverlay;
    [SerializeField] private Material extraMaterial;
    [SerializeField] private bool hackerOnly;
    [SerializeField] private bool sneakerOnly;
    [SerializeField][Layer] private int sneakerOnlyLayer;
    [SerializeField][Layer] private int hackerOnlyLayer;

    [Header("Networked Properties")]
    [Networked] public bool IsPressed {get; set;}
    [Networked] public bool IsSolved { get; set; }

    public System.Action<ISolvable> OnSolved { get; set; }
    private bool wasPreviouslySolved = false;
    private Material buttonTopMaterial;

    private void Awake()
    {
        if (buttonTop != null)
        {
            buttonTopMaterial = buttonTop.material;
            buttonTopMaterial.color = mainColor;
        }
        if (buttonBottomOverlay == null) return;

        if (hackerOnly && sneakerOnly || !hackerOnly && !sneakerOnly)
        {
            buttonBottomOverlay.gameObject.SetActive(false);
        }
        else if (!hackerOnly && sneakerOnly)
        {
            buttonBottomOverlay.gameObject.layer = sneakerOnlyLayer;
            buttonBottomOverlay.material = extraMaterial;
        }
        else
        {
            buttonBottomOverlay.gameObject.layer = hackerOnlyLayer;
            buttonBottomOverlay.material = extraMaterial;
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SetInitialState();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            CheckSolution();
        }
    }

    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = initialPressState;
            IsSolved = false;
        }
        
    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = initialPressState;
            IsSolved = false;
        }
        
    }

    public void CheckSolution()
    {
        bool currentlySolved = IsSolved;
        if (currentlySolved && !wasPreviouslySolved)
        {
            wasPreviouslySolved = true;
            OnSolved?.Invoke(this);
        }
    }

    public bool IsPuzzleSolved()
    {
        return IsSolved;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPress()
    {
        if(IsPressed)
        {
            buttonTopMaterial.color = pressedColor;
        }
        else
        {
            buttonTopMaterial.color = mainColor;
        }
    }

    //called from interaction script
    public void TriggerPress()
    {
        if(buttonTopMaterial == null) return;
        RPC_RequestPress();
        
        if (NetworkedSoundManager.Instance != null)
        {
            NetworkedSoundManager.Instance.PlayEnvironmentSound("Keycard_Grabbed", transform.position);
        }
    }

    [ContextMenu("Test Trigger Press")]
    public void DebugTriggerPress()
    {
        TriggerPress();
    }
}
