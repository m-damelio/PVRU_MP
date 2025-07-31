using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Fusion;
using Fusion.XR.Host.Rig;
using Fusion.XR.Host.Locomotion;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class VRPauseMenuWithRayBeamer : NetworkBehaviour
{
    [Header("Menu Activation")]
    [SerializeField] private KeyCode pauseKey1 = KeyCode.P;
    [SerializeField] private KeyCode pauseKey2 = KeyCode.Escape;
    [SerializeField] private KeyCode pauseKey3 = KeyCode.M;

    [Header("Menu Positioning")]
    [SerializeField] private float menuDistance = 3f;
    [SerializeField] private float menuHeight = 0f;
    [SerializeField] private Vector3 menuScale = new Vector3(0.003f, 0.003f, 0.003f);

    [Header("Rig Selection")]
    [SerializeField] private bool autoDetectRig = true;
    [SerializeField] private HardwareRig manualRig;

    private bool isPaused = false;
    private bool wasKeyPressed = false;

    private GameObject menuCanvas;
    private Camera playerCamera;

    private HardwareRig activeRig;
    private RigLocomotion rigLocomotion;
    private List<RayBeamer> rayBeamers = new List<RayBeamer>();
    private Button currentHoveredButton;
    private Collider lastPhysicsHit;

    private Vector3 debugMenuPosition;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority) return;

        Debug.Log("[VRPauseMenu] Initializing...");

        if (autoDetectRig)
        {
            activeRig = GetComponentInChildren<HardwareRig>(true);

            if (activeRig == null || !activeRig.isActiveAndEnabled)
            {
                Debug.LogWarning("[VRPauseMenu] No active HardwareRig under player, searching scene...");
                var rigs = FindObjectsOfType<HardwareRig>(true).Where(r => r.isActiveAndEnabled).ToList();

                if (rigs.Count > 0)
                {
                    activeRig = rigs[0];
                    Debug.Log($"[VRPauseMenu] Found active HardwareRig: {activeRig.name}");
                }
                else
                {
                    Debug.LogError("[VRPauseMenu] No active HardwareRig found!");
                    return;
                }
            }
        }
        else
        {
            if (manualRig == null)
            {
                Debug.LogError("[VRPauseMenu] Manual rig not assigned!");
                return;
            }
            activeRig = manualRig;
        }

        Debug.Log($"[VRPauseMenu] Using HardwareRig: {activeRig.name}");

        rigLocomotion = activeRig.GetComponentInChildren<RigLocomotion>(true);
        if (rigLocomotion == null)
        {
            Debug.LogError("[VRPauseMenu] No RigLocomotion found in HardwareRig!");
            return;
        }

        rayBeamers = new List<RayBeamer>(rigLocomotion.teleportBeamers);
        Debug.Log($"[VRPauseMenu] Found {rayBeamers.Count} RayBeamers.");

        AssignRigCamera();

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[VRPauseMenu] No EventSystem in scene. Creating one...");
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // create menu
        CreatePauseMenu();

        foreach (var beamer in rayBeamers)
            beamer.onRelease.AddListener(OnLaserRelease);
    }

    private void AssignRigCamera()
    {
        Transform camTransform = activeRig.transform.Find("Camera Offset/Main Camera");
        if (camTransform != null)
        {
            var cam = camTransform.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                playerCamera = cam;
                Debug.Log($"[VRPauseMenu] Using XR camera: {playerCamera.name}");
                return;
            }
            else
            {
                Debug.LogWarning("[VRPauseMenu] Found XR camera but it is disabled.");
            }
        }

        // fallback
        playerCamera = Camera.main;
        Debug.LogWarning($"[VRPauseMenu] Using Camera.main fallback: {playerCamera?.name}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;

        if (GetInput<RigInput>(out var rigInput))
        {
            bool keyPressed = rigInput.keyPressed1 == pauseKey1 ||
                              rigInput.keyPressed1 == pauseKey2 ||
                              rigInput.keyPressed1 == pauseKey3;

            if (keyPressed && !wasKeyPressed)
                TogglePauseMenu();

            wasKeyPressed = keyPressed;
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority) return;

        // take rig camera
        if ((playerCamera == null || !playerCamera.enabled) && activeRig != null)
        {
            Transform camTransform = activeRig.transform.Find("Camera Offset/Main Camera");
            if (camTransform != null)
            {
                var cam = camTransform.GetComponent<Camera>();
                if (cam != null && cam.enabled)
                {
                    playerCamera = cam;
                    Debug.Log($"[VRPauseMenu] XR camera became active: {playerCamera.name}");
                }
            }
        }

        if (isPaused) HandleRayHover();
    }

    private void HandleRayHover()
    {
        if (playerCamera == null)
        {
            Debug.LogError("[VRPauseMenu] No camera available for ray hover!");
            return;
        }

        if (EventSystem.current == null)
        {
            Debug.LogError("[VRPauseMenu] No EventSystem active!");
            return;
        }

        foreach (var beamer in rayBeamers)
        {
            if (beamer == null || !beamer.isRayEnabled) continue;

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
                        Debug.Log($"[VRPauseMenu] Hover: {currentHoveredButton.name}");
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

    private void OnLaserRelease(Collider hitCollider, Vector3 hitPoint)
    {
        if (!isPaused || currentHoveredButton == null) return;
        Debug.Log($"[VRPauseMenu] Laser clicked: {currentHoveredButton.name}");
        currentHoveredButton.onClick.Invoke();
    }

    private void TogglePauseMenu()
    {
        isPaused = !isPaused;
        if (menuCanvas != null) menuCanvas.SetActive(isPaused);

        if (isPaused) ShowMenu();
        else HideMenu();

        Debug.Log($"[VRPauseMenu] Menu {(isPaused ? "opened" : "closed")}");
    }

    private void ShowMenu()
    {
        if (playerCamera != null && menuCanvas != null)
        {
            Vector3 forward = playerCamera.transform.forward;
            Vector3 menuPosition = playerCamera.transform.position + forward * menuDistance;
            menuPosition.y = playerCamera.transform.position.y + menuHeight;

            debugMenuPosition = menuPosition;

            Debug.Log($"[VRPauseMenu] Showing menu at {menuPosition}");

            menuCanvas.transform.position = menuPosition;
            menuCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            menuCanvas.SetActive(true);
        }
    }

    private void HideMenu()
    {
        if (currentHoveredButton != null)
        {
            currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
            currentHoveredButton = null;
        }
    }

    private void CreatePauseMenu()
    {
        menuCanvas = new GameObject("VR Pause Menu");
        Canvas canvas = menuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        menuCanvas.layer = 5;

        var scaler = menuCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        menuCanvas.AddComponent<GraphicRaycaster>();
        var collider = menuCanvas.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(400, 300, 10);

        var rectTransform = menuCanvas.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(400, 300);
        menuCanvas.transform.localScale = menuScale;

        CreateBackground();
        CreateText("GAME PAUSED", new Vector2(0, 100), 32, FontStyles.Bold);
        CreateButton("Resume Game", new Vector2(0, 40), Color.green, TogglePauseMenu);
        CreateButton("Restart Level", new Vector2(0, -10), new Color(1f, 0.5f, 0f), RestartLevel);
        CreateButton("Exit Game", new Vector2(0, -60), Color.red, QuitGame);
        CreateText("Point laser and release to interact", new Vector2(0, -120), 14, FontStyles.Italic);

        menuCanvas.SetActive(false);
    }

    private void CreateBackground()
    {
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(menuCanvas.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.15f, 0.95f);
    }

    private void CreateText(string text, Vector2 position, int fontSize, FontStyles style = FontStyles.Normal)
    {
        var textGO = new GameObject($"Text_{text}");
        textGO.transform.SetParent(menuCanvas.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchoredPosition = position;
        textRect.sizeDelta = new Vector2(350, 40);
        var textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontStyle = style;
    }

    private void CreateButton(string text, Vector2 position, Color buttonColor, System.Action onClick)
    {
        var buttonGO = new GameObject($"Button_{text}");
        buttonGO.transform.SetParent(menuCanvas.transform, false);
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(250, 40);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor * 0.7f;
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        var colors = button.colors;
        colors.normalColor = buttonColor * 0.7f;
        colors.highlightedColor = buttonColor;
        colors.pressedColor = buttonColor * 0.5f;
        colors.selectedColor = buttonColor * 0.8f;
        button.colors = colors;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 18;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontStyle = FontStyles.Bold;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());
    }

    private void RestartLevel()
    {
        if (Object.HasInputAuthority)
            RPC_RestartLevelForAll();
    }

    private void QuitGame()
    {
        if (Object.HasInputAuthority)
            RPC_QuitGameForAll();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_RestartLevelForAll()
    {
        // TODO!!!
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_QuitGameForAll()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDrawGizmos()
    {
        if (isPaused && debugMenuPosition != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(debugMenuPosition, new Vector3(0.5f, 0.3f, 0.01f));
        }
    }
}
