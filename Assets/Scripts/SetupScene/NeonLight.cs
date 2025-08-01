using UnityEngine;
using System.Collections.Generic;
public class NeonLight : MonoBehaviour
{
    public static List<NeonLight> AllNeonLights = new List<NeonLight>();
    
    public static System.Action OnNeonLightChanged;

    private Light _light;
    
    void Awake()
    {
        _light = GetComponent<Light>();
    }
    
    void OnEnable()
    {
        // Register when enabled
        if (!AllNeonLights.Contains(this))
        {
            AllNeonLights.Add(this);
            OnNeonLightChanged?.Invoke(); // Notify lighting manager
        }
    }
    
    void OnDisable()
    {
        // Unregister when disabled
        AllNeonLights.Remove(this);
        OnNeonLightChanged?.Invoke(); // Notify lighting manager
    }
    
    void OnDestroy()
    {
        // Clean up when destroyed
        AllNeonLights.Remove(this);
        OnNeonLightChanged?.Invoke();
    }
    
    public Light GetLight()
    {
        return _light;
    }
}