using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Fusion.XR.Host.Rig;
using Fusion.XR.Host.Locomotion;
using System.Collections.Generic;
using System.Linq;

public class VRRayUIClickHandler : MonoBehaviour
{
    [Header("UI Canvas")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Rig Selection")]
    [SerializeField] private bool autoDetectRig = true;
    [SerializeField] private HardwareRig manualRig;

    private HardwareRig activeRig;
    private RigLocomotion rigLocomotion;
    private List<RayBeamer> rayBeamers = new List<RayBeamer>();
    private Camera playerCamera;
    private Button currentHoveredButton;

    private void Start()
    {
        if (autoDetectRig)
        {
            activeRig = FindObjectsOfType<HardwareRig>(true).FirstOrDefault(r => r.isActiveAndEnabled);
        }
        else
        {
            activeRig = manualRig;
        }

        if (activeRig == null)
        {
            Debug.LogError("[VRRayUIClickHandler] No active HardwareRig found!");
            return;
        }

        rigLocomotion = activeRig.GetComponentInChildren<RigLocomotion>(true);
        if (rigLocomotion != null)
            rayBeamers = new List<RayBeamer>(rigLocomotion.teleportBeamers);

        // Kamera holen
        var camTransform = activeRig.transform.Find("Camera Offset/Main Camera");
        if (camTransform != null) playerCamera = camTransform.GetComponent<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;

        Debug.Log($"[VRRayUIClickHandler] Ready. RayBeamers: {rayBeamers.Count}, Camera: {playerCamera?.name}");
    }

    private void Update()
    {
        if (playerCamera == null || rayBeamers.Count == 0) return;
        HandleRayHover();
    }

    private void HandleRayHover()
    {
        foreach (var beamer in rayBeamers)
        {
            if (!beamer.isRayEnabled) continue;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = playerCamera.WorldToScreenPoint(beamer.ray.target)
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                var button = results[0].gameObject.GetComponent<Button>();
                if (button != currentHoveredButton)
                {
                    if (currentHoveredButton != null)
                        currentHoveredButton.OnPointerExit(pointerData);

                    currentHoveredButton = button;

                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.OnPointerEnter(pointerData);
                        Debug.Log($"[VRRayUIClickHandler] Hover: {currentHoveredButton.name}");
                    }
                }
            }
            else if (currentHoveredButton != null)
            {
                currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
                currentHoveredButton = null;
            }
        }
    }

    public void OnLaserClick()
    {
        if (currentHoveredButton != null)
        {
            Debug.Log($"[VRRayUIClickHandler] Clicked: {currentHoveredButton.name}");
            currentHoveredButton.onClick.Invoke();
        }
    }

    private void OnEnable()
    {
        foreach (var beamer in rayBeamers)
            beamer.onRelease.AddListener((col, pos) => OnLaserClick());
    }

    private void OnDisable()
    {
        foreach (var beamer in rayBeamers)
            beamer.onRelease.RemoveListener((col, pos) => OnLaserClick());
    }
}
