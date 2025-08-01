using UnityEngine;

public class colorControl : MonoBehaviour
{
  
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetSelectionColor(bool state, GameObject obj)
    {
        var color = new Color(255, 0, 255, 255);
        obj.GetComponentInChildren<Renderer>().material.color = state ? color : Color.white;
    }
}
