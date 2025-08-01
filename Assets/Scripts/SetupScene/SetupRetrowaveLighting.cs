using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class SetupRetrowaveLighting : MonoBehaviour
{
    [Header("Ligting Color")]
    [SerializeField] private Color primaryNeonColor = new Color(1f, 0f, 1f, 1f); //Magenta

    [Header("Fog Settings")]
    [SerializeField] private bool enableFog = true;
    [SerializeField] private Color fogColor = new Color(0.2f, 0.1f, 0.4f, 1f);
    [SerializeField] private float fogDensity = 0.02f;

    [Header("Post Processing")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Dynamic Lighting")]
    [SerializeField] private bool enablePulsing = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.5f;

    private Light[] _neonLights;
    private float _originalIntensity;
    private Bloom _bloom;
    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private ChromaticAberration _chromaticAberration;
    private FilmGrain _filmGrain;

    void Start()
    {
        SetupLighting();
        SetupFog();
        SetupPostProcessing();
        FindNeonLights();
    }

    void SetupLighting()
    {
        //reduce amiebt light
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.3f;
    }

    void SetupFog()
    {
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    void SetupPostProcessing()
    {
        if (postProcessVolume == null) return;

        var profile = postProcessVolume.profile;

        //Steup bloom
        if (profile.TryGet<Bloom>(out _bloom))
        {
            _bloom.intensity.value = 0.8f;
            _bloom.threshold.value = 0.5f;
            _bloom.scatter.value = 0.7f;
            _bloom.tint.value = primaryNeonColor;
            _bloom.active = true;
        }

        //Setup color adjustments
        if (profile.TryGet<ColorAdjustments>(out _colorAdjustments))
        {
            _colorAdjustments.contrast.value = 20f;
            _colorAdjustments.saturation.value = 30f;
            _colorAdjustments.colorFilter.value = new Color(1f, 0.9f, 1f);
            _colorAdjustments.active = true;
        }

        //Setup Vignette
        if (profile.TryGet<Vignette>(out _vignette))
        {
            _vignette.intensity.value = 0.3f;
            _vignette.smoothness.value = 0.5f;
            _vignette.color.value = new Color(0.2f, 0.1f, 0.4f);
            _vignette.active = true;
        }

        //Steup chromatic aberration
        if (profile.TryGet<ChromaticAberration>(out _chromaticAberration))
        {
            _chromaticAberration.intensity.value = 0.3f;
            _chromaticAberration.active = true;
        }

        if (profile.TryGet<FilmGrain>(out _filmGrain))
        {
            _filmGrain.intensity.value = 0.2f;
            _filmGrain.response.value = 0.8f;
            _filmGrain.active = true;
        }
    }

    void FindNeonLights()
    {
        NeonLight.OnNeonLightChanged += RefreshNeonLights;
        //Find all lights tagged as neon
        RefreshNeonLightsInternal();

    }
    void RefreshNeonLightsInternal()
    {
        List<Light> neonLightsList = new List<Light>();

        foreach (NeonLight neonLight in NeonLight.AllNeonLights)
        {
            if (neonLight != null && neonLight.GetLight() != null)
            {
                Light light = neonLight.GetLight();
                neonLightsList.Add(light);
                SetupNeonLight(light);
            }
        }

        _neonLights = neonLightsList.ToArray();

        Debug.Log($"Found {_neonLights.Length} active neon lights");
    }

    void SetupNeonLight(Light light)
    {
        light.type = LightType.Point;
        light.color = primaryNeonColor;
    }

    void Update()
    {
        if (enablePulsing) UpdatePulsingLights();
    }

    void UpdatePulsingLights()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity + 1f;

        foreach (Light light in _neonLights)
        {
            if (light != null)
            {
                light.intensity = 2f * pulse;
            }
        }
    }

    public void RefreshNeonLights()
    {
        RefreshNeonLightsInternal();
    }

    void OnDestroy()
    {
        NeonLight.OnNeonLightChanged -= RefreshNeonLights;
    }

}
