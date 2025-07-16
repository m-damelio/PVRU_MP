using UnityEngine;
using Fusion;

public class ScanlineController : NetworkBehaviour
{
    [Header("Scanline Settings")]
    public Color activeColor = Color.cyan;
    public Color inactiveColor = Color.red;
    public float activeScanlineIntensity = 0.1f;
    public float inactiveScanlineIntensity = 0.05f;

    [Networked] public bool IsActive {get; set;}

    [SerializeField] private Renderer objectRenderer;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int ScanlineColorID = Shader.PropertyToID("_ScanlineColor");
    private static readonly int ScanlineIntensityID = Shader.PropertyToID("_ScanlineIntensity");

    public override void Spawned()
    {
        
        if(objectRenderer == null)
        {
            Debug.Log("NetworkedScanlineController requires Renderer, set in inspector");
            return;
        }
        IsActive = true;
        propertyBlock = new MaterialPropertyBlock();
        UpdateScanlineProperties();
    }

    void UpdateScanlineProperties()
    {
        if(objectRenderer == null || propertyBlock == null) return;

        objectRenderer.GetPropertyBlock(propertyBlock);

        Color currentColor = IsActive ? activeColor : inactiveColor;
        float currentIntensity = IsActive ? activeScanlineIntensity : inactiveScanlineIntensity;

        propertyBlock.SetColor(ScanlineColorID, currentColor);
        propertyBlock.SetFloat(ScanlineIntensityID, currentIntensity);

        objectRenderer.SetPropertyBlock(propertyBlock); 
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetActive(bool enabled)
    {
        IsActive = enabled;
        UpdateScanlineProperties();
    }
}
