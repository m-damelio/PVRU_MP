using UnityEngine;
using System.Collections;

public class openDoor : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float duration;
    public float openStart;
    private Vector3 scaleChange;

    public GameObject door_left;
    public GameObject door_right;

    private bool isOpen;

    void Start()
    {
        duration = 10.0f;
        openStart = 0.0f;
        scaleChange = new Vector3(-0.001f, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator Open()
    {
        Vector3 initialLeft = door_left.transform.localScale;
        Vector3 initialRight = door_right.transform.localScale;

        Vector3 target = new Vector3(0.001f, 1.0f, 1.0f);

        openStart = 0.0f;
        while (openStart < duration)
        {
            door_left.transform.localScale = Vector3.Lerp(initialLeft, target, openStart / duration);
            door_right.transform.localScale = Vector3.Lerp(initialLeft, target, openStart / duration);
            openStart += Time.deltaTime;
            yield return null;
        }

        door_left.transform.localScale = target;
        door_right.transform.localScale = target;
    }
}
