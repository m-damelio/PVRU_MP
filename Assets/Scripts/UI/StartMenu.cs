using UnityEngine;
using UnityEngine.UI;
using Fusion.Addons.ConnectionManagerAddon;
using System.Collections;

public class StartMenu : MonoBehaviour
{
    [Header("Fusion Connection Manager")]
    public ConnectionManager connectionManager;

    [Header("UI Root (Prefab)")]
    public GameObject startMenuRoot;

    [Header("UI Elements")]
    public Toggle joinGameToggle;
    public Toggle quitGameToggle;

    private Canvas canvasComponent;
    private CanvasGroup canvasGroup;
    private Camera playerCamera;

    [Header("Fade Settings")]
    public float fadeDuration = 0.5f;

    private void Awake()
    {
        if (startMenuRoot != null)
        {
            canvasComponent = startMenuRoot.GetComponentInChildren<Canvas>();
            canvasGroup = startMenuRoot.GetComponentInChildren<CanvasGroup>();

            if (canvasGroup == null && canvasComponent != null)
                canvasGroup = canvasComponent.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (startMenuRoot != null)
        {
            startMenuRoot.SetActive(true);
            ShowCanvasInstant();
        }

        joinGameToggle.onValueChanged.AddListener(OnJoinGameToggled);
        quitGameToggle.onValueChanged.AddListener(OnQuitGameToggled);

        FindAndAttachToPlayerCamera();
    }

    private void LateUpdate()
    {
        if (playerCamera == null || (canvasComponent != null && canvasComponent.worldCamera != playerCamera))
        {
            FindAndAttachToPlayerCamera();
        }
    }

    private void FindAndAttachToPlayerCamera()
    {
        playerCamera = Camera.main;

        if (playerCamera != null && canvasComponent != null)
        {
            canvasComponent.worldCamera = playerCamera;
            canvasComponent.renderMode = RenderMode.WorldSpace;

            Transform camTransform = playerCamera.transform;
            canvasComponent.transform.SetParent(camTransform);
            canvasComponent.transform.localPosition = new Vector3(0f, 0f, 2f);
            canvasComponent.transform.localRotation = Quaternion.identity;
            canvasComponent.transform.localScale = Vector3.one * 0.002f; 
        }
    }

    private async void OnJoinGameToggled(bool isOn)
    {
        if (!isOn) return;
        Debug.Log("[StartMenu] Join Game Toggle clicked!");

        if (connectionManager != null)
        {
            joinGameToggle.interactable = false;

            StartCoroutine(FadeOutAndDisable());

            await connectionManager.Connect();
        }
        else
        {
            Debug.LogWarning("[StartMenu] ConnectionManager missing!");
        }
    }

    private void OnQuitGameToggled(bool isOn)
    {
        if (!isOn) return;
        Debug.Log("[StartMenu] Quit Game Toggle clicked!");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowCanvasInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private IEnumerator FadeOutAndDisable()
    {
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            canvasGroup.alpha = newAlpha;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return new WaitForEndOfFrame();
        startMenuRoot.SetActive(false);

        Debug.Log("[StartMenu] Deactivate Canvas");
    }
}
