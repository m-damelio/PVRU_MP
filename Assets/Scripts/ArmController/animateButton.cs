using UnityEngine;
using Fusion;

public class animateButton : NetworkBehaviour, ILevelResettable
{

    [SerializeField] private Transform buttonTop;
    public Vector3 originalPos;
    public float bounceDistance;
    public float bounceDuration;

    [Header("Networked Settings")]
    [Networked] public bool IsPressed { get; set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SetInitialState();
        }
    }

    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            if (buttonTop != null)
            {
                originalPos = buttonTop.position;
            }
            else
            {
                originalPos = transform.GetChild(0).transform.position;
            }
            IsPressed = false;
            
            bounceDistance = 0.02f;
            bounceDuration = 0.1f;
        }
    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsPressed = false;
            originalPos = transform.GetChild(0).transform.position;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (Object == null) return;
        if (!Object.HasStateAuthority || IsPressed) return;

        RPC_StartPress();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartPress()
    {
        StartCoroutine(ButtonFeedbackVisual());
        if (NetworkedSoundManager.Instance != null)
        {
            NetworkedSoundManager.Instance.PlayEnvironmentSound("Button_Clicking", transform.position);
        }
    }


    System.Collections.IEnumerator ButtonFeedbackVisual()
    {
        //make the inner cube of the button smaller for "pressing" visually the button down
        IsPressed = true;
        Transform buttonCT;
        if (buttonTop != null)
        {
            buttonCT = buttonTop.transform;
        }
        else
        {
            buttonCT = transform.GetChild(0).transform;
        }
            

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

        IsPressed = false;
    }
}
