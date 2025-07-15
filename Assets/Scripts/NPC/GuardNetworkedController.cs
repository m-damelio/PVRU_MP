using UnityEngine;
using UnityEngine.AI;
using Fusion;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator), typeof(NavMeshAgent), typeof(AnimatorStateSync))]
public class GuardNetworkedController : NetworkBehaviour
{
    [Header("Pathway settings")]
    [SerializeField] private List<Transform> patrolPoints;
    [SerializeField] private Transform alarmSpot;
    [SerializeField] private float restDuration = 10f;
    [SerializeField] private NetworkedAlarmBooth alarmBooth;

    [Header("Character Settings")]
    //[SerializeField] private float moveSpeed; 

    [Header("Fusion synced")]
    [Networked] public bool IsAlert {get;set;}
    [Networked] public bool AlarmRunning {get;set;}

    private Animator _animator; 
    private NavMeshAgent _agent;
    private AnimatorStateSync _animatorSync;
    private int _patrolIndex = 0;
    private enum State {Patrol, Alert, RunToAlarm, Rest, Return}
    private State _state = State.Patrol;
    [HideInInspector] public bool IsSpawnedAndValid => Object != null && Object.IsValid;

    public override void Spawned()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();

        if(Object.HasStateAuthority)
        {
            _animatorSync.NetworkTrigger("IsSpawned");
        }
        
        ApplyAnimParams();
        
    }

    public override void FixedUpdateNetwork()
    {
        if(!Object.HasStateAuthority) return;
        if(patrolPoints.Count == 0) return;

        switch (_state)
        {
            case State.Patrol:
                RunPatrol();
                break;

            case State.Alert:
                //For now a few seconds delay before returning to patroling
                StartCoroutine(DelayThen(_ => _state = State.Patrol, 2f));
                _state = State.Patrol; //makes sure no re-enter happens next frame
                break;
            
            case State.RunToAlarm:
                _animatorSync.NetworkTrigger("AlarmTriggered");
                _agent.destination = alarmSpot.position;
                _state = State.Rest;
                break;
            
            case State.Rest:
                if(Vector3.Distance(transform.position, alarmSpot.position) < 0.5f)
                {
                    _animatorSync.NetworkTrigger("AtAlarmLocation");
                    StartCoroutine(DelayThen(_ => 
                    {
                        _animatorSync.NetworkTrigger("AlarmFixed");
                        if(alarmBooth != null)
                        {
                            alarmBooth.RPC_GuardResetAlarm();
                        }
                        _state = State.Return;
                    }, restDuration));
                }
                break;

            case State.Return:
                _patrolIndex = FindClosestPatrolPoint();
                _agent.destination = patrolPoints[_patrolIndex].position;
                if(Vector3.Distance(transform.position, _agent.destination) < 0.2f) _state = State.Patrol;
                break;
        }

        //Sync booleans so clients update their animators
        IsAlert = (_state == State.Alert);
        ApplyAnimParams();
    }

    private void RunPatrol()
    {
        _agent.destination = patrolPoints[_patrolIndex].position;
        if(Vector3.Distance(transform.position, _agent.destination) < 0.2f)
        {
            _patrolIndex = (_patrolIndex + 1) % patrolPoints.Count;
        }
    }

    private int FindClosestPatrolPoint()
    {
        int idx = 0;
        float best = float.MaxValue;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            float dis = Vector3.Distance(transform.position, patrolPoints[i].position);
            if(dis < best) 
            {
                best = dis;
                idx = i;
            }
        }
        return idx;
    }

    //Called from vision-cone logic on any client
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_NotifyPlayerSpotted()
    {
        if(_state == State.Patrol) _state = State.Alert;
    }

    //Called from alarm 
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TriggerAlarm()
    {
        if(_state != State.RunToAlarm && _state != State.Rest) _state = State.RunToAlarm;
    }

    private void ApplyAnimParams()
    {
        _animatorSync.SetNetworkBool("IsAlert", IsAlert);
    }

    //Helper to trigger an action after a delay
    private IEnumerator DelayThen(System.Action<object> action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action(null);
    }

}
