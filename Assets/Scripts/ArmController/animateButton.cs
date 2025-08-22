using UnityEngine;
using Fusion;

public class animateButton : NetworkBehaviour, ILevelResettable
{
    [Header("Networked Button settings")]
    [SerializeField] private NetworkedButton buttonController;
    [SerializeField] private Transform buttonTop;
    [SerializeField] private float coolDownTime = 5f;


    [Header("Animation Properties")]
    private Vector3 originalPos;
    public float bounceDistance;
    public float bounceDuration;

    [Header("Networked Properties")]
    [Networked] public TickTimer ActiveTimer { get; set; }
    [Networked] public bool OnCoolDown { get; set; }

    //private bool wasPressed = false;

    private Coroutine currentAnimation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SetInitialState();
        }
    }

    private void Awake()
    {
        if (buttonTop != null)
        {
            originalPos = buttonTop.position;

        }

        if (bounceDistance == 0) bounceDistance = 0.02f;
        if (bounceDuration == 0) bounceDuration = 0.1f;
    }

    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
         
            OnCoolDown = false;
            ActiveTimer = TickTimer.None;
            
        }
    }

    public void ResetToInitialState()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        if (Object.HasStateAuthority)
        {
            OnCoolDown = false;
            ActiveTimer = TickTimer.None;
            buttonTop.position = originalPos;
        }
        
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (ActiveTimer.IsRunning && ActiveTimer.Expired(Runner))
        {
            OnCoolDown = false;
            if (buttonController != null) buttonController.IsPressed = false;
            ActiveTimer = TickTimer.None;
        }

        OnCoolDown = ActiveTimer.IsRunning;
    }
    public void OnTriggerEnter(Collider other)
    {
        if (OnCoolDown)
        {
            Debug.Log("Button on cooldown, ignoring trigger");
            return;
        }  //If there is not supposed to be a cooldown skip the active timer check
        Debug.Log($"Trigger entered by: {other.gameObject.name}");
        if (Object == null)
        {
            Debug.Log("Object is null in OnTriggerEnter");
            return;
        }
        if (!Object.HasStateAuthority)
        {
            Debug.Log("No state authority in OnTriggerEnter, requesting press");
            RPC_RequestStartPress();
            return;
        }

        PressButton();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestStartPress()
    {
        if (!OnCoolDown)
        {
            PressButton();
        }
            
    }

    private void PressButton()
    {
        if (!Object.HasStateAuthority) return;

        ActiveTimer = TickTimer.CreateFromSeconds(Runner, coolDownTime);
        
        if (buttonController != null) buttonController.IsPressed = true;

        RPC_AnimateAndSound();
       
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnimateAndSound()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            buttonTop.position = originalPos;
        }
        currentAnimation = StartCoroutine(ButtonFeedbackVisual());
        if (NetworkedSoundManager.Instance != null)
        {
            NetworkedSoundManager.Instance.PlayEnvironmentSound("Button_Clicking", transform.position);
        }
    }


    System.Collections.IEnumerator ButtonFeedbackVisual()
    {
        //make the inner cube of the button smaller for "pressing" visually the button down
        Transform buttonCT;
        if (buttonTop != null)
        {
            buttonCT = buttonTop.transform;
        }
        else
        {
            yield break;
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
    }
}
