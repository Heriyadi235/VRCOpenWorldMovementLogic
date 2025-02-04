﻿
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZHK_OWML_Player_Debugger : UdonSharpBehaviour
{
    public Text DebugText;
    public ZHK_OWML_Station StationBase;
    public Transform LookAtBase;
    private VRCPlayerApi localPlayer;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (DebugText == null || StationBase == null)
        {
            gameObject.SetActive(false);
        }
    }
    void Update()
    {
        if (StationBase.PlayerID == localPlayer.playerId)
        {
            gameObject.SetActive(false);
        }
        if (LookAtBase != null)
        {
            LookAtBase.LookAt(localPlayer.GetPosition());
        }
        if (DebugText != null)
        {
            string output = "";
            output += StationBase.name;
            output += StationBase.Player!=null ? StationBase.Player.displayName : "NULL";
            output += "\n x:" + StationBase.CurrentPlayerPosition.x;
            output += "\n y:" + StationBase.CurrentPlayerPosition.y;
            output += "\n z:" + StationBase.CurrentPlayerPosition.z;
            output += "\n inVehicle:" + StationBase.inVehicle;
            output += "\n PlayerID:" + StationBase.PlayerID;
            output += "\n DoIOwn?: " + Networking.IsOwner(Networking.LocalPlayer, StationBase.gameObject);

            DebugText.text = output;
        }
    }
}
