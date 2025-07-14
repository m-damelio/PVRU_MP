using UnityEngine;
using Fusion;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class AnimatorStateSync : NetworkBehaviour
{
    [Header("Sync Settings")]
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private float positionThreshold = 0.1f;
    [SerializeField] private float rotationThreshold = 5f;

    [Header("Parameter Sync")]
    [SerializeField] private List<string> boolParameters = new List<string>();
    [SerializeField] private List<string> intParameters = new List<string>();
    [SerializeField] private List<string> floatParameters = new List<string>();
    [SerializeField] private List<string> triggerParameters = new List<string>();

    private Animator _animator;
    private Dictionary<string, int> _parameterHashes = new Dictionary<string, int>();
    
    // Networked state
    [Networked] public int CurrentStateHash { get; set; }
    [Networked] public float StateTime { get; set; }
    [Networked] public int LayerIndex { get; set; }
    
    // Networked parameters - adjust size based on your needs
    [Networked, Capacity(10)] public NetworkDictionary<int, bool> BoolParams { get; }
    [Networked, Capacity(10)] public NetworkDictionary<int, int> IntParams { get; }
    [Networked, Capacity(10)] public NetworkDictionary<int, float> FloatParams { get; }
    
    // For triggers - we'll use a different approach
    [Networked] public int TriggerHash { get; set; }
    [Networked] public byte TriggerFrame { get; set; }
    
    private byte _lastTriggerFrame;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        
        // Cache parameter hashes for performance
        foreach (var param in boolParameters)
            _parameterHashes[param] = Animator.StringToHash(param);
        foreach (var param in intParameters)
            _parameterHashes[param] = Animator.StringToHash(param);
        foreach (var param in floatParameters)
            _parameterHashes[param] = Animator.StringToHash(param);
        foreach (var param in triggerParameters)
            _parameterHashes[param] = Animator.StringToHash(param);
            
        _lastPosition = transform.position;
        _lastRotation = transform.rotation;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // Sync animator state
        if (_animator.layerCount > 0)
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            CurrentStateHash = stateInfo.fullPathHash;
            StateTime = stateInfo.normalizedTime;
            LayerIndex = 0;
        }

        // Sync parameters
        SyncParameters();
    }

    public override void Render()
    {
        if (Object.HasStateAuthority) return;

        // Apply synced state to non-authoritative clients
        ApplySyncedState();
        ApplySyncedParameters();
        CheckForTriggers();
    }

    private void SyncParameters()
    {
        // Sync bool parameters
        foreach (var param in boolParameters)
        {
            if (_parameterHashes.TryGetValue(param, out int hash))
            {
                bool value = _animator.GetBool(hash);
                BoolParams.Set(hash, value);
            }
        }

        // Sync int parameters
        foreach (var param in intParameters)
        {
            if (_parameterHashes.TryGetValue(param, out int hash))
            {
                int value = _animator.GetInteger(hash);
                IntParams.Set(hash, value);
            }
        }

        // Sync float parameters
        foreach (var param in floatParameters)
        {
            if (_parameterHashes.TryGetValue(param, out int hash))
            {
                float value = _animator.GetFloat(hash);
                FloatParams.Set(hash, value);
            }
        }
    }

    private void ApplySyncedState()
    {
        if (CurrentStateHash != 0)
        {
            // Check if we need to transition to the synced state
            var currentState = _animator.GetCurrentAnimatorStateInfo(LayerIndex);
            
            if (currentState.fullPathHash != CurrentStateHash)
            {
                // Force the state if it's different
                _animator.Play(CurrentStateHash, LayerIndex, StateTime);
            }
            else
            {
                // Sync the time if we're in the right state but time is off
                float timeDiff = Mathf.Abs(currentState.normalizedTime - StateTime);
                if (timeDiff > 0.1f) // Threshold to avoid micro-corrections
                {
                    _animator.Play(CurrentStateHash, LayerIndex, StateTime);
                }
            }
        }
    }

    private void ApplySyncedParameters()
    {
        // Apply bool parameters
        foreach (var kvp in BoolParams)
        {
            _animator.SetBool(kvp.Key, kvp.Value);
        }

        // Apply int parameters
        foreach (var kvp in IntParams)
        {
            _animator.SetInteger(kvp.Key, kvp.Value);
        }

        // Apply float parameters
        foreach (var kvp in FloatParams)
        {
            _animator.SetFloat(kvp.Key, kvp.Value);
        }
    }

    private void CheckForTriggers()
    {
        if (TriggerFrame != _lastTriggerFrame)
        {
            if (TriggerHash != 0)
            {
                _animator.SetTrigger(TriggerHash);
            }
            _lastTriggerFrame = TriggerFrame;
        }
    }

    // Public method to trigger animations from other scripts
    public void NetworkTrigger(string parameterName)
    {
        if (Object.HasStateAuthority && _parameterHashes.TryGetValue(parameterName, out int hash))
        {
            _animator.SetTrigger(hash);
            TriggerHash = hash;
            TriggerFrame = (byte)((TriggerFrame + 1) % 256);
        }
    }

    // Public method to set bool parameter
    public void SetNetworkBool(string parameterName, bool value)
    {
        if (Object.HasStateAuthority && _parameterHashes.TryGetValue(parameterName, out int hash))
        {
            _animator.SetBool(hash, value);
        }
    }

    // Public method to set int parameter
    public void SetNetworkInt(string parameterName, int value)
    {
        if (Object.HasStateAuthority && _parameterHashes.TryGetValue(parameterName, out int hash))
        {
            _animator.SetInteger(hash, value);
        }
    }

    // Public method to set float parameter
    public void SetNetworkFloat(string parameterName, float value)
    {
        if (Object.HasStateAuthority && _parameterHashes.TryGetValue(parameterName, out int hash))
        {
            _animator.SetFloat(hash, value);
        }
    }
}