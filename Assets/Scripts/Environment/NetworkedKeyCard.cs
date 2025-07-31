using UnityEngine;
using Fusion;
using Fusion.XR.Host.Grabbing;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkGrabbable))]
public class NetworkedKeyCard : NetworkBehaviour
{
    [Header("Keycard parameters")]
    [SerializeField] private string _keyID;
    private string defaultKeyID = "1234";

    [Header("Visual components")]
    public GameObject visualModel;
    public Collider keyCardCollider;

    [Header("Networked properties")]
    [Networked] public int KeyIDHash { get; set; }
    [Networked] public PlayerRef Holder { get; set; }

    protected NetworkGrabbable _grabbable;

    private static Dictionary<int, string> hashToStringMap = new Dictionary<int, string>();
    private string _currentKeyID;

    public string KeyID
    {
        get
        {
            //Get hash from map
            if (KeyIDHash != 0 && hashToStringMap.TryGetValue(KeyIDHash, out string storedValue))
            {
                return storedValue;
            }

            //Fallback if hash from map fails
            if (!string.IsNullOrEmpty(_currentKeyID))
            {
                return _currentKeyID;
            }

            //Final fallback to default
            return defaultKeyID;
        }
        private set
        {
            _currentKeyID = value;
            int hash = value.GetHashCode();
            KeyIDHash = hash;

            if (!hashToStringMap.ContainsKey(hash)) hashToStringMap[hash] = value;
        }
    }

    protected virtual void Awake()
    {
        _grabbable = GetComponent<NetworkGrabbable>();
    }

    public override void Spawned()
    {
        if (!string.IsNullOrEmpty(_keyID))
        {
            KeyID = _keyID;
        }
        else
        {
            Debug.LogWarning("This keycards keyID was not set before spawning, assigning default" + defaultKeyID);
            KeyID = defaultKeyID;
        }

        _grabbable.onDidGrab.AddListener(OnGrab);
        _grabbable.onDidUngrab.AddListener(OnRelease);

    }

    protected virtual void OnGrab(NetworkGrabber grabber)
    {
        if (grabber != null)
        {
            Holder = grabber.Object.InputAuthority;
            Debug.Log($"NetworkedKeyCard: picked up by player: {Holder}");

            if (NetworkedSoundManager.Instance != null)
            {
                NetworkedSoundManager.Instance.PlayEnvironmentSound("Keycard_Grabbed", transform.position);
            }
        }
    }

    protected virtual void OnRelease()
    {
        Debug.Log($"NetworkedKeyCard: Dropped by player {Holder}");
        Holder = PlayerRef.None;

            if (NetworkedSoundManager.Instance != null)
            {
                //TODO: Exchange Sound
                NetworkedSoundManager.Instance.PlayEnvironmentSound("Keycard_Grabbed", transform.position);
            }
    }

    public override void Render()
    {
        if (KeyIDHash != 0 && !hashToStringMap.ContainsKey(KeyIDHash))
        {
            if (!string.IsNullOrEmpty(_keyID))
            {
                hashToStringMap[KeyIDHash] = _keyID;
            }
        }
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        bool isHeld = Holder != PlayerRef.None;

        //if (keyCardCollider != null) keyCardCollider.enabled = !isHeld;
    }

    public void InsertInto(IKeyCardReceiver receiver)
    {
        if (receiver is NetworkBehaviour networkReceiver)
        {
            //Called by e.g. player when inserted into something
            RPC_InsertOnServer(Holder, networkReceiver.Object.Id);
        }
        else
        {
            Debug.LogError("KeyCard receiver must be a NetworkBehaviour to work with networked insertion");
        }

    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_InsertOnServer(PlayerRef user, NetworkId receiverId, RpcInfo info = default)
    {
        Debug.Log($"NetworkedKeyCard: RPC_InsertOnServer called by {info.Source}, user: {user}, holder: {Holder}");
        //Only holder of card can insert
        if (Holder != user)
        {
            Debug.LogWarning($"NetworkedKeyCard: Player {info.Source} tried to insert key card but is not holder. Current holder: {Holder}.");
            return;
        }
        var netObj = Runner.FindObject(receiverId);
        var rec = netObj?.GetComponent<IKeyCardReceiver>();
        if (rec == null) return;
        Debug.Log($"NetworkedKeyCard: Calling OnKeyCardInserted with key Id: {KeyID}");
        rec.OnKeyCardInserted(this);
        //Destroy or drop after we use it?
    }

    public void SetKeyID(string newKeyID)
    {
        if (Object.HasStateAuthority)
        {
            KeyID = newKeyID;
        }
        else
        {
            RPC_SetKeyID(newKeyID);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_SetKeyID(string newKeyID, RpcInfo info = default)
    {
        KeyID = newKeyID;
    }

    //utility method ti sync hash mappings
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncKeyIDMapping(int hash, string keyIDValue, RpcInfo info = default)
    {
        if (!hashToStringMap.ContainsKey(hash))
        {
            hashToStringMap[hash] = keyIDValue;
        }
    }

    public void EnsureKeyIDSynced()
    {
        if (Object.HasStateAuthority && KeyIDHash != 0)
        {
            RPC_SyncKeyIDMapping(KeyIDHash, KeyID);
        }
    }

    //For reference
    // Set a keycard ID (server authority)
    //keycard.SetKeyID("AccessCard_Level3");

    // Get the keycard ID (works on all clients)
    //string id = keycard.KeyID;

    // Ensure sync when spawning (call from server)
    //keycard.EnsureKeyIDSynced();
}
