using UnityEngine;
using Fusion;
using System.Collections;

public class DoorOpener : NetworkBehaviour, IKeyCardReceiver
{
    [Header("Door Opener Settings")]
    [SerializeField] private string requireKeyID = "1234";
    [SerializeField] private DoorNetworkedController doorController;
    [SerializeField] private bool consumeKeyOnUse = false;
    [SerializeField] private float keyCardEjectDelay = 2f;

    [Header("Audio & Visual Feedback")]
    [SerializeField] private Light statusLight;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failureColor = Color.red;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip failureSound;
    [SerializeField] private AudioClip insertSound;

    [Header("Networked properties")]
    [Networked] public bool HasKeyCardInserted {get;set;}
    [Networked] public NetworkId InsertedKeyCardId {get;set;}

    void Awake()
    {
        if(doorController == null)
        {
            Debug.LogError("No door to open assigned to" + this.gameObject.name);
        }     
    }

    public void OnKeyCardInserted(NetworkedKeyCard card)
    {
        Debug.Log($"DoorOpener: OnKeyCardInserted called with card: {(card != null ? card.name : "NULL")}");
        if(!Object.HasStateAuthority) 
        {   
            Debug.Log("DoorOpener: Not state authority, ignoring insertion");
            return;
        }

        Debug.Log($"DoorOpener: Key card inserted with ID: {card.KeyID}");
        Debug.Log($"DoorOpner: Required Key ID: {requireKeyID}");

        if(insertSound != null)
        {
            RPC_PlaySound(insertSound.name);
        }

        if(card.KeyID == requireKeyID)
        {
            Debug.Log($"DoorOpener: Key ID matches, opening door ...");
            HasKeyCardInserted = true;
            InsertedKeyCardId = card.Object.Id;

            if(doorController!=null)
            {
                doorController.RequestOpen();
            }
            else
            {
                Debug.LogError("DoorOpener: No door controller assigned");
            }

            RPC_ShowFeedback(true);

            if(consumeKeyOnUse)
            {
                StartCoroutine(DestroyKeyCardAfterDelay(card, keyCardEjectDelay));
            }
            else
            {
                StartCoroutine(EjectKeyCardAfterDelay(card, keyCardEjectDelay));
            }
        }
        else
        {
            Debug.Log($"DoorOpener: Key ID '{card.KeyID}' doesn't mact required '{requireKeyID}'");

            RPC_ShowFeedback(false);

            StartCoroutine(EjectKeyCardAfterDelay(card, keyCardEjectDelay));
        }
    }

    private IEnumerator EjectKeyCardAfterDelay(NetworkedKeyCard card, float delay)
    {
        yield return new WaitForSeconds(delay);

        if(card != null && card.Object != null)
        {
            Debug.Log("DoorOpener: Eject key card");
            Vector3 ejectPosition = transform.position+ transform.forward *0.5f;
            Quaternion ejectRotation = transform.rotation;

            card.RPC_Drop(ejectPosition, ejectRotation);
        }
        HasKeyCardInserted = false;
        InsertedKeyCardId = default;
    }

    private IEnumerator DestroyKeyCardAfterDelay(NetworkedKeyCard card, float delay)
    {
        yield return new WaitForSeconds(delay);
        if(card != null && card.Object != null)
        {
            Debug.Log("DoorOpener: Destroying consumed key card");
            Runner.Despawn(card.Object);
        }
        HasKeyCardInserted = false;
        InsertedKeyCardId = default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ShowFeedback(bool success)
    {
        AudioClip soundToPlay = success ? successSound : failureSound;
        if(soundToPlay != null && audioSource!= null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
        if(statusLight != null)
        {
            statusLight.color = success ? successColor : failureColor;

            StartCoroutine(FlashLight());
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_PlaySound(string soundName)
    {
        if(audioSource != null && insertSound != null && insertSound.name == soundName)
        {
            audioSource.PlayOneShot(insertSound);
        }
    }

    private IEnumerator FlashLight()
    {
        if(statusLight == null) yield break;

        float originalIntensity = statusLight.intensity;
        Color originalColor = statusLight.color;

        statusLight.intensity = originalIntensity * 2f;
        yield return new WaitForSeconds(0.2f);

        statusLight.intensity = originalIntensity;
        yield return new WaitForSeconds(0.3f);

        statusLight.color = Color.white;
        statusLight.intensity = originalIntensity * 0.5f;
    }

    //utility for setting the required key at runtime
    public void SetRequiredKeyID(string newKeyID)
    {
        if(Object.HasStateAuthority)
        {
            requireKeyID = newKeyID;
            Debug.Log($"DoorOpener: Required key ID changed to: {newKeyID}");
        } 
        else
        {
            RPC_SetRequiredKeyID(newKeyID);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SetRequiredKeyID(string newKeyID)
    {
        requireKeyID = newKeyID;
        Debug.Log($"DoorOpener: Required key ID changed to: {newKeyID}");
    }

    [ContextMenu("Test Open Door")]
    public void TestOpenDoor()
    {
        if(doorController != null)
        {
            doorController.RequestOpen();
        }
    }

    void OnDrawGizmosSelected()
    {
        //Draws eject position in the editor
        Gizmos.color = Color.yellow;
        Vector3 ejectPos = transform.position + transform.forward * 0.5f;
        Gizmos.DrawWireSphere(ejectPos, 0.1f);
        Gizmos.DrawLine(transform.position, ejectPos);
    }
}
