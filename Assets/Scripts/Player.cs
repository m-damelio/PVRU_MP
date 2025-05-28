using UnityEngine;
using Fusion;

public class Player : NetworkBehaviour
{
    [Networked] public float playerSpeed {get;set;}
    [SerializeField] private Ball _ballPrefab;
    [SerializeField] private PhysxBall _physxBallPrefab;
    [Networked] public bool spawnedProjectile {get;set;}

    private Color originalColor;
    private Material _material;

    [Networked] private TickTimer delay {get;set;}
    [Networked] private TickTimer colorTimer {get;set;}
    private Vector3 _forward = Vector3.forward;
    private NetworkCharacterController _ncc;
    private ChangeDetector _changeDetector;

    private void Awake()
    {
        _ncc = GetComponent<NetworkCharacterController>();

    }
    public override void Spawned()
    {
        _material = GetComponentInChildren<MeshRenderer>().material;
        originalColor= _material.color;
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if(Object.HasStateAuthority)
        {  
            playerSpeed = 5.0f;
            
        }
    }
    public override void FixedUpdateNetwork() 
    {
        //Client with state authority (aka host) processes movement and input 
        if(Object.HasStateAuthority == false) 
        {
            return;
        }
        
        
        //Get input over network
        if(GetInput(out NetworkInputData data))
        {
            //Move player
            data.direction.Normalize();
            _ncc.Move(playerSpeed * data.direction * Runner.DeltaTime);

            //Update which way forward is
            if(data.direction.sqrMagnitude >0)
            {
               _forward = data.direction;
            }
            //Check if timer has expired
            if(HasStateAuthority && delay.ExpiredOrNotRunning(Runner))
            {
                //If left button was pressed spawn ball projectile
                if(data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
                {
                    delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    colorTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    Runner.Spawn(_ballPrefab,
                    transform.position+_forward, Quaternion.LookRotation(_forward),
                    Object.InputAuthority, (runner, o) =>{
                        //Init ball before spawning it
                        o.GetComponent<Ball>().Init();
                    });
                    spawnedProjectile = !spawnedProjectile;
                }
                //If right button was pressed spawn ball with physics enabled
                else if(data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
                {
                    delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    colorTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    Runner.Spawn(_physxBallPrefab,
                    transform.position+_forward,
                    Quaternion.LookRotation(_forward),
                    Object.InputAuthority,
                    (runner, o) =>
                    {
                        o.GetComponent<PhysxBall>().Init(10*_forward);
                    });
                    spawnedProjectile = !spawnedProjectile;
                }
            }
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(spawnedProjectile):
                {
                    _material.color = Color.white;
                    break;
                }
            }
        }
        _material.color = Color.Lerp(_material.color, originalColor, Time.deltaTime);
    }
}
