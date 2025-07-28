using UnityEngine;
using Fusion;
using System.Collections.Generic;

//Base class for level specific logic, override in derived class for level-specific logic (I.e. Level 1 logic might differ from level 2 logic)
public class LevelController : NetworkBehaviour
{
    [Header("Level Reset Manager")]
    [SerializeField] private List<GameObject> resettableObjects = new List<GameObject>();
    [SerializeField] private bool autoFindResettableObjects = true;

    private List<ILevelResettable> resettableComponents = new List<ILevelResettable>();

    [Header("Level Solved Manager")]
    [SerializeField] private GameObject finalStepGameobject;
    private ISolvable finalSolvableStep;
    [SerializeField] private LevelManager levelManager;

    void OnValidate()
    {
        if (finalStepGameobject != null)
        {
            ISolvable checkImplementsInterface = finalStepGameobject.GetComponent<ISolvable>();
            if (checkImplementsInterface == null)
            {
                Debug.LogError($"LevelController: Object {finalStepGameobject.name} doesn't implement ISolvable");
            }
        }
    }

    void Start()
    {
        finalSolvableStep = finalStepGameobject.GetComponent<ISolvable>();
    }

    public override void Spawned()
    {
        //Store initial states when level is first loaded
        InitializeResettableObjects();
        InitializeFinalStep();
        SetInitialStates();
    }

    private void InitializeResettableObjects()
    {
        resettableComponents.Clear();

        //Auto find all resettable objects in level if enabled
        if (autoFindResettableObjects)
        {
            var foundResettables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var obj in foundResettables)
            {
                if (obj is ILevelResettable && obj.transform.IsChildOf(transform))
                {
                    resettableComponents.Add(obj as ILevelResettable);
                }
            }
        }

        //Adds manually assigned objects
        foreach (var obj in resettableObjects)
        {
            var resettable = obj.GetComponent<ILevelResettable>();
            if (resettable != null && !resettableComponents.Contains(resettable))
            {
                resettableComponents.Add(resettable);
            }
        }

        Debug.Log($"Level Controller: Found {resettableComponents.Count} resettable objects");
    }

    private void SetInitialStates()
    {
        foreach (var resettable in resettableComponents)
        {
            resettable.SetInitialState();
        }
    }

    private void InitializeFinalStep()
    {
        if (finalSolvableStep != null) finalSolvableStep.OnSolved += OnFinalStepSolved;
    }

    private void OnFinalStepSolved(ISolvable solvablePuzzle)
    {
        Debug.Log("LevelController: Final step solved action was called from " + solvablePuzzle);
        if (levelManager != null)
        {
            levelManager.RPC_CompleteLevel();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResetLevel()
    {
        Debug.Log("Level Controller: Resetting level to initial state");

        foreach (var resettable in resettableComponents)
        {
            resettable.ResetToInitialState();
        }
    }

    public virtual void RestartLevel()
    {
        if (Object.HasStateAuthority) RPC_ResetLevel();
    }

    public virtual void OnLevelActivated()
    {
        if (Object.HasStateAuthority) RPC_ResetLevel();
    }

    public virtual void OnLevelDeactivated()
    {
        //TODO: Cleanup ongoing processes if any 
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeFromSolvableEvents();
    }

    private void UnsubscribeFromSolvableEvents()
    {
        if(finalSolvableStep != null) finalSolvableStep.OnSolved -= OnFinalStepSolved;
    }
}
