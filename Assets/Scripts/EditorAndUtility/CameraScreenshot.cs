using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

public class CameraScreenshot : MonoBehaviour
{
    [Header("Screenshot Settings")]
    public Camera targetCamera;                // The camera to capture from
    public string folderName = "Screenshots";  // Folder inside project root
    public string screenshotBaseName = "Screenshot"; // Base name for file

    private int screenshotCount = 0;
    private InputAction screenshotAction;

    void OnEnable()
    {
        // Create a new InputAction for the space key
        screenshotAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/space");
        screenshotAction.performed += ctx => TakeScreenshot();
        screenshotAction.Enable();
    }

    void OnDisable()
    {
        screenshotAction.Disable();
    }

    void TakeScreenshot()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("No camera assigned for screenshots!");
            return;
        }

        // Create folder path relative to project
        string folderPath = Path.Combine(Application.dataPath, folderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Create unique filename
        string fileName = $"{screenshotBaseName}_{screenshotCount}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        // Render to a texture
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        targetCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        targetCamera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenShot.Apply();

        // Reset
        targetCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // Save as PNG
        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);

        Debug.Log($"Screenshot saved to: {fullPath}");

        screenshotCount++;
    }
}
