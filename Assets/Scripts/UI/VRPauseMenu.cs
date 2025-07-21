using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Fusion;
using Fusion.XR.Host.Rig;
using TMPro;

/// VR Pause Menu with Meta Hand Tracking support
/// pinch gestures and finger pointing for interaction
public class VRPauseMenu : NetworkBehaviour
{
    [Header("Menu Activation")]
    [SerializeField] private KeyCode pauseKey1 = KeyCode.P;
    [SerializeField] private KeyCode pauseKey2 = KeyCode.Escape;
    [SerializeField] private KeyCode pauseKey3 = KeyCode.M;
    
    [Header("Menu Positioning")]
    [SerializeField] private float menuDistance = 3f;
    [SerializeField] private float menuHeight = 0f;
    [SerializeField] private Vector3 menuScale = new Vector3(0.003f, 0.003f, 0.003f);
    
    [Header("Hand Tracking Settings")]
    [SerializeField] private LayerMask uiLayerMask = 1 << 5;
    [SerializeField] private float maxInteractionDistance = 10f;
    [SerializeField] private float pinchThreshold = 0.7f;
    [SerializeField] private float hoverDistance = 0.1f;
    
    [Header("Desktop Testing")]
    [SerializeField] private bool useMouseForTesting = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color pointerColorNormal = Color.cyan;
    [SerializeField] private Color pointerColorHover = Color.yellow;
    [SerializeField] private Color pointerColorPinch = Color.green;
    [SerializeField] private float pointerSize = 0.02f;
    
    // Internal variables
    private bool isPaused = false;
    private bool wasKeyPressed = false;
    private GameObject menuCanvas;
    private Camera playerCamera;
    private NetworkRig networkRig;
    
    // Hand tracking
    private GameObject leftPointer;
    private GameObject rightPointer;
    private Button currentHoveredButton;
    private bool wasPinching = false;
    
    // Hand tracking components
    private Transform leftIndexTip;
    private Transform leftThumbTip;
    private Transform rightIndexTip;
    private Transform rightThumbTip;
    
