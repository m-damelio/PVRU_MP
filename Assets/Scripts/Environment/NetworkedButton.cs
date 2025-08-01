using UnityEngine;
using UnityEngine.Events;
using Fusion;

public class NetworkedButton : NetworkBehaviour, ISolvable, ILevelResettable
{
    [Header("Events")]
    [SerializeField] private UnityEvent OnButtonPressed;
    [SerializeField] private UnityEvent OnButtonReleased;
    [SerializeField] private UnityEvent OnButtonSolved;
    [SerializeField] private UnityEvent OnButtonReset;
    [Header("Event Settings")]
    [SerializeField] private bool onlyTriggerEventsOnAuthority = true;
    [SerializeField] private bool triggerEventsOnAllClients = false;

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
    [Networked] public bool IsPressed { get; set; }
    [Networked] public bool IsSolved { get; set; }
    [Networked] private bool PreviousPressed { get; set; }
    [Networked] private bool PreviousSolved { get; set; }

    public System.Action<ISolvable> OnSolved { get; set; }
    private bool wasPreviouslySolved = false;
    private Material buttonTopMaterial;

    private ChangeDetector _changeDetector;

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
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            SetInitialState();
        }
    }

    public override void Render()
    {
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsPressed))
            {
                RPC_PressChanged();
                HandlePressStateChanged();
            }
            if (changedProperty == nameof(IsSolved))
            {
                CheckSolution();
                HandlePressStateChanged();
            }
        }
    }

    private void HandlePressStateChanged()
    {
        bool shouldTriggerEvents = !onlyTriggerEventsOnAuthority || Object.HasStateAuthority;

        if (!shouldTriggerEvents && !triggerEventsOnAllClients) return;

        if (triggerEventsOnAllClients)
        {
            // Use RPC to ensure all clients receive the event
            if (Object.HasStateAuthority)
            {
                if (IsPressed && !PreviousPressed)
                {
                    RPC_TriggerPressedEvent();
                }
                else if (!IsPressed && PreviousPressed)
                {
                    RPC_TriggerReleasedEvent();
                }
                PreviousPressed = IsPressed;
            }
        }
        else
        {
            // Local event triggering
            if (IsPressed && !PreviousPressed)
            {
                OnButtonPressed?.Invoke();
            }
            else if (!IsPressed && PreviousPressed)
            {
                OnButtonReleased?.Invoke();
            }
            PreviousPressed = IsPressed;
        }
    }

    private void HandleSolvedStateChange()
    {
        bool shouldTriggerEvents = !onlyTriggerEventsOnAuthority || Object.HasStateAuthority;

        if (!shouldTriggerEvents && !triggerEventsOnAllClients) return;

        if (triggerEventsOnAllClients)
        {
            if (Object.HasStateAuthority && IsSolved && !PreviousSolved)
            {
                RPC_TriggerSolvedEvent();
                PreviousSolved = IsSolved;
            }
        }
        else
        {
            if (IsSolved && !PreviousSolved)
            {
                OnButtonSolved?.Invoke();
                PreviousSolved = IsSolved;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerPressedEvent()
    {
        OnButtonPressed?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerReleasedEvent()
    {
        OnButtonReleased?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerSolvedEvent()
    {
        OnButtonSolved?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerResetEvent()
    {
        OnButtonReset?.Invoke();
    }



    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = initialPressState;
            IsSolved = false;
            PreviousPressed = initialPressState;
            PreviousSolved = false;
        }

    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = initialPressState;
            IsSolved = false;
            PreviousPressed = initialPressState;
            PreviousSolved = false;

            if (triggerEventsOnAllClients)
            {
                RPC_TriggerResetEvent();
            }
            else
            {
                OnButtonReset?.Invoke();
            }
        }

    }

    public void CheckSolution()
    {
        if (!Object.HasStateAuthority) return;
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
    public void SetSolved()
    {
        IsSolved = true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PressChanged()
    {
        ChangeColor();
    }


    private void ChangeColor()
    {
        if (buttonTopMaterial == null) return;
        if (IsPressed)
        {
            buttonTopMaterial.color = pressedColor;
            IsSolved = true;
        }
        else
        {
            buttonTopMaterial.color = mainColor;
        }
    }

    private void PlaySound()
    {
        if (NetworkedSoundManager.Instance != null)
        {
            NetworkedSoundManager.Instance.PlayEnvironmentSound("Keycard_Grabbed", transform.position);
        }
    }

    [ContextMenu("Test Trigger Press")]
    public void DebugTriggerPress()
    {
        RPC_PressChanged();
    }
    
    [ContextMenu("Test Press Event")]
    public void TestPressEvent()
    {
        if (Object.HasStateAuthority)
        {
            if (triggerEventsOnAllClients)
            {
                RPC_TriggerPressedEvent();
            }
            else
            {
                OnButtonPressed?.Invoke();
            }
        }
    }

    [ContextMenu("Test Release Event")]
    public void TestReleaseEvent()
    {
        if (Object.HasStateAuthority)
        {
            if (triggerEventsOnAllClients)
            {
                RPC_TriggerReleasedEvent();
            }
            else
            {
                OnButtonReleased?.Invoke();
            }
        }
    }
}
