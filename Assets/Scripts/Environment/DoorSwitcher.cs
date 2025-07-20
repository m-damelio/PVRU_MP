using UnityEngine;
using Fusion;
using System.Collections.Generic;
public class DoorSwitcher : DoorOpener
{
    [Header("Door Switcher Settings \n⚠️  IGNORE THE 'Door Controller' FIELD ABOVE - USE THE LIST BELOW")]
    [SerializeField] private List<DoorNetworkedController> doorControllerList = new List<DoorNetworkedController>();
    [SerializeField] private DoorSwitchMode switchMode = DoorSwitchMode.Toggle;

    public enum DoorSwitchMode
    {
        Toggle, //Toggle all door states 
        OpenAll, //open all doors
        CloseAll, //Close all doors
        Synchronize //Make all doors match first door state
    }

    void Awake()
    {
        if(doorControllerList.Count == 0)
        {
            Debug.LogError("No door to open assigned to" + this.gameObject.name);
        }
    } 

    public override void OnKeyCardInserted(NetworkedKeyCard card)
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

            OperateDoors();

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

    private void OperateDoors()
    {
        if(doorControllerList.Count == 0)
        {
            Debug.LogWarning("DoorSwitcher: No door controller to operate");
            return;
        }

        switch (switchMode)
        {
            case DoorSwitchMode.Toggle:
                foreach(DoorNetworkedController door in doorControllerList)
                {
                    door.RequestToggle();
                }
                break;
            case DoorSwitchMode.OpenAll:
                foreach(DoorNetworkedController door in doorControllerList)
                {
                    door.RequestOpen();
                }
                break;
            case DoorSwitchMode.CloseAll:
                foreach(DoorNetworkedController door in doorControllerList)
                {
                    door.RequestClose();
                }
                break;
            
            case DoorSwitchMode.Synchronize:
                bool firstDoorState = doorControllerList[0].IsOpen;
                bool targetState = !firstDoorState;
                foreach(DoorNetworkedController door in doorControllerList)
                {
                    if(targetState)
                        door.RequestOpen();
                    else
                        door.RequestClose();
                }
                break;

        }
    }

    [ContextMenu("Test Operate Doors")]
    public void TestOperateDoors()
    {
        OperateDoors();
    }

}