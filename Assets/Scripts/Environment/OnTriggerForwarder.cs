using UnityEngine;

public class OnTriggerForwarder : MonoBehaviour
{
    [SerializeField] private DoorOpener _forwardTo;

    private void OnTriggerEnter(Collider other)
    {
        if (_forwardTo != null)
        {
            _forwardTo.OnTriggerEnter(other);
        }
    }
}
