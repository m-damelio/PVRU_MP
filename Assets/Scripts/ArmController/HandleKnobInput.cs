using UnityEngine;
using UnityEngine.Jobs;

public class HandleKnobInput : MonoBehaviour
{
    [SerializeField]
    public GameObject knob1;
    public GameObject knob2;
    public GameObject knob3;
    public GameObject knob4;
    public GameObject button1;
    public GameObject button2;
    public GameObject button3;
    public GameObject button4;

    public Vector3[] originalPos = new Vector3[4];

    public int keycode;

    public bool isPressed;

    public float bounceDistance;
    public float bounceDuration;
   
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalPos[0] = button1.transform.GetChild(0).transform.position;
        Debug.Log(button1.transform.GetChild(0).name);
        originalPos[1] = button2.transform.GetChild(0).transform.position;
        originalPos[2] = button3.transform.GetChild(0).transform.position;
        originalPos[3] = button4.transform.GetChild(0).transform.position;

        bounceDistance = 0.02f;
        bounceDuration = 0.1f;
    }

    // Update is called once per frame
    void Update()
    {
        if (gameObject != null)
        {
            handleButtonInput();
            handleKnobInput();
        }
    }

    //handle button Input visually
    public void handleButtonInput()
    {
       
        if (Input.GetKeyDown(KeyCode.W))
        {
            StartCoroutine(ButtonFeedbackVisual(button1, 0));
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            StartCoroutine(ButtonFeedbackVisual(button2, 1));
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            StartCoroutine(ButtonFeedbackVisual(button3, 2));
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            StartCoroutine(ButtonFeedbackVisual(button4, 3));
        }


    }

    //for handle the knob input visually
    public void handleKnobInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ChangeRotationOfNob(knob1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ChangeRotationOfNob(knob2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ChangeRotationOfNob(knob3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ChangeRotationOfNob(knob4);
        }
    }

    public void ChangeRotationOfNob(GameObject knob)
    {
        var knobCT = knob.transform.GetChild(0).transform;
        knobCT.Rotate(Vector3.up * 300f * Time.deltaTime, Space.Self);
    }

    System.Collections.IEnumerator ButtonFeedbackVisual(GameObject b, int numberB)
    {
        //make the inner cube of the button smaller for "pressing" visually the button down
        isPressed = true;
        var buttonCT = b.transform.GetChild(0).transform;

        Vector3 downPos = originalPos[numberB] - buttonCT.transform.TransformDirection(Vector3.down) * bounceDistance;

        // Runter bewegen
        float elapsed = 0;
        while (elapsed < bounceDuration)
        {
            buttonCT.position = Vector3.Lerp(originalPos[numberB], downPos, elapsed / bounceDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonCT.position = downPos;

        // Wieder hoch
        elapsed = 0;
        while (elapsed < bounceDuration)
        {
                buttonCT.position = Vector3.Lerp(downPos, originalPos[numberB], elapsed / bounceDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonCT.position = originalPos[numberB];

        isPressed = false;
    }
}
