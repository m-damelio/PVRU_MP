using UnityEngine;
using UnityEngine.UI;
using Fusion.Addons.ConnectionManagerAddon;

public class StartMenu : MonoBehaviour
{
    [Header("Fusion Connection Manager")]
    public ConnectionManager connectionManager;

    [Header("UI Buttons")]
    public Toggle joinGameToggle;
    public Toggle quitGameToggle;

    private void Start()
    {
        joinGameToggle.onValueChanged.AddListener(OnJoinGameToggled);
        quitGameToggle.onValueChanged.AddListener(OnQuitGameToggled);
    }

    private async void OnJoinGameToggled(bool isOn)
    {
        if (!isOn) return;

        Debug.Log("Join Game Toggle clicked!");

        if (connectionManager != null)
        {
            joinGameToggle.interactable = false;
            await connectionManager.Connect();
            joinGameToggle.isOn = true;
        }
        else
        {
            Debug.LogWarning("ConnectionManager mssing!");
        }
    }

    private void OnQuitGameToggled(bool isOn)
    {
        if (!isOn) return;

        Debug.Log("Quit Game toggle clicked!");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
