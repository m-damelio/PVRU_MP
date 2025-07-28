using UnityEngine;
using Fusion;

public class NetworkedButton : NetworkBehaviour
{   
    //Visuals of the actual button
    [Header("Button Visual Settings")]
    [SerializeField] private Color mainColor;
    [SerializeField] private Color pressedColor;
    [SerializeField] private bool initialPressState;
    [SerializeField] private Renderer buttonVisual;

    //Visuals of an overlay above the actual button which is supposed to be somewhat transparent and only be visible by either one or none
    [Header("Extra Button Visual Settings")]
    [SerializeField] private Renderer extraButtonVisual;
    [SerializeField] private Material extraButtonMaterial;
    [SerializeField] private bool hackerOnly;
    [SerializeField] private bool sneakerOnly;
    [SerializeField][Layer] private int sneakerOnlyLayer;
    [SerializeField][Layer] private int hackerOnlyLayer;

    [Header("Solvable Interface Settings")]
    [SerializeField] private bool isFinalStep;

    [Header("Networked Properties")]
    [Networked] public bool IsPressed {get; set;}

    private Material buttonMaterial;

    private void Awake()
    {
        if(buttonVisual != null)
        {
            buttonMaterial = buttonVisual.material;
            buttonMaterial.color = mainColor;
        }
        if(extraButtonVisual == null) return;

        if(hackerOnly && sneakerOnly || !hackerOnly && !sneakerOnly)
        {
            extraButtonVisual.gameObject.SetActive(false);
        }
        else if (!hackerOnly && sneakerOnly)
        {
            extraButtonVisual.gameObject.layer = sneakerOnlyLayer;
            extraButtonVisual.material = extraButtonMaterial;
        }
        else
        {
            extraButtonVisual.gameObject.layer = hackerOnlyLayer;
            extraButtonVisual.material = extraButtonMaterial;
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = initialPressState;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPress()
    {
        if(IsPressed)
        {
            buttonMaterial.color = pressedColor;
        }
        else
        {
            buttonMaterial.color = mainColor;
        }
    }

    //called from interaction script
    public void TriggerPress()
    {
        if(buttonMaterial == null) return;
        RPC_RequestPress();
    }

    [ContextMenu("Test Trigger Press")]
    public void DebugTriggerPress()
    {
        TriggerPress();
    }
}
