using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

// ZHK Player Station
// Per station / player synchronization script for ZHK_OpenWorldMovementLogic
// For use with SaccFlight 1.5 Onwards & ZHK_OpenWorldMovementLogic
// Dependencies:
// ZHK_UIScript - Local Player Manager
// ZHK_OWML_Player - Global Player Manager script
// Contact: Twitter: @zzhako / Discord: ZhakamiZhako#2147
[DefaultExecutionOrder(11)] //after player controller
// TODO: Further Optimization & smoother player movement
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
//[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]


public class ZHK_OWML_Station : UdonSharpBehaviour
{
    
    //inspector
    [Tooltip("Required: Your Scene's UI Script / 必須 あなたのシーンのUIスクリプト")]
    public ZHK_UIScript UIScript;
    [Tooltip("A mesh or gameobject that's an indicator to let you know that this player's station is working or not. You may replace it with something else. \n \n " +
             "このプレイヤーのステーションが機能しているかどうかを知らせるためのインジケータとなるメッシュやゲームオブジェクトです。他のものに置き換えてもよい。")]
    public GameObject IndicatorDebug;
    [Tooltip("This station's VRCStation. Best to set this up on this very same gameobject. " +
             "\n \n このステーションのVRCStationです。この全く同じゲームオブジェクトに設定するのがベストです。")]
    public VRCStation stationObject;
    [Tooltip("Required: Your Scene's OWML PlayerController Gameobject. \n \n 必須 シーンの OWML PlayerController Gameobject。")]
    public ZHK_OWML_Player OWML_Player;


