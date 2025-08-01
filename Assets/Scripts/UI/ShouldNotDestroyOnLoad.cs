using UnityEngine;

public class ShouldNotDestroyOnLoad : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this.transform.gameObject);
    }
}
