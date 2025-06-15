using UnityEngine;

public class reflection : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public Transform originalObject;
    public Transform reflectedObject;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void OnTriggerEnter(Collider other)
    {
        if(other.tag == "laser") {
            reflectedObject.position = Vector3.Reflect(originalObject.position, Vector3.right);
        }
    }
}
