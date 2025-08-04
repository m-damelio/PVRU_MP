using UnityEngine;
using UnityEngine.AI;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public enum GuardState {Patrol, Alert, RunToAlarm, Rest, Return}

[RequireComponent(typeof(Animator), typeof(NavMeshAgent), typeof(AnimatorStateSync))]
public class GuardNetworkedController : NetworkBehaviour, ILevelResettable
{
    [Header("Pathway settings")]
    [SerializeField] private List<Transform> patrolPoints;
    [SerializeField] private Transform alarmSpot;
    [SerializeField] private float restDuration = 10f;
    [SerializeField] private NetworkedAlarmBooth alarmBooth;
    [SerializeField] private float distanceToCenter = 1f;

    [Header("Character Settings")]
    //[SerializeField] private float moveSpeed; 

    [Header("Fusion synced")]
    [Networked] public bool IsAlert { get; set; }
    [Networked] public bool IsAlarmRunning { get; set; }
    [Networked] public bool ResetCalled { get; set; }
    [Networked] public GuardState State { get; set; }

    [Header("Initial State")]
    [SerializeField] private Vector3 initialPosition;
    [SerializeField] private Quaternion initialRotation;

    private Animator _animator;
    private NavMeshAgent _agent;
    private AnimatorStateSync _animatorSync;
    private int _patrolIndex = 0;
    private GuardState _previousState = GuardState.Patrol;
    private bool _alertCoroutineStarted = false;
    private bool _restCoroutineStarted = false;
    [HideInInspector] public bool IsSpawnedAndValid => Object != null && Object.IsValid;

    private Coroutine _alertCoroutine;
    private Coroutine _restCoroutine;
    private Coroutine _resetCoroutine;

    private Coroutine _restartLevelCoroutine;
    public override void Spawned()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();
        if (_agent != null) alarmSpot.position = new Vector3(alarmSpot.position.x, alarmSpot.position.y + _agent.baseOffset, alarmSpot.position.z);

        if (Object.HasStateAuthority)
        {
            _animatorSync.NetworkTrigger("IsSpawned");
            _restartLevelCoroutine = null;
        }
    }

    public void SetInitialState()
    {
        if (!Object.HasStateAuthority) return;

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        _restartLevelCoroutine = null;
    }

    public void ResetToInitialState()
    {
        if (!Object.HasStateAuthority) return;

        //Stop coroutines if running
        if (_alertCoroutine != null) StopCoroutine(_alertCoroutine);
        if (_restCoroutine != null) StopCoroutine(_restCoroutine);
        if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);

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
        State = GuardState.Patrol;
        _previousState = GuardState.Patrol;
        _patrolIndex = 0;

        _alertCoroutineStarted = false;
        _restCoroutineStarted = false;

        //Wait a bit before allowing movement again
        ResetCalled = true;
        _resetCoroutine = StartCoroutine(DelayThen(_ =>
        {
            ResetCalled = false;
        }, 1f));

    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (ResetCalled) return;
        if (patrolPoints.Count == 0) return;

        switch (State)
        {
            case GuardState.Patrol:
                RunPatrol();
                break;

            case GuardState.Alert:
                if (!_alertCoroutineStarted)
                {
                    _alertCoroutineStarted = true;

                    //Stop Movement
                    _agent.ResetPath();
                    _agent.velocity = Vector3.zero;

                    _animatorSync.SetNetworkBool("IsAlert", true);

                    //For now a few seconds delay before returning to patroling
                    _alertCoroutine = StartCoroutine(DelayThen(_ =>
                    {
                        _alertCoroutineStarted = false;
                        _animatorSync.SetNetworkBool("IsAlert", false);

                        if (_previousState == GuardState.RunToAlarm)
                        {
                            State = GuardState.RunToAlarm;
                            _animatorSync.SetNetworkBool("IsAlarmRunning", true);
                        }
                        else
                        {
                            State = GuardState.Patrol;
                        }
                    }, 5f));
                }
                break;

            case GuardState.RunToAlarm:
                if (!IsAlarmRunning)
                {
                    _animatorSync.SetNetworkBool("IsAlarmRunning", true);
                    IsAlarmRunning = true;
                }
                _agent.destination = alarmSpot.position;
                _agent.speed = 5f;

                //Check if guard reached alarm spot 
                if (Vector3.Distance(transform.position, alarmSpot.position) < distanceToCenter)
                {
                    State = GuardState.Rest;
                }
                break;

            case GuardState.Rest:
                if (!_restCoroutineStarted)
                {
                    _restCoroutineStarted = true;
                    _animatorSync.NetworkTrigger("AtAlarmLocation");

                    _agent.ResetPath();
                    _agent.velocity = Vector3.zero;

                    _restCoroutine = StartCoroutine(DelayThen(_ =>
                    {
                        _animatorSync.NetworkTrigger("AlarmFixed");
                        if (alarmBooth != null)
                        {
                            alarmBooth.RPC_GuardResetAlarm();
                        }
                        State = GuardState.Return;
                        _restCoroutineStarted = false;
                        IsAlarmRunning = false;
                        _animatorSync.SetNetworkBool("IsAlarmRunning", false);
                    }, restDuration));
                }
                break;

            case GuardState.Return:
                _patrolIndex = FindClosestPatrolPoint();
                _agent.destination = patrolPoints[_patrolIndex].position;
                if (Vector3.Distance(transform.position, _agent.destination) < 0.2f) State = GuardState.Patrol;
                break;
        }

        //Sync booleans so clients update their animators
        IsAlert = (State == GuardState.Alert);
    }

    private void RunPatrol()
    {
        _agent.speed = 1f;
        _agent.destination = patrolPoints[_patrolIndex].position;
        if (Vector3.Distance(transform.position, _agent.destination) < 0.2f)
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
            if (dis < best)
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
        if (State != GuardState.Alert && State != GuardState.Rest)
        {
            _previousState = State; //remember what guard was doing (running to alarm/patrolling)
            State = GuardState.Alert;
            RPC_ShowGameOverForAll();
            if (_restartLevelCoroutine == null) _restartLevelCoroutine = StartCoroutine(RestartLevelAfterDelay(5f));
        }
        
    }

    //Called from alarm 
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TriggerAlarm()
    {
        if (State != GuardState.RunToAlarm && State != GuardState.Rest)
        {
            State = GuardState.RunToAlarm;
        }
    }

    //Helper to trigger an action after a delay
    private IEnumerator DelayThen(System.Action<object> action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action(null);
    }
    
    public void HandlePlayerDetected()
    {
        RPC_NotifyPlayerSpotted();

    }

    [ContextMenu("Test Player Detection Chain")]
    public void DebugTriggerPlayerDetection()
    {
        if (Object.HasStateAuthority)
        {
            Debug.Log("Triggering player detection chain");
            HandlePlayerDetected();
        }
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGameOverForAll()
    {
        GameOverOverlayController overlay = FindObjectOfType<GameOverOverlayController>();
        if (overlay != null)
        {
            overlay.ShowGameOver();
        }
    }

    private IEnumerator RestartLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        FindObjectOfType<LevelManager>()?.RestartCurrentLevel();
        _restartLevelCoroutine = null;
    }


}
