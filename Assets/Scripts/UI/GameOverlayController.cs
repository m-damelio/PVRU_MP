using UnityEngine;
using System.Collections;

public class GameOverOverlayController : MonoBehaviour
{
    [SerializeField] private Canvas gameOverCanvas;
    [SerializeField] private float autoHideDelay = 5f; 

    private Coroutine hideRoutine;

    private void Start()
    {
        if (gameOverCanvas != null)
            gameOverCanvas.enabled = false;
    }

    [ContextMenu("Test ShowGameOver()")]
    public void ShowGameOver()
    {
        if (gameOverCanvas == null) return;

        gameOverCanvas.enabled = true;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void HideGameOver()
    {
        if (gameOverCanvas != null)
            gameOverCanvas.enabled = false;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HideGameOver();
    }
}
