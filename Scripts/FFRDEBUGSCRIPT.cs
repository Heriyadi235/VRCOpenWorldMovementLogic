
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FFRDEBUGSCRIPT : UdonSharpBehaviour
{
public Text TextOutput;
public Text EventTextOutput;
[TextArea]public String debugOutput;
[System.NonSerializedAttribute][HideInInspector] public VRCPlayerApi localPlayer;
public Vector3 pos = new Vector3(0,0,0);
public Vector3 startPos;
public ZHK_UIScript UIScript;
public ZHK_OWML_Station[] Stations;
public SAV_SyncScript_OWML[] DebuggingSyncs;
public int checking = 0;
public int checking_syncs = 0;
private bool pressed = false;
public float distance = 0.1f;

// private void OnEnable()
// {
//     TextOutput.transform.position = startPos;
// }

void Start()
    {
        localPlayer = Networking.LocalPlayer;
        Debug.Log("Debug Script init");
        // startPos = TextOutput.rectTransform.position;
        EventTextOutput.text = "EventList";
    }
    void Update()
    {
        RectTransform textpos = TextOutput.rectTransform;
        if(Input.GetKeyDown(KeyCode.PageUp))
        {
            if (checking + 1 < Stations.Length)
            {
                checking = checking + 1;
            }
            else
            {
                checking = 0;
            }
            
            
        }
        if(Input.GetKeyDown(KeyCode.PageDown))
        {
            if (checking - 1 > -1)
            {
                checking = checking - 1;
            }
            else
            {
                checking = Stations.Length-1;
            }
            
            
        }
        
        // if(Input.GetKeyDown(KeyCode.Keypad8))
        // {
        //     if (checking_syncs + 1 < DebuggingSyncs.Length)
        //     {
        //         checking_syncs = checking_syncs + 1;
        //     }
        //     else
        //     {
        //         checking_syncs = 0;
        //     }
        //     
        //     
        // }
        // if(Input.GetKeyDown(KeyCode.Keypad7))
        // {
        //     if (checking_syncs - 1 > -1)
        //     {
        //         checking_syncs = checking_syncs - 1;
        //     }
        //     else
        //     {
        //         checking_syncs = DebuggingSyncs.Length-1;
        //     }
        //     
        //     
        // }
        //
        // if (Input.GetKey(KeyCode.Keypad9))
        // {
        //     TextOutput.transform.position =
        //         new Vector3(textpos.position.x, textpos.position.y + distance, textpos.position.z);
        // }
        //
        // if (Input.GetKey(KeyCode.Keypad6))
        // {
        //     TextOutput.transform.position =
        //         new Vector3(textpos.position.x, textpos.position.y - distance, textpos.position.z);
        // }
        //
        if(TextOutput!=null){
            // if(localPlayer!=null){
                // pos = localPlayer.GetPosition();
                if(localPlayer!=null){
                    pos = localPlayer.GetPosition();
                }
                string text = "X:" + pos.x + "\nY:" + pos.y + "\nZ:" + pos.z + "\n" +
                              "--DEBUG MENU - Press CTRL+ALT+O to hide--\n" +
                              "--Pageup/PageDown to cycle stations--\n" +
                              "Current Player ID: "+localPlayer.playerId;
                if (UIScript != null)
                {
                    string toAdd = "";
                    toAdd = toAdd + "\n[MAPOffset]:: " +
                            "\nx:" + UIScript.Map.position.x + " " +
                            "\ty:" + UIScript.Map.position.y + "  " +
                            "\tz:" + UIScript.Map.position.z + "\n";
                if (UIScript.stationObject != null)
                {
                    toAdd = toAdd + "\n[ZHKStation]:: "
                                  + "[CurrentPlayerMapPosition]" +
                                  "\nStationID:" + UIScript.stationObject.name +
                                  "\nx:" + UIScript.stationObject.CurrentPlayerPosition.x + " " +
                                  "\ty:" + UIScript.stationObject.CurrentPlayerPosition.y + "  " +
                                  "\tz:" + UIScript.stationObject.CurrentPlayerPosition.z + " " +
                                  "\nInVehicle?:" + UIScript.stationObject.inVehicle +
                                  "\nisMe??:" + UIScript.stationObject.isMe + "\n";
                } //check stations data even dont have station now
                        toAdd = toAdd + "\n[Checking]::"+checking+""
                                +"\n[SelectedStation]" +
                                "\nStationID:" + Stations[checking].name +
                                ((UIScript.stationObject != null) ? 
                                ("\nUIScriptStation:" + (UIScript.stationObject == Stations[checking] ? "Yes" : "No"))
                                :("No UIScriptStation"))+
                                "\nx:" + Stations[checking].CurrentPlayerPosition.x + " " +
                                "\ty:" + Stations[checking].CurrentPlayerPosition.y + "  " +
                                "\tz:" + Stations[checking].CurrentPlayerPosition.z  + "\n" +
                                "PlayerControllLocal: " + UIScript.PlayerManager.stationFlagLocal[checking] +
                                  "\nGameObjectActive" + Stations[checking].gameObject.activeSelf  +
                                  "\nInVehicle?:" + Stations[checking].inVehicle +
                                  "\nz:" + "isMe??:" + Stations[checking].isMe +
                                  "\nz:" + "playerid??:" + Stations[checking].PlayerID +
                                  "\n DoIOwn?: " + Networking.IsOwner(Networking.LocalPlayer, Stations[checking].gameObject) + 
                                  (Stations[checking].Player != null
                            ? "\nPlayer:" + Stations[checking].Player.displayName + "\n"
                            : "\nPlayer: None") + ""
                            + "\nSeated: " + Stations[checking].playerSet;
                        //toAdd = toAdd + "\n[PlayerWorldOffset]:: " +
                        //    "\nx:" + Stations[checking].oldoffset.x + " " +
                        //    "\ty:" + Stations[checking].oldoffset.y + "  " +
                        //    "\tz:" + Stations[checking].oldoffset.z + "\n"
                        ;
                
                    if (UIScript.OWML != null)
                    {
                        toAdd = toAdd + "\n [OWML] + " +
                                "\n PosSync:: \nx:" 
                                + UIScript.OWML.PosSync.x + 
                                " \ty:" + UIScript.OWML.PosSync.y + "  " +
                                "\tz:" + UIScript.OWML.PosSync.z +
                                "\n AnchorCoordinates:: " +
                                "\nx:[" + UIScript.OWML.AnchorCoordsPosition.x + "] " +
                                "\ty:[" +UIScript.OWML.AnchorCoordsPosition.y + "]  " +
                                "\tz:[" + UIScript.OWML.AnchorCoordsPosition.z + "]"+
                                "\n Moved:" + UIScript.OWML.moved+
                                "\n Distance To AC:\n" +
                                Vector3.Distance(UIScript.OWML.AnchorCoordsPosition,
                                    UIScript.OWML.VehicleRigidBody.transform.position);
                    }
                    text = text + toAdd;
                }
                text = text + "\n";

                debugOutput = text;
                TextOutput.text = text;
        }
    }
    public void Log(string logInfo)
    { 
        if(EventTextOutput!=null)
        {
            EventTextOutput.text = EventTextOutput.text + "\n";
            EventTextOutput.text = EventTextOutput.text + "[" + System.DateTime.Now.ToString()+ "]\t";
            EventTextOutput.text = EventTextOutput.text + logInfo;
        }
    }
}
