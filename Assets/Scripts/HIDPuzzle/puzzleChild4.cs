using UnityEngine;

public class puzzleChild4 : MonoBehaviour
{
    public bool selected = false;
    public int code4;




    public Material defaultMaterial;
    public Material material1;
    public Material material2;
    public Material material3;
    public Material material4;
    



    public puzzleController parentController;
    private Renderer objectRenderer;
    private int currentMaterialIndex = 0;




    void Start()
    {
        objectRenderer = GetComponent<Renderer>();

        if (parentController == null)
        {
            Transform parentTransform = transform.parent;
            if (parentTransform != null)
            {
                parentController = parentTransform.GetComponent<puzzleController>();
            }
        }

        if (defaultMaterial == null && objectRenderer != null)
        {
            defaultMaterial = objectRenderer.material;
        }
    }
    
    void Update()
    {
        if (parentController != null && parentController.selected)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                selected = !selected;
                
                if (selected)
                {
                    Debug.Log("F selected");
                }
                else
                {
                    ResetToDefaultMaterial();
                    Debug.Log("F deselected");
                }
            }
            
            if (selected)
            {
                HandleNumberKeyInput();
            }
        }
        else
        {
            if (selected)
            {
                selected = false;
            }
        }
    }
    
    void HandleNumberKeyInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            code4 = 1;
            ChangeMaterialByNumber(1);
            selected = false;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            code4 = 2;
            ChangeMaterialByNumber(2);
            selected = false;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            code4 = 3;
            ChangeMaterialByNumber(3);
            selected = false;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            code4 = 4;
            ChangeMaterialByNumber(4);
            selected = false;
        }
    }

    void ChangeMaterialByNumber(int number)
    {
        if (objectRenderer != null)
        {
            Material materialToUse = null;
            
            switch (number)
            {
                case 1:
                    materialToUse = material1;
                    currentMaterialIndex = 1;
                    break;
                case 2:
                    materialToUse = material2;
                    currentMaterialIndex = 2;
                    break;
                case 3:
                    materialToUse = material3;
                    currentMaterialIndex = 3;
                    break;
                case 4:
                    materialToUse = material4;
                    currentMaterialIndex = 4;
                    break;
            }
            
            if (materialToUse != null)
            {
                objectRenderer.material = materialToUse;
            }
            else
            {
                Debug.LogWarning($"{number} not assigned");
            }
        }
    }
    
    void ResetToDefaultMaterial()
    {
        if (objectRenderer != null && defaultMaterial != null)
        {
            objectRenderer.material = defaultMaterial;
            currentMaterialIndex = 0;
        }
    }
    
    public int GetCurrentMaterialIndex()
    {
        return currentMaterialIndex;
    }
    
    public Material GetCurrentMaterial()
    {
        switch (currentMaterialIndex)
        {
            case 0: return defaultMaterial;
            case 1: return material1;
            case 2: return material2;
            case 3: return material3;
            case 4: return material4;
            default: return defaultMaterial;
        }
    }
}
