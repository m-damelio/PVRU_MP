using UnityEngine;
using Fusion;

public class Player : NetworkBehaviour
{
    [Networked] public float playerSpeed {get;set;}
    [SerializeField] private Ball _ballPrefab;
    [Networked] private TickTimer delay {get;set;}
    private Vector3 _forward = Vector3.forward;
    private NetworkCharacterController _ncc;

    private void Awake()
    {
        _ncc = GetComponent<NetworkCharacterController>();
    }
    public override void Spawned()
    {
        if(Object.HasStateAuthority)
        {  
            playerSpeed = 5.0f;
        }
    }
    public override void FixedUpdateNetwork() 
    {
        if(Object.HasInputAuthority == false) 
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
                //If button was pressed spawn ball
                if(data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
                {
                    delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    Runner.Spawn(_ballPrefab,
                    transform.position+_forward, Quaternion.LookRotation(_forward),
                    Object.InputAuthority, (runner, o) =>{
                        //Init ball before spawning it
                        o.GetComponent<Ball>().Init();
                    });
                }
            }
        }
    }
}
