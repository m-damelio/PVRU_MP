using UnityEngine;
using Fusion.XR.Host.Grabbing;

public class StealableKeyCardGrabbable : NetworkPhysicsGrabbable
{
    private StealableKeyCard _stealableKeycard;

    protected override void Awake()
    {
        base.Awake();
        _stealableKeycard = GetComponent<StealableKeyCard>();
    }

    public override void Grab(NetworkGrabber newGrabber, GrabInfo newGrabInfo)
    {
        if (_stealableKeycard != null && _stealableKeycard.IsAttachedToGuard)
        {
            Debug.Log("Cannot grab keycard, it is attached to a guard. must be stolen");
            return;
        }

        base.Grab(newGrabber, newGrabInfo);
    }
}
