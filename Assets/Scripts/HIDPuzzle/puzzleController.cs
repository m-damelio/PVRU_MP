using UnityEngine;
using Fusion.XR.Host.Rig;

public class puzzleController : MonoBehaviour
{
    [Header("Puzzle Settings")]
    public bool selected = false;
    
    [Header("Materials")]
    public Material defaultMaterial;
    public Material selectedMaterial;
    
    [Header("Controller Detection")]
    public string leftControllerTag = "LeftController";
    
    public int code1;
    public int code2;
    public int code3;
    public int code4;

    private Renderer objectRenderer;
    private puzzleChild1 child1;
    private puzzleChild2 child2;
    private puzzleChild3 child3;
    private puzzleChild4 child4;
    
    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        
        if (defaultMaterial == null && objectRenderer != null)
        {
            defaultMaterial = objectRenderer.material;
        }
        
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("PuzzleController: No collider found! Adding BoxCollider.");
            gameObject.AddComponent<BoxCollider>().isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (child1 != null)
        {
            code1 = child1.code1;
        }

        if (child2 != null)
        {
            code2 = child2.code2;
        }

        if (child3 != null)
        {
            code3 = child3.code3;
        }

        if (child4 != null)
        {
            code4 = child4.code4;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsLeftController(other))
        {
            selected = true;
            if (selectedMaterial != null && objectRenderer != null)
            {
                objectRenderer.material = selectedMaterial;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsLeftController(other))
        {
            selected = false;
            if (defaultMaterial != null && objectRenderer != null)
            {
                objectRenderer.material = defaultMaterial;
            }
        }
    }
    
    private bool IsLeftController(Collider other)
    {
        XRControllerInputDevice parentXrController = other.GetComponentInParent<XRControllerInputDevice>();
        if (parentXrController != null && parentXrController.side == XRControllerInputDevice.ControllerSide.Left)
        {
            return true;
        }
        

        return false;
    }
}
