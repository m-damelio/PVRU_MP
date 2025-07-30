using UnityEngine;

public class animateButton : MonoBehaviour
{

    public Vector3 originalPos;
    public float bounceDistance;
    public float bounceDuration;

    public bool isPressed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalPos = transform.GetChild(0).transform.position;
        bounceDistance = 0.02f;
        bounceDuration = 0.1f;
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void OnTriggerEnter(Collider other)
    {
        //DO some if other.gameObject.tag == 'hand' or something when hand is ready
        StartCoroutine(ButtonFeedbackVisual());
    }


    System.Collections.IEnumerator ButtonFeedbackVisual()
    {
        //make the inner cube of the button smaller for "pressing" visually the button down
        isPressed = true;
        var buttonCT = transform.GetChild(0).transform;

        Vector3 downPos = originalPos- buttonCT.transform.TransformDirection(Vector3.down) * bounceDistance;

        // Runter bewegen
        float elapsed = 0;
        while (elapsed < bounceDuration)
        {
            buttonCT.position = Vector3.Lerp(originalPos, downPos, elapsed / bounceDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonCT.position = downPos;

        // Wieder hoch
        elapsed = 0;
        while (elapsed < bounceDuration)
        {
            buttonCT.position = Vector3.Lerp(downPos, originalPos, elapsed / bounceDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonCT.position = originalPos;

        isPressed = false;
    }
}