    public override void Spawned()
    {
        base.Spawned();
        
        if (!Object.HasInputAuthority) return;
        
        Debug.Log("VRPauseMenu: Setting up for local player!");
        
        playerCamera = Camera.main ?? FindObjectOfType<Camera>();
        
        networkRig = GetComponent<NetworkRig>();
        if (networkRig == null)
        {
            Debug.LogError("VRPauseMenu: No NetworkRig found!");
            return;
        }
        
        CreatePauseMenu();
        SetupHandTracking();
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
            {
                TogglePauseMenu();
            }
            
            wasKeyPressed = keyPressed;
        }
    }
    
    private void Update()
    {
        if (!Object.HasInputAuthority || !isPaused) return;
        
        HandleHandTrackingInteraction();
    }
    
    private void SetupHandTracking()
    {
        if (networkRig != null)
        {
            // Create visual pointers for hands
            leftPointer = CreateHandPointer("LeftHandPointer");
            rightPointer = CreateHandPointer("RightHandPointer");
            
            leftIndexTip = networkRig.leftHand.transform;
            rightIndexTip = networkRig.rightHand.transform;
            leftThumbTip = networkRig.leftHand.transform; 
            rightThumbTip = networkRig.rightHand.transform;
        }
        
        Debug.Log("VRPauseMenu: Hand tracking setup complete!");
    }
    
    private GameObject CreateHandPointer(string name)
    {
        var pointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pointer.name = name;
        pointer.transform.localScale = Vector3.one * pointerSize;
        
        Destroy(pointer.GetComponent<Collider>());
        var renderer = pointer.GetComponent<Renderer>();
        var material = new Material(Shader.Find("Standard"));
        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.color = pointerColorNormal;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", pointerColorNormal * 0.5f);
        renderer.material = material;
        
        pointer.SetActive(false);
        return pointer;
    }
    
    private void HandleHandTrackingInteraction()
    {
        if (useMouseForTesting)
        {
            HandleMouseSimulation();
        }
        else
        {
            bool leftInteracting = HandleHandInteraction(leftIndexTip, leftThumbTip, leftPointer, "Left");
            bool rightInteracting = HandleHandInteraction(rightIndexTip, rightThumbTip, rightPointer, "Right");
            
            if (leftPointer != null) leftPointer.SetActive(isPaused && leftInteracting);
            if (rightPointer != null) rightPointer.SetActive(isPaused && rightInteracting);
        }
    }
    
    private void HandleMouseSimulation()
    {
        if (!isPaused || playerCamera == null) return;
        
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        bool hitUI = false;
        bool isClicking = Input.GetMouseButton(0);
        if (rightPointer != null)
        {
            Vector3 mouseWorldPos = ray.origin + ray.direction * menuDistance;
            rightPointer.transform.position = mouseWorldPos;
            rightPointer.SetActive(true);
        }
        
        // Check UI interaction
        if (Physics.Raycast(ray, out hit, maxInteractionDistance, uiLayerMask))
        {
            if (hit.collider.gameObject == menuCanvas)
            {
                hitUI = true;
                
                // Check for button hover
                var button = GetButtonAtPosition(hit.point);
                if (button != currentHoveredButton)
                {
                    // Unhighlight previous button
                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
                    }
                    
                    // Highlight new button
                    currentHoveredButton = button;
                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.OnPointerEnter(new PointerEventData(EventSystem.current));
                        Debug.Log($"VRPauseMenu: Mouse hovering over {currentHoveredButton.name}");
                    }
                }
                
                // Handle click
                if (isClicking && !wasPinching && currentHoveredButton != null)
                {
                    Debug.Log($"VRPauseMenu: Mouse clicked {currentHoveredButton.name}");
                    currentHoveredButton.onClick.Invoke();
                }
            }
        }
        
        if (!hitUI && currentHoveredButton != null)
        {
            currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
            currentHoveredButton = null;
        }
        
        // Update pointer visual based on state
        if (rightPointer != null)
        {
            var renderer = rightPointer.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color targetColor = pointerColorNormal;
                if (isClicking)
                    targetColor = pointerColorPinch;
                else if (hitUI)
                    targetColor = pointerColorHover;
                    
                renderer.material.color = targetColor;
                renderer.material.SetColor("_EmissionColor", targetColor * 0.5f);
            }
        }
        
        wasPinching = isClicking;
    }
    
    private bool HandleHandInteraction(Transform indexTip, Transform thumbTip, GameObject pointer, string handName)
    {
        if (indexTip == null || pointer == null) return false;
        
        // Position the pointer at the index finger tip
        pointer.transform.position = indexTip.position;
        
        // Cast ray from index finger
        Ray ray = new Ray(indexTip.position, indexTip.forward);
        RaycastHit hit;
        
        bool hitUI = false;
        bool isPinching = false;
        
        // Check for pinch gesture
        if (thumbTip != null)
        {
            float pinchDistance = Vector3.Distance(indexTip.position, thumbTip.position);
            isPinching = pinchDistance < pinchThreshold;
        }
        else
        {
            // Fallback: use space bar or mouse for testing
            isPinching = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
        }
        
        // Check UI interaction
        if (Physics.Raycast(ray, out hit, maxInteractionDistance, uiLayerMask))
        {
            if (hit.collider.gameObject == menuCanvas)
            {
                hitUI = true;
                
                // Check for button hover
                var button = GetButtonAtPosition(hit.point);
                if (button != currentHoveredButton)
                {
                    // Unhighlight previous button
                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
                    }
                    
                    // Highlight new button
                    currentHoveredButton = button;
                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.OnPointerEnter(new PointerEventData(EventSystem.current));
                        Debug.Log($"VRPauseMenu: {handName} hand hovering over {currentHoveredButton.name}");
                    }
                }
                
                // Handle pinch to click
                if (isPinching && !wasPinching && currentHoveredButton != null)
                {
                    Debug.Log($"VRPauseMenu: {handName} hand pinched to click {currentHoveredButton.name}");
                    currentHoveredButton.onClick.Invoke();
                }
            }
        }
        
        if (!hitUI && currentHoveredButton != null)
        {
            currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
            currentHoveredButton = null;
        }
        
        // Update pointer visual based on state
        var renderer = pointer.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color targetColor = pointerColorNormal;
            if (isPinching)
                targetColor = pointerColorPinch;
            else if (hitUI)
                targetColor = pointerColorHover;
                
            renderer.material.color = targetColor;
            renderer.material.SetColor("_EmissionColor", targetColor * 0.5f);
        }
        
        wasPinching = isPinching;
        return hitUI || isPinching;
    }
    
    private Button GetButtonAtPosition(Vector3 worldPosition)
    {
        // Convert world position to canvas local position
        var localPoint = menuCanvas.transform.InverseTransformPoint(worldPosition);
        
        // Find all buttons and check which one is closest to the hit point
        var buttons = menuCanvas.GetComponentsInChildren<Button>();
        Button closestButton = null;
        float closestDistance = float.MaxValue;
        
        foreach (var button in buttons)
        {
            var buttonRect = button.GetComponent<RectTransform>();
            var buttonLocalPos = buttonRect.anchoredPosition;
            var distance = Vector2.Distance(new Vector2(localPoint.x, localPoint.y), buttonLocalPos);
            
            // Check if point is within button bounds
            var size = buttonRect.sizeDelta;
            if (Mathf.Abs(localPoint.x - buttonLocalPos.x) <= size.x / 2 &&
                Mathf.Abs(localPoint.y - buttonLocalPos.y) <= size.y / 2)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestButton = button;
                }
            }
        }
        
        return closestButton;
    }
    
    private void CreatePauseMenu()
    {
        // Create canvas
        menuCanvas = new GameObject("Meta Hand VR Pause Menu");
        Canvas canvas = menuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        menuCanvas.layer = 5; // UI layer

        // Add canvas scaler
        var scaler = menuCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // Add graphic raycaster
        var raycaster = menuCanvas.AddComponent<GraphicRaycaster>();

        // Add box collider for interaction
        var collider = menuCanvas.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(400, 300, 10);

        // Set size and scale
        var rectTransform = menuCanvas.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(400, 300);
        menuCanvas.transform.localScale = menuScale;

        // Create background
        CreateBackground();

        // Create title
        CreateText("GAME PAUSED", new Vector2(0, 100), 32, FontStyles.Bold);

        // Create buttons
        CreateButton("Resume Game", new Vector2(0, 40), Color.green, () => TogglePauseMenu());
        CreateButton("Restart Level", new Vector2(0, -10), new Color(1f, 0.5f, 0f), () => RestartLevel());
        CreateButton("Exit Game", new Vector2(0, -60), Color.red, () => QuitGame());

        // Create instruction text
        CreateText("Point with finger and pinch to interact", new Vector2(0, -120), 14, FontStyles.Italic);

        // Initially hide
        menuCanvas.SetActive(false);

        Debug.Log("VRPauseMenu: Menu created successfully!");
    }
    
    private void CreateBackground()
    {
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(menuCanvas.transform, false);
        bgGO.layer = 5;
        
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.15f, 0.95f);
        
        // Add border
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(bgGO.transform, false);
        borderGO.layer = 5;
        
        var borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(-10, -10);
        borderRect.anchoredPosition = Vector2.zero;
        
        var borderImage = borderGO.AddComponent<Image>();
        borderImage.color = Color.clear;
        
        // Add outline
        var outline = borderGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2, 2);
    }
    
    private void CreateText(string text, Vector2 position, int fontSize, FontStyles style = FontStyles.Normal)
    {
        var textGO = new GameObject($"Text_{text}");
        textGO.transform.SetParent(menuCanvas.transform, false);
        textGO.layer = 5;
        
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
        buttonGO.layer = 5;
        
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(250, 40);
        
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor * 0.7f;
        
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Set button colors
        var colors = button.colors;
        colors.normalColor = buttonColor * 0.7f;
        colors.highlightedColor = buttonColor;
        colors.pressedColor = buttonColor * 0.5f;
        colors.selectedColor = buttonColor * 0.8f;
        button.colors = colors;
        
        // Add text to button
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        textGO.layer = 5;
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        var textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 18;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontStyle = FontStyles.Bold;
        
        // Add click event
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
    }
    
    private void TogglePauseMenu()
    {
        isPaused = !isPaused;
        
        if (menuCanvas != null)
        {
            menuCanvas.SetActive(isPaused);
        }
        
        if (isPaused)
        {
            ShowMenu();
        }
        else
        {
            HideMenu();
        }
        
        Debug.Log($"VRPauseMenu: Menu {(isPaused ? "opened" : "closed")}");
    }
    
    private void ShowMenu()
    {
        // Position menu in front of player
        if (playerCamera != null && menuCanvas != null)
        {
            Vector3 forward = playerCamera.transform.forward;
            Vector3 menuPosition = playerCamera.transform.position + forward * menuDistance;
            menuPosition.y = playerCamera.transform.position.y + menuHeight;
            
            menuCanvas.transform.position = menuPosition;
            menuCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
    
    private void HideMenu()
    {
        // Hide pointers
        if (leftPointer != null) leftPointer.SetActive(false);
        if (rightPointer != null) rightPointer.SetActive(false);
        
        // Clear hovered button
        if (currentHoveredButton != null)
        {
            currentHoveredButton.OnPointerExit(new PointerEventData(EventSystem.current));
            currentHoveredButton = null;
        }
    }
    
    private void RestartLevel()
    {
        Debug.Log("VRPauseMenu: Requesting level restart for all players...");
        
        // Use RPC to restart for all players
        if (Object.HasInputAuthority)
        {
            RPC_RestartLevelForAll();
        }
    }
    
    private void QuitGame()
    {
        Debug.Log("VRPauseMenu: Requesting game quit for all players...");
        if (Object.HasInputAuthority)
        {
            RPC_QuitGameForAll();
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_RestartLevelForAll()
    {
        Debug.Log("VRPauseMenu: Restarting level for all players...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_QuitGameForAll()
    {
        Debug.Log("VRPauseMenu: Quitting game for all players...");
        Time.timeScale = 1f;
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}