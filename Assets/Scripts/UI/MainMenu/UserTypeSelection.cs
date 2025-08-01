using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Fusion;

public class UserTypeSelection : NetworkBehaviour
{
    public enum UserType { None, Hacker, Sneaker }

    [Networked]
    public UserType currentUserType { get; set; } = UserType.None;

    [Header("UI Elements - Front")]
    public Button hackerButton;
    public Button sneakerButton;
    public TextMeshProUGUI countdownText;
    public Canvas selectionCanvas;

    [Header("UI Elements - Back Wall")]
    public Canvas leftBackCanvas;
    public Canvas rightBackCanvas;
    public TextMeshProUGUI leftBackText;
    public TextMeshProUGUI rightBackText;
    public Button confirmButtonLeft;
    public Button confirmButtonRight;
    public Button goBackButtonLeft;
    public Button goBackButtonRight;

    [Header("Confirm Button Images")]
    public Image confirmImageLeft;
    public Image confirmImageRight;
    public Sprite hackerSprite;
    public Sprite sneakerSprite;

    [Header("Highlight Settings")]
    public Color activeColor = Color.green;
    public Color inactiveColor = Color.white;
    public float pulseSpeed = 2f;
    public float pulseScale = 1.1f;

    [Header("Countdown Settings")]
    public float countdownDuration = 5f;

    private RectTransform activeButtonTransform;
    private Vector3 originalScale;
    private Coroutine countdownCoroutine;
    private bool isSpawned = false;
    private UserType lastUserType = UserType.None;

    public override void Spawned()
    {
        if (leftBackCanvas != null) leftBackCanvas.gameObject.SetActive(false);
        if (rightBackCanvas != null) rightBackCanvas.gameObject.SetActive(false);

        UpdateUserTypeUI();
        isSpawned = true;

        hackerButton.onClick.AddListener(() => OnHackerButtonClicked());
        sneakerButton.onClick.AddListener(() => OnSneakerButtonClicked());
        goBackButtonLeft.onClick.AddListener(() => OnGoBackButtonClicked());
        goBackButtonRight.onClick.AddListener(() => OnGoBackButtonClicked());
        confirmButtonLeft.onClick.AddListener(() => OnConfirmButtonClicked());
        confirmButtonRight.onClick.AddListener(() => OnConfirmButtonClicked());

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isSpawned || !Object || !Runner) return;

        if (activeButtonTransform != null)
        {
            float scale = 1 + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1);
            activeButtonTransform.localScale = originalScale * scale;
        }

        if (lastUserType != currentUserType)
        {
            lastUserType = currentUserType;
            UpdateUserTypeUI();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SelectUserType(UserType selectedType)
    {
        if (!isSpawned) return;

        if (currentUserType != UserType.None)
        {
            Debug.Log($"[{Runner.LocalPlayer}] choosed: {currentUserType}");
        }
        else
        {
            currentUserType = (selectedType == UserType.Hacker) ? UserType.Sneaker : UserType.Hacker;
            Debug.Log($"[{Runner.LocalPlayer}] automatically set as {currentUserType}.");
        }

        hackerButton.interactable = false;
        sneakerButton.interactable = false;

        if (Runner.IsServer)
            RPC_StartCountdown();
    }

    private void UpdateUserTypeUI()
    {
        if (!isSpawned || !Object || !Runner) return;

        hackerButton.image.color = (currentUserType == UserType.Hacker) ? activeColor : inactiveColor;
        sneakerButton.image.color = (currentUserType == UserType.Sneaker) ? activeColor : inactiveColor;

        if (currentUserType == UserType.Hacker)
            SetActiveButton(hackerButton);
        else if (currentUserType == UserType.Sneaker)
            SetActiveButton(sneakerButton);
        else
            activeButtonTransform = null;
    }

    private void SetActiveButton(Button button)
    {
        activeButtonTransform = button.GetComponent<RectTransform>();
        originalScale = activeButtonTransform.localScale;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_StartCountdown()
    {
        if (!isSpawned) return;

        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);

        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        countdownText.gameObject.SetActive(true);

        float timeLeft = countdownDuration;
        while (timeLeft > 0)
        {
            countdownText.text = $"{Mathf.CeilToInt(timeLeft)}...";
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }

        countdownText.text = "";
        yield return new WaitForSeconds(0.5f);

        selectionCanvas.gameObject.SetActive(false);
        ActivateBackCanvases();
    }

    private void ActivateBackCanvases()
    {
        leftBackCanvas.gameObject.SetActive(true);
        rightBackCanvas.gameObject.SetActive(true);

        string userTypeText = $"You are: {currentUserType}";
        leftBackText.text = userTypeText;
        rightBackText.text = userTypeText;

        Sprite selectedSprite = (currentUserType == UserType.Hacker) ? hackerSprite : sneakerSprite;
        confirmImageLeft.sprite = selectedSprite;
        confirmImageRight.sprite = selectedSprite;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_GoBack()
    {
        if (!isSpawned) return;

        currentUserType = UserType.None;
        countdownText.gameObject.SetActive(false);

        leftBackCanvas.gameObject.SetActive(false);
        rightBackCanvas.gameObject.SetActive(false);
        selectionCanvas.gameObject.SetActive(true);

        hackerButton.interactable = true;
        sneakerButton.interactable = true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_Confirm()
    {
        if (!isSpawned) return;

        confirmButtonLeft.gameObject.SetActive(false);
        confirmButtonRight.gameObject.SetActive(false);
        goBackButtonLeft.gameObject.SetActive(false);
        goBackButtonRight.gameObject.SetActive(false);
        confirmImageLeft.gameObject.SetActive(false);
        confirmImageRight.gameObject.SetActive(false);

        leftBackText.text = "You are ready to escape";
        rightBackText.text = "You are ready to escape";
    }

    [ContextMenu("Test Hacker")]
    public void OnHackerButtonClicked()
    {
        if (currentUserType == UserType.None)
        {
            currentUserType = UserType.Hacker;
            UpdateUserTypeUI();
        }
        RPC_SelectUserType(UserType.Hacker);
    }

    [ContextMenu("Test Sneaker")]
    public void OnSneakerButtonClicked()
    {
        if (currentUserType == UserType.None)
        {
            currentUserType = UserType.Sneaker;
            UpdateUserTypeUI();
        }
        RPC_SelectUserType(UserType.Sneaker);
    }

    [ContextMenu("Test Go-Back Button")]
    public void OnGoBackButtonClicked()
    {
        RPC_GoBack();
    }

    [ContextMenu("Test Confirm Button")]
    public void OnConfirmButtonClicked()
    {
        RPC_Confirm();
    }
}
