using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion.Addons.ConnectionManagerAddon;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    public ConnectionManager connectionManager;

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject selectionPanel;
    public GameObject settingsPanel;

    [Header("Settings UI")]
    public Color SoundActiveColor = Color.magenta;
    public Color SoundInactiveColor = Color.white;
    public TextMeshProUGUI soundText;
    public TextMeshProUGUI countDowntext;
    public Image[] soundBars;

    [Header("Sound Logic")]
    public int maxSteps = 10;
    public int currentStep = 10;
    private NetworkedSoundManager soundManager;

    private void Start()
    {
        //soundManager = NetworkedSoundManager.Instance;
        SetVolumeStep(maxSteps);
    }

    [ContextMenu("Trigger Connect")]
    public void Connect()
    {
        Debug.Log("Trigger Connect on connectionManager over UI");
        connectionManager.DoConnect();
    }

    [ContextMenu("Trigger Connect as Hacker")]

    public void ConnectAsHacker()
    {
        connectionManager.DoConnectAsHacker();
    }

    [ContextMenu("Trigger Connect as Hacker")]

    public void ConnectAsSneaker()
    {
        connectionManager.DoConnectAsSneaker();
    }

    [ContextMenu("Trigger ShowMainMenu")]
    public void ShowMainMenu()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        selectionPanel.SetActive(false);
    }

    [ContextMenu("Trigger ShowSettings")]
    public void ShowSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        UpdateVolumeUI();
    }

    public void ProceedToSelection()
    {
        if(countDowntext != null) countDowntext.text = "";
        mainPanel.SetActive(false);
        selectionPanel.SetActive(true);
    }

    [ContextMenu("Increase Volume +10%")]
    public void IncreaseVolume()
    {
        if (currentStep < maxSteps)
        {
            currentStep++;
            ApplyVolumeChange();
        }
    }

    [ContextMenu("Decrease Volume -10%")]
    public void DecreaseVolume()
    {
        if (currentStep > 0)
        {
            currentStep--;
            ApplyVolumeChange();
        }
    }

    private void ApplyVolumeChange()
    {
        float volumePercent = currentStep / (float)maxSteps; // 0–1
        // soundManager.RPC_SetMasterVolume(volumePercent);
        UpdateVolumeUI();
    }

    private void SetVolumeStep(int step)
    {
        currentStep = Mathf.Clamp(step, 0, maxSteps);
        ApplyVolumeChange();
    }

    private void UpdateVolumeUI()
    {
        if (soundText != null)
            soundText.text = $"Volume: {currentStep * 10}%";

        for (int i = 0; i < soundBars.Length; i++)
        {
            soundBars[i].color = (i < currentStep) ? SoundActiveColor : SoundInactiveColor;
        }
    }

    public void StartCountDown()
    {
        StartCoroutine(CountDownStart());
    }

    private IEnumerator CountDownStart()
    {
        if(countDowntext != null) countDowntext.text = "5";
        yield return new WaitForSeconds(1f);
        if(countDowntext != null) countDowntext.text = "4";
        yield return new WaitForSeconds(1f);
        if(countDowntext != null) countDowntext.text = "3";
        yield return new WaitForSeconds(1f);
        if(countDowntext != null) countDowntext.text = "2";
        yield return new WaitForSeconds(1f);
        if(countDowntext != null) countDowntext.text = "1";
        yield return new WaitForSeconds(1f);
        if(countDowntext != null) countDowntext.text = "...";
    }
    

    public void QuitGame()
    {
        Debug.Log("Quit Game triggered");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

}
