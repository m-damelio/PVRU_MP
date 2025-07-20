using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class SneakVisualizerCube : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string deviceIP = "192.168.178.51";
    [SerializeField] private float checkInterval = 1.0f;
    [SerializeField] private bool simulateDevice = true;
    [SerializeField] private bool enableLogs = true;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 1.0f;
    
    [Header("Material Settings")]
    [SerializeField] private Material walkingMaterial;
    [SerializeField] private Material sneakingMaterial;
    
    [Header("Fallback Colors (if no materials assigned)")]
    [SerializeField] private Color walkingColor = Color.green;
    [SerializeField] private Color sneakingColor = Color.red;
    
    private Renderer cubeRenderer;
    private bool isSneaking = false;
    private float sneakValue = 1.0f;
    private Material defaultMaterial;
    private int consecutiveFailures = 0;
    private float lastSuccessfulRequest = 0f;
    
    // Helper class to return result from coroutine
    private class RequestResult
    {
        public bool success = false;
        public bool sneakingState = false;
        public string response = "";
        public float requestTime = 0f;
    }
    
    void Start()
    {
        cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            Debug.LogError("SneakVisualizerCube: No Renderer component found!");
            return;
        }
        
        defaultMaterial = cubeRenderer.material;
        UpdateCubeMaterial();
        StartCoroutine(CheckDeviceLoop());
        
        Debug.Log("SneakVisualizerCube: Started - Press S key to test simulation");
    }
    
    IEnumerator CheckDeviceLoop()
    {
        int checkCount = 0;
        
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            checkCount++;
            
            if (simulateDevice)
            {
                bool wasSneaking = isSneaking;
                isSneaking = Input.GetKey(KeyCode.S);
                
                if (enableLogs && wasSneaking != isSneaking)
                {
                    Debug.Log($"SneakCube: S key changed - Sneaking: {isSneaking} (Check #{checkCount})");
                }
            }
            else
            {
                yield return StartCoroutine(GetDeviceSneakStateWithRetry());
            }
            
            float oldSneakValue = sneakValue;
            sneakValue = isSneaking ? 0.0f : 1.0f;
            
            if (enableLogs && oldSneakValue != sneakValue)
            {
                Debug.Log($"SneakCube: Sneak value updated: {oldSneakValue} -> {sneakValue} [Check #{checkCount}]");
            }
            
            UpdateCubeMaterial();
            
            if (consecutiveFailures > 2)
            {
                Debug.Log($"SneakCube: Connection unstable, increasing check interval");
                yield return new WaitForSeconds(1.0f);
            }
        }
    }
    
    IEnumerator GetDeviceSneakStateWithRetry()
    {
        RequestResult result = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                if (enableLogs)
                    Debug.Log($"SneakCube: Retry attempt {attempt + 1}/{maxRetries}");
                yield return new WaitForSeconds(retryDelay);
            }
            
            // Create new result object for each attempt
            result = new RequestResult();
            yield return StartCoroutine(GetDeviceSneakState(attempt + 1, result));
            
            if (result.success)
            {
                isSneaking = result.sneakingState;
                consecutiveFailures = 0;
                lastSuccessfulRequest = Time.time;
                break; // Success, exit retry loop
            }
        }
        
        if (result != null && !result.success)
        {
            consecutiveFailures++;
            if (enableLogs)
                Debug.LogWarning($"SneakCube: Failed after {maxRetries} attempts. Consecutive failures: {consecutiveFailures}");
        }
    }
    
    IEnumerator GetDeviceSneakState(int attemptNumber, RequestResult result)
    {
        string url = $"http://{deviceIP}/sneakstatus";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 3;
            request.SetRequestHeader("Connection", "close");
            request.SetRequestHeader("Cache-Control", "no-cache");
            
            if (enableLogs && attemptNumber == 1)
                Debug.Log($"SneakCube: Requesting {url} (Attempt {attemptNumber})");
            
            float requestStart = Time.time;
            yield return request.SendWebRequest();
            result.requestTime = Time.time - requestStart;
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                result.response = request.downloadHandler.text.Trim();
                bool wasSneaking = isSneaking;
                result.sneakingState = result.response == "1" || result.response.ToLower() == "true";
                result.success = true;
                
                if (enableLogs && (wasSneaking != result.sneakingState || attemptNumber > 1))
                {
                    Debug.Log($"SneakCube: SUCCESS (Attempt {attemptNumber}) - Response '{result.response}' -> Sneaking: {result.sneakingState} (Time: {result.requestTime:F2}s)");
                }
            }
            else
            {
                result.success = false;
                if (enableLogs)
                {
                    Debug.LogWarning($"SneakCube: FAILED (Attempt {attemptNumber}) - {request.error} (Time: {result.requestTime:F2}s)");
                    if (request.responseCode > 0)
                        Debug.LogWarning($"SneakCube: Response Code: {request.responseCode}");
                }
            }
        }
    }
    
    void UpdateCubeMaterial()
    {
        if (cubeRenderer == null) return;
        
        if (isSneaking)
        {
            if (sneakingMaterial != null)
            {
                cubeRenderer.material = sneakingMaterial;
                if (enableLogs)
                    Debug.Log($"Cube material changed to: SNEAKING MATERIAL - SneakValue: {sneakValue}");
            }
            else
            {
                cubeRenderer.material.color = sneakingColor;
                if (enableLogs)
                    Debug.Log($"Cube color changed to: RED (Sneaking) - SneakValue: {sneakValue}");
            }
        }
        else
        {
            if (walkingMaterial != null)
            {
                cubeRenderer.material = walkingMaterial;
                if (enableLogs)
                    Debug.Log($"Cube material changed to: WALKING MATERIAL - SneakValue: {sneakValue}");
            }
            else
            {
                cubeRenderer.material.color = walkingColor;
                if (enableLogs)
                    Debug.Log($"Cube color changed to: GREEN (Walking) - SneakValue: {sneakValue}");
            }
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log($"=== Sneak Cube Debug ===");
            Debug.Log($"Device IP: {deviceIP}");
            Debug.Log($"Simulate Device: {simulateDevice}");
            Debug.Log($"Is Sneaking: {isSneaking}");
            Debug.Log($"Sneak Value: {sneakValue}");
            Debug.Log($"Check Interval: {checkInterval}s");
            Debug.Log($"Consecutive Failures: {consecutiveFailures}");
            Debug.Log($"Last Successful Request: {Time.time - lastSuccessfulRequest:F1}s ago");
            Debug.Log($"Current Material: {cubeRenderer.material.name}");
            Debug.Log($"S Key Pressed: {Input.GetKey(KeyCode.S)}");
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            Debug.Log("SneakCube: Manual connection test...");
            StartCoroutine(GetDeviceSneakStateWithRetry());
        }
    }
    
    void OnDestroy()
    {
        if (cubeRenderer != null && defaultMaterial != null)
        {
            cubeRenderer.material = defaultMaterial;
        }
    }
}
