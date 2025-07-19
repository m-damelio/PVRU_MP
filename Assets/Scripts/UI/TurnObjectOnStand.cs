using UnityEngine;
using System.Collections;

public class TurnObjectOnStand : MonoBehaviour
{   
    [SerializeField] private GameObject objectOfInterest;
    [SerializeField] private bool enableTurn;
    [SerializeField] private float turnDuration = 60f;
    [SerializeField] private float turnSpeed = 45f;

    private bool hasObjectToTurn = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Start()
    {
        if(objectOfInterest == null) return;
        if(objectOfInterest.transform.GetChild(0) == null) return;

        hasObjectToTurn = true;
        originalPosition = objectOfInterest.transform.position;
        originalRotation = objectOfInterest.transform.rotation;

        if(enableTurn)
        {
            StartCoroutine(StopObjectFromTurning());
        }
    }

    void Update()
    {
        if(hasObjectToTurn && enableTurn)
        {
            objectOfInterest.transform.Rotate(0, turnSpeed * Time.deltaTime, 0);
        }
    }

    private IEnumerator StopObjectFromTurning()
    {
        yield return new WaitForSeconds(turnDuration);
        
        enableTurn = false;
        objectOfInterest.transform.position = originalPosition;
        objectOfInterest.transform.rotation = originalRotation;
    }
}