    //PlayerInfo
    [System.NonSerializedAttribute] public VRCPlayerApi Player;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(inVehicle))] public bool _inVehicle = false;
    public bool inVehicle
    {
        set
        {
            _inVehicle = value;
            Debug.Log(PlayerID + "In vehicle Called " + value, this);
            FFRDebug(PlayerID + "In vehicle Called " + value);
            if (Networking.IsOwner(gameObject) && UIScript.stationObject == this && !value)
            {
                stationObject.transform.position = Networking.LocalPlayer.GetPosition();
                stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
                FFRDebug("UseStation form inVehicle false");
                //stationObject.UseStation(Networking.LocalPlayer);
                SendCustomEventDelayedFrames(nameof(useSeat), 10);//use seat 1 second later avoid being kick out
            }
        }
        get => _inVehicle;
    }
    //[System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.None), FieldChangeCallback((nameof(PlayerID)))] public int _PlayerID = -1;
    [System.NonSerializedAttribute] [FieldChangeCallback((nameof(PlayerID)))] public int _PlayerID = -1; //改为不同步，根据player controller 赋值
    public int PlayerID
    {
        set
        {
            _PlayerID = value;
            if (Networking.LocalPlayer.playerId != value && value != -1 && IndicatorDebug != null && UIScript.showDebugPlayerPos) { IndicatorDebug.SetActive(true); }
            else
            {
                if (IndicatorDebug != null) IndicatorDebug.SetActive(false);
            }
        }
        get => _PlayerID;
    }
    [HideInInspector] public bool isMe = false;
    [HideInInspector] [UdonSynced(UdonSyncMode.None)] public bool playerSet = false; //玩家已经坐在椅子上了(差不多可靠了，但是还没应用上)

    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.None)] public Vector3 CurrentPlayerPosition = Vector3.zero;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.None)] public Quaternion CurrentPlayerRotation;
    public Vector3 oldPos = Vector3.zero;
    private Quaternion oldRot;
    
    private float timerPlayerUpdate = 0f;
    private float distanceFromCenter = 0f;
    private Vector3 PlayerPosition = Vector3.zero;
    private Vector3 TemporaryVelocity = Vector3.zero;
    
    private bool nextFrame = false;
    private float timeoutTimer = 0f;

    public int stationIndex = 0;

    //[System.NonSerializedAttribute] [UdonSynced] public Vector3 oldoffset = Vector3.zero; //存储了这个站的坐标
    //[System.NonSerializedAttribute] [UdonSynced] public bool mapoffsetUpdateFlag = false;
    //[System.NonSerializedAttribute] [UdonSynced] public bool playerNeedAssign = false;

    //[VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */
    //public VRCPlayerApi LocalPlayer;
    void Start()
    {
        UIScript = OWML_Player.UIScript;
        //oldoffset = UIScript.Map.transform.position;
        transform.position = Vector3.zero;
        stationObject.PlayerMobility = VRCStation.Mobility.Immobilize;
        stationObject.canUseStationFromStation = true;
        stationObject.disableStationExit = true;
        FFRDebug("Station" + stationIndex + "ready");
    }

    public void OnEnable()
    {
        FFRDebug(gameObject.name + "station enable");
        //register();
    }

    public void OnDisable()
    {
        FFRDebug(gameObject.name + "station disable");
        //unregister();
    }

    public void register()
    {
        FFRDebug("STATION REGISTER" + gameObject.name);
        if (PlayerID == Networking.LocalPlayer.playerId) //被分配到的玩家执行这里
        {
            FFRDebug("STATION REGISTER OWNER" + PlayerID + "TO" + gameObject.name);
            Player = Networking.LocalPlayer;
            UIScript.stationObject = this;
            isMe = true;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            SendCustomEventDelayedSeconds(nameof(useSeat),1);//延迟1s以等待ownerset生效
        }
        else
        {
            FFRDebug("STATION REGISTER NONOWNER "+ PlayerID +"TO"+  gameObject.name);
            Player = VRCPlayerApi.GetPlayerById(PlayerID);
            isMe = false;
            stationObject.PlayerMobility = VRCStation.Mobility.Immobilize;
        }
    }
    
    public void unregister()
    {
        FFRDebug("STATION UNREGISTER" + gameObject.name);
        Debug.Log((PlayerID == -1)? "Clearing" : "UNREGISTERING");
        inVehicle = false;
        CurrentPlayerPosition = Vector3.zero;
        Player = null;
        PlayerID = -1;
        playerSet = false;
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        //if (Networking.IsOwner(gameObject))//wrong occupitied seems from here
        if (Networking.IsOwner(gameObject) && PlayerID == Networking.LocalPlayer.playerId)
        {
            FFRDebug("On Player local respawn");
            inVehicle = false;
            RequestSerialization();
            useSeat(); // Ey, quick fix
        }
    }

    public void useSeat()
    {
        FFRDebug("On useSeat" + Networking.GetOwner(gameObject).displayName + "[" + Networking.GetOwner(gameObject).playerId + "]");
        if (Player != null)
        {
            if (Player.isLocal)
            {
                TemporaryVelocity = Networking.LocalPlayer.GetVelocity();
                stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
                stationObject.UseStation(Networking.LocalPlayer);
            }
        }
        else
            FFRDebug("Player is none, useSeat call failed"); //useful fixs script halted on second joiner 
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        //这里与Playercontroller的功能有重复
        if (player.playerId == PlayerID)
        {
            //PlayerID = -1; //Station 不应该自己更改PlayerID，这是一个例外
            playerSet = false;
        }
    }
    
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        
    //    Debug.Log("!!!!!!!! ON OWNERSHIP TRANSFERRED CALL");
        //可能来到这的情况
        //1.被分配了座椅
        //2.玩家离开了
        //FFRDebug("Station OwnerChanged!");
        //if (PlayerID != -1)//after assigned the playerid has been synced to every player
        //    Player = VRCPlayerApi.GetPlayerById(PlayerID);
        //    else
        //        gameObject.SetActive(false);

        //    //Player = Networking.GetOwner(gameObject)
        //    if (playerNeedAssign && PlayerID != -1)
        //    {
        //        if (Networking.IsOwner(gameObject))
        //        {
        //            Debug.Log("registe player from OnOwnershipTransferred");
        //            FFRDebug("registe player from OnOwnershipTransferred");
        //            gameObject.SetActive(true);
        //            Player = VRCPlayerApi.GetPlayerById(PlayerID);
        //            UIScript.stationObject = this;
        //            isMe = true;

        //            PlayerID = Networking.LocalPlayer.playerId; //see if id will change after
        //            SendCustomEventDelayedSeconds(nameof(useSeat), 2);
        //        }
        //        else//不是房主，仅仅是被通知到了座椅分配
        //        {
        //            Debug.Log("OnOwnershipcall but not owner");
        //            FFRDebug("OnOwnershipcall but not owner");
        //            gameObject.SetActive(true);
        //            Player = VRCPlayerApi.GetPlayerById(PlayerID);
        //            isMe = false;
        //        }
        //        playerNeedAssign = false;
        //    }
        //    else //座椅的玩家离开或者房主更换了
        //    {
        //        unregister();
        //    }
    }

    public override void OnStationEntered(VRCPlayerApi player) // station enter logic
    {
        Debug.Log(player.displayName + "-" + player.playerId + " Entered Station" + gameObject.name);
        FFRDebug(player.displayName + "-" + player.playerId + " Entered Station" + gameObject.name);
        playerSet = true;
        if (!player.isLocal)
        {
            stationObject.PlayerMobility = VRCStation.Mobility.Immobilize;
        }

        if (player.isLocal)
        {
            stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
            Networking.LocalPlayer.SetVelocity(TemporaryVelocity);
        }
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        //从驾驶位下来也会触发OnStationExited，导致nextFrame不可靠
        //Entered a vehicle or something else.
        Debug.Log(player.displayName + "-" + player.playerId + " Left Station" + gameObject.name);
        FFRDebug(player.displayName + "-" + player.playerId + " Left Station" + gameObject.name);
        //stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
        //Do something over here if in case player has been using another seat.
        playerSet = false;
    }

    void Update()
    {
        if (PlayerID == -1 && UIScript.stationObject != this /*|| !playerSet*/)
        {
            //尝试一下从外部获取playerid
            PlayerID = OWML_Player.stationFlagLocal[stationIndex];
            timeoutTimer = timeoutTimer + Time.deltaTime;

            if (timeoutTimer > UIScript.StationTimeout)
            {
                Debug.Log("Deactivating: " + gameObject.name + " due to timeout.");
                FFRDebug("Deactivating: " + gameObject.name + " due to timeout.");
                timeoutTimer = 0;
            }
            return;
        }//超时自动禁用

        if (PlayerID != -1)
        {
            //if (UIScript.stationObject == this && PlayerID == -1) //不这么更新，以PlayerID为是否有玩家的准则
            if (UIScript.stationObject == this && Networking.IsOwner(gameObject))
            {
                IndicatorDebug.SetActive(false);
            }

            if (Player == null)//信息残缺的话更新方式
            {
                if (PlayerID == Networking.LocalPlayer.playerId) //被分配到的玩家执行这里
                {
                    FFRDebug("fixing broken station on me" + PlayerID + " to " + gameObject.name);
                    Player = Networking.LocalPlayer;
                    UIScript.stationObject = this;
                    isMe = true;
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    SendCustomEventDelayedSeconds(nameof(useSeat), 1);//延迟1s以等待ownerset生效
                }
                else
                {
                    FFRDebug("fixing broken station on others" + PlayerID + " to " + gameObject.name);
                    Player = VRCPlayerApi.GetPlayerById(PlayerID);
                    isMe = false;
                    stationObject.PlayerMobility = VRCStation.Mobility.Immobilize;
                }
                FFRDebug("fixing done");
            }

            if (isMe && Networking.IsOwner(gameObject)) //这里不检查playersset, 因为player owml 会导致玩家一瞬间离开椅子
            {
                if (Player != Networking.LocalPlayer)//某种异常, 玩家退出的一瞬间发生
                {
                    Debug.Log("line388 really happend, sad,,,");
                }
                if (!nextFrame && !inVehicle)
                //if (!playerSet && !inVehicle)
                {
                    //FFRDebug("Seems haven't seat, Resign station");
                    FFRDebug("On nextFrame true, Resign station");
                    nextFrame = true;
                    //useSeat(); //下面的事件好像没有被接受到，所以这里处理了一下(暂时的)
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(useSeat));
                }
                if (UIScript.PlayerAircraft != null && !inVehicle)
                {
                    inVehicle = true;
                    RequestSerialization();
                }

                if (inVehicle && UIScript.PlayerAircraft == null)
                {
                    inVehicle = false;
                    RequestSerialization();
                }

                if ((!inVehicle || UIScript.syncEvenInVehicle))
                {
                    PlayerPosition = Networking.LocalPlayer.GetPosition();
                    if (/*!Networking.IsClogged && */timerPlayerUpdate > UIScript.PlayerUpdateRate)
                    {
                        CurrentPlayerPosition = -UIScript.Map.position + Networking.LocalPlayer.GetPosition();
                        // stationObject.transform.position = PlayerPosition; //<-- StationObject syncing 'calling it a day'
                        // CurrentPlayerPosition = stationObject.transform.localPosition;//<-- StationObject syncing 'calling it a day'
                        CurrentPlayerRotation = Networking.LocalPlayer.GetRotation();
                        RequestSerialization();
                        timerPlayerUpdate = 0f;
                    }
                }

                if (!inVehicle && UIScript.allowPlayerOWML) // Player Teleport MUST NOT BE CALLED WHILE FLYING! 
                {
                    if (Vector3.Distance(PlayerPosition, Vector3.zero) > UIScript.ChunkDistance)
                    {
                        var tempAnchor = PlayerPosition;
                        UIScript.Map.position = UIScript.Map.position - PlayerPosition;
                        TemporaryVelocity = Networking.LocalPlayer.GetVelocity();
                        Networking.LocalPlayer.TeleportTo(Vector3.zero, Networking.LocalPlayer.GetRotation());
                        UIScript.doChunkUpdate(tempAnchor);
                        nextFrame = false;
                    }
                }

                timerPlayerUpdate = timerPlayerUpdate + Time.deltaTime;
            }
            else
            {
                if (stationObject != null)
                {
                    if (!inVehicle || UIScript.syncEvenInVehicle)
                    {
                        stationObject.transform.SetPositionAndRotation(
                            Vector3.Lerp(oldPos, UIScript.Map.position + CurrentPlayerPosition, Time.deltaTime * 10), 
                            Quaternion.Slerp(oldRot, CurrentPlayerRotation, Time.deltaTime * 10)); 
                        oldPos = stationObject.transform.position;
                        oldRot = stationObject.transform.rotation;
                    }
                }
            }
        }
    }

    private void FFRDebug(string x)
    {
            UIScript.OWMLDebuger.Log(x);
    }
}

