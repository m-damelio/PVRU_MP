using UnityEngine;
using Fusion;

public class PressTwoButtonsSimultaneously : NetworkBehaviour, ILevelResettable, ISolvable
{
    [SerializeField] private NetworkedButton button1;
    [SerializeField] private NetworkedButton button2;

    [Header("Networked Properties")]
    [Networked] public bool IsSolved { get; set; }
    
    public System.Action<ISolvable> OnSolved { get; set; }
    private bool wasPreviouslySolved = false;

    void Awake()
    {
        if (button1 == null || button2 == null) 
        {
            Debug.LogWarning("PressTwoButtonsSimultaneously: One of the buttons was not set in the editor");
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
        if (button1 == null || button2 == null) return;
        if (Object == null) return;
        if (!Object.HasStateAuthority) return;

        CheckSimultaneousPress();
        CheckSolution();
    }

    private void CheckSimultaneousPress()
    {
        // Check if both buttons were pressed simultaneously (time intervall between presses defined by how long the cooldown is for a button press)
        bool bothPressed = button1.IsPressed && button2.IsPressed;
        
        if (bothPressed && !IsSolved)
        {
            IsSolved = true;
            Debug.Log("Both buttons pressed simultaneously! Puzzle solved!");
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

    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsSolved = false;
            wasPreviouslySolved = false;
        }
    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsSolved = false;
            wasPreviouslySolved = false;
        }
    }

    public bool IsPuzzleSolved()
    {
        return IsSolved;
    }
}