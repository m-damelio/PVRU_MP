using UnityEngine;
using System.Collections;

public class rotateLaser : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private int rotate;
    
    void Start()
    {
        rotate = -2;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            this.gameObject.transform.Rotate(0, +rotate, 0);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            this.gameObject.transform.Rotate(0, -rotate, 0);
        }
    }
}
