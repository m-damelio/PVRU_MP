using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private GameObject pauseMenuCanvas;
    [SerializeField] private float menuDistance = 0.5f;
    [SerializeField] private bool menuFollowsHead = false;

    [Header("Input Settings")]
    [SerializeField] private KeyCode testKey = KeyCode.Escape;
    [SerializeField] private OVRInput.Button vrPauseButton = OVRInput.Button.Start;

    private bool isPaused = false;
    private NetworkObject netObj;

    void Start()
    {
        netObj = GetComponent<NetworkObject>();

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.SetActive(false);
    }

    void Update()
    {
        if (netObj != null && !netObj.HasInputAuthority) return;

        // keyboard test
        if (Input.GetKeyDown(testKey))
        {
            TogglePauseMenu();
        }

        // quest button
        if (OVRInput.GetDown(vrPauseButton))
        {
            TogglePauseMenu();
        }
    }

    public void TogglePauseMenu()
    {
        isPaused = !isPaused;

        if (pauseMenuCanvas == null) return;

        pauseMenuCanvas.SetActive(isPaused);

        if (isPaused)
        {
            if (menuFollowsHead)
            {
                AttachMenuToHead();
            }
            else
            {
                PositionMenuInFrontOfPlayer();
            }

            Time.timeScale = 0f;
        }
        else
        {
            DetachMenu();
            Time.timeScale = 1f;
        }
    }

    private void PositionMenuInFrontOfPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        pauseMenuCanvas.transform.SetParent(null);

        Vector3 forward = cam.transform.forward;
        Vector3 menuPosition = cam.transform.position + forward.normalized * menuDistance;
        menuPosition.y = cam.transform.position.y;

        pauseMenuCanvas.transform.position = menuPosition;
        pauseMenuCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void AttachMenuToHead()
    {
        Camera cam = Camera.main;
        if (cam == null || pauseMenuCanvas == null) return;

        pauseMenuCanvas.transform.SetParent(cam.transform);
        pauseMenuCanvas.transform.localPosition = new Vector3(0, 0, menuDistance);
        pauseMenuCanvas.transform.localRotation = Quaternion.identity;
    }

    private void DetachMenu()
    {
        if (pauseMenuCanvas == null) return;

        pauseMenuCanvas.transform.SetParent(null);
    }


    public void ResumeGame()
    {
        isPaused = false;
        pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f;
        DetachMenu();
    }

    public void RestartPuzzle()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LeaveGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScene");
    }

    public void ResumeGameToggle(bool value)
    {
        if (value) ResumeGame();
    }

public void RestartPuzzleToggle(bool value)
    {
        if (value) RestartPuzzle();
    }

public void LeaveGameToggle(bool value)
    {
        if (value) LeaveGame();
    }
}
