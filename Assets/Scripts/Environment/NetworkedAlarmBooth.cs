using UnityEngine;
using Fusion;
using System.Collections;

public class NetworkedAlarmBooth : NetworkBehaviour, ILevelResettable
{
    [Header("Hack settings")]
    [SerializeField] private GameObject hackThis; //TODO how to hack this
    [SerializeField] private float hackDuration = 5f;
    [SerializeField] private float interactionRange = 2f;

    [Header("Guard")]
    [SerializeField] private GuardNetworkedController guardChecker;
    
    [Header("Visual & Audio settings")]
    [SerializeField] private Light alarmLight;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color alarmColor = Color.red;
    [SerializeField] private Light hackedStatusLight;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failureColor = Color.red;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip failureSound;
    [SerializeField] private AudioClip alarmSound;

    [Header("Networked Properties")]
    [Networked] public float HackingProgress {get;set;}
    [Networked] public PlayerRef HackingPlayer {get; set;}
    [Networked] public bool IsAlarmedActive {get;set;}
    [Networked] public bool IsHacked {get;set;}
    [Networked] public bool IsBeingHacked {get;set;}

    private enum AlarmState {Normal, Hacking, Hacked, Alarmed}
    private AlarmState _currentState = AlarmState.Normal;

    public enum SoundType {Success, Failure, Alarm}


    public override void Spawned()
    {
        UpdateVisuals();
    }

    public void SetInitialState()
    {
        //Nothing needs to be tracked here currently
    }

    public void ResetToInitialState()
    {
        if(Object.HasStateAuthority)
        {
            RPC_ResetAlarmBooth();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if(!Object.HasStateAuthority) return;

        if(IsBeingHacked && HackingPlayer != PlayerRef.None)
        {
            HackingProgress += Runner.DeltaTime;

            if(HackingProgress >= hackDuration)
            {
                CompleteHack();
            }
        }

        UpdateState();
    }

    public override void Render()
    {
        UpdateVisuals();
    }

    private void UpdateState()
    {
        if(IsHacked)
        {
            _currentState = AlarmState.Hacked;
        }
        else if(IsAlarmedActive)
        {
            _currentState = AlarmState.Alarmed;
        }
        else if(IsBeingHacked)
        {
            _currentState = AlarmState.Hacking;
        }
        else 
        {
            _currentState = AlarmState.Normal;
        }
    }

    private void UpdateVisuals()
    {
        if(alarmLight != null && hackedStatusLight != null)
        {
            switch (_currentState)
            {
                case AlarmState.Normal:
                    alarmLight.color = normalColor;
                    break;
                case AlarmState.Hacking:
                    float pulseValue = Mathf.PingPong(Time.time *2f, 1f);
                    alarmLight.color = Color.Lerp(normalColor, alarmColor, pulseValue);
                    break;
                case AlarmState.Hacked:
                    hackedStatusLight.color = successColor;
                    break;
                case AlarmState.Alarmed:
                    alarmLight.color = alarmColor;
                    break;
            }
        }
    }

    //Needs to be called by VRPlayer
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartHacking(PlayerRef player)
    {
        if(IsHacked || IsBeingHacked) return;
        HackingPlayer = player;
        IsBeingHacked = true;
        HackingProgress = 0f;
    }

    [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
    public void RPC_StopHacking()
    {
        if(!IsBeingHacked) return;
        IsBeingHacked = false;
        HackingProgress = 0f;
        HackingPlayer = PlayerRef.None;

        RPC_PlaySound(SoundType.Failure);
    }

    private void CompleteHack()
    {
        IsBeingHacked = false;
        IsHacked = true;
        HackingPlayer = PlayerRef.None;

        RPC_PlaySound(SoundType.Success);

        TriggerAlarmFromHack();
    }

    private void TriggerAlarmFromHack()
    {
        IsAlarmedActive = true;

        if(guardChecker != null)
        {
            guardChecker.RPC_TriggerAlarm();
        }

        RPC_PlaySound(SoundType.Alarm);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void RPC_GuardResetAlarm()
    {
        IsAlarmedActive = false;
        IsHacked = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlaySound(SoundType soundToPlay)
    {
        if(audioSource != null)
        {
            AudioClip clipToPlay = null;
            switch(soundToPlay)
            {
                case SoundType.Alarm:
                    clipToPlay = alarmSound;
                    break;
                case SoundType.Success:
                    clipToPlay = successSound;
                    break;
                case SoundType.Failure:
                    clipToPlay = failureSound;
                    break;
            }
            if(clipToPlay != null)
            {
                audioSource.clip = clipToPlay;
                audioSource.Play();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResetAlarmBooth()
    {
        if(Object.HasStateAuthority)
        {
            IsAlarmedActive = false;
            IsHacked = false;
            IsBeingHacked = false;
            HackingPlayer = PlayerRef.None;
            HackingProgress = 0f;
        }
    }

    //For debugging
    [ContextMenu("Test Alarm")]
    public void TestTriggerAlarm()
    {
        if(Runner!=null)
        {
            if(guardChecker != null)
            {
                guardChecker.RPC_TriggerAlarm();
            }
            
        }
    }
    //Utility function
    public bool IsPlayerInRange(Transform playerTransform)
    {
        return Vector3.Distance(transform.position, playerTransform.position) <=interactionRange;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
