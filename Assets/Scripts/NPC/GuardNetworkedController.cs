using UnityEngine;
using UnityEngine.AI;
using Fusion;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator), typeof(NavMeshAgent), typeof(AnimatorStateSync))]
public class GuardNetworkedController : NetworkBehaviour, ILevelResettable
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
    [Networked] public bool IsAlarmRunning {get;set;}
    [Networked] public bool ResetCalled {get; set;}

    [Header("Initial State")]
    [SerializeField] private Vector3 initialPosition;
    [SerializeField] private Quaternion initialRotation;

    private Animator _animator; 
    private NavMeshAgent _agent;
    private AnimatorStateSync _animatorSync;
    private int _patrolIndex = 0;
    private enum State {Patrol, Alert, RunToAlarm, Rest, Return}
    private State _state = State.Patrol;
    private State _previousState = State.Patrol;
    private bool _alertCoroutineStarted = false;
    private bool _restCoroutineStarted = false;
    [HideInInspector] public bool IsSpawnedAndValid => Object != null && Object.IsValid;

    private Coroutine _alertCoroutine;
    private Coroutine _restCoroutine;
    private Coroutine _resetCoroutine;

    public override void Spawned()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();

        if(Object.HasStateAuthority)
        {
            _animatorSync.NetworkTrigger("IsSpawned");
        }
    }

    public void SetInitialState()
    {
        if (!Object.HasStateAuthority) return;

        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void ResetToInitialState()
    {
        if(!Object.HasStateAuthority) return;

        //Stop coroutines if running
        if(_alertCoroutine !=null) StopCoroutine(_alertCoroutine);
        if(_restCoroutine != null) StopCoroutine(_restCoroutine);
        if(_resetCoroutine != null) StopCoroutine(_resetCoroutine);

        // Reset networked properties
        IsAlert = false;
        IsAlarmRunning = false;

        //NavMeshAgent reset
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;
        _agent.speed = 1f;

        //Transform reset
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        //animation state reset
        _animatorSync.SetNetworkBool("IsAlert", false);
        _animatorSync.SetNetworkBool("IsAlarmRunning", false);
        _animatorSync.NetworkTrigger("RestartAnimator");

        //Reset state
        _state = State.Patrol;
        _previousState = State.Patrol;
        _patrolIndex = 0;

        _alertCoroutineStarted = false;
        _restCoroutineStarted = false;

        //Wait a bit before allowing movement again
        ResetCalled = true;
        _resetCoroutine = StartCoroutine(DelayThen(_ => {
            ResetCalled = false; 
            }, 1f));

    }

    public override void FixedUpdateNetwork()
    {
        if(!Object.HasStateAuthority) return;
        if(ResetCalled) return;
        if(patrolPoints.Count == 0) return;

        switch (_state)
        {
            case State.Patrol:
                RunPatrol();
                break;

            case State.Alert:
                if(!_alertCoroutineStarted)
                {
                    _alertCoroutineStarted = true;

                    //Stop Movement
                    _agent.ResetPath();
                    _agent.velocity = Vector3.zero;
                    
                    _animatorSync.SetNetworkBool("IsAlert", true);

                    //For now a few seconds delay before returning to patroling
                    _alertCoroutine = StartCoroutine(DelayThen(_ => {
                        _alertCoroutineStarted = false; 
                        _animatorSync.SetNetworkBool("IsAlert", false);

                        if(_previousState == State.RunToAlarm)
                        {
                            _state = State.RunToAlarm;
                            _animatorSync.SetNetworkBool("IsAlarmRunning", true);
                        }
                        else
                        {
                            _state = State.Patrol;
                        }
                        }, 5f));
                }
                break;
            
            case State.RunToAlarm:
                if(!IsAlarmRunning)
                {
                    _animatorSync.SetNetworkBool("IsAlarmRunning", true);
                    IsAlarmRunning = true;
                }
                _agent.destination = alarmSpot.position;
                _agent.speed = 5f;

                //Check if guard reached alarm spot 
                if(Vector3.Distance(transform.position, alarmSpot.position) < 0.5f)
                {
                    _state = State.Rest;
                }
                break;
            
            case State.Rest:
                if(!_restCoroutineStarted)
                {
                    _restCoroutineStarted = true;
                    _animatorSync.NetworkTrigger("AtAlarmLocation");

                    _agent.ResetPath();
                    _agent.velocity = Vector3.zero;

                    _restCoroutine = StartCoroutine(DelayThen(_ => 
                    {
                        _animatorSync.NetworkTrigger("AlarmFixed");
                        if(alarmBooth != null)
                        {
                            alarmBooth.RPC_GuardResetAlarm();
                         }
                        _state = State.Return;
                        _restCoroutineStarted = false;
                        IsAlarmRunning = false;
                        _animatorSync.SetNetworkBool("IsAlarmRunning", false);
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
    }

    private void RunPatrol()
    {
        _agent.speed = 1f;
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
        //Fixing alarm state aka rest state will not be interrupted by vision so players can steal
        if(_state != State.Alert && _state != State.Rest) 
        {
            _previousState = _state; //remember what guard was doing (running to alarm/patrolling)
            _state = State.Alert;
        }
    }

    //Called from alarm 
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TriggerAlarm()
    {
        if(_state != State.RunToAlarm && _state != State.Rest) 
        {
            _state = State.RunToAlarm;
        }
    }

    //Helper to trigger an action after a delay
    private IEnumerator DelayThen(System.Action<object> action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action(null);
    }

}
