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

// TODO: Further Optimization & smoother player movement
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
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
    [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */     public VRCPlayerApi Player;
    [System.NonSerializedAttribute] [UdonSynced, FieldChangeCallback(nameof(inVehicle))] public bool _inVehicle = false;
    public bool inVehicle
    {
        set
        {
            _inVehicle = value;
            Debug.Log("In vehicle Called " + value, this);
            FFRDebug("In vehicle Called " + value);
            if (Networking.IsOwner(gameObject) && UIScript.stationObject == this)
            {
                if (!value)
                {
                    stationObject.transform.position = Networking.LocalPlayer.GetPosition();
                    stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
                    stationObject.UseStation(Networking.LocalPlayer);
                }
            }
        }
        get => _inVehicle;
    }
    [System.NonSerializedAttribute] [UdonSynced, FieldChangeCallback((nameof(PlayerID)))] public int _PlayerID = -1;
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
    [HideInInspector] [UdonSynced] public bool playerSet = false; //玩家已经坐在椅子上了
    


    [System.NonSerializedAttribute] [UdonSynced] public Vector3 CurrentPlayerPosition = Vector3.zero;
    [System.NonSerializedAttribute] [UdonSynced] public Quaternion CurrentPlayerRotation;
    public Vector3 oldPos = Vector3.zero;
    private Quaternion oldRot;
    private float timerPlayerUpdate = 0f;
    private float distanceFromCenter = 0f;
    private Vector3 PlayerPosition = Vector3.zero;
    private Vector3 TemporaryVelocity = Vector3.zero;
    
    private bool nextFrame = false;
    private float timeoutTimer = 0f;

    FFRDEBUGSCRIPT OWMLDebuger;

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
        stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
        stationObject.disableStationExit = true;
        //LocalPlayer = Networking.LocalPlayer;

        //debug
        OWMLDebuger = UIScript.Debugger.GetComponent<FFRDEBUGSCRIPT>();
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

    //public void broadcastRegistered()
    //{
    //    Debug.Log(gameObject.name + " registered for " + PlayerID);
    //    gameObject.SetActive(true);

    //}

    public void register()
    {
        FFRDebug("STATION REGISTER" + gameObject.name);
        if (PlayerID == Networking.LocalPlayer.playerId) //被分配到的玩家执行这里
        {
            Player = Networking.LocalPlayer;
            UIScript.stationObject = this;
            isMe = true;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            SendCustomEventDelayedSeconds(nameof(useSeat),1);
        }
        else
        {
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
        //playerSet = false;
        if (PlayerID != -1)
        {
            FFRDebug("Clearing Station for register");
            stationObject.ExitStation(VRCPlayerApi.GetPlayerById(PlayerID));
        }
        else
        {
            FFRDebug("UNREGISTERING");
        }
    }

    public void delaySetOwner()
    {
        Networking.SetOwner(Networking.GetOwner(OWML_Player.gameObject), gameObject);
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
        if (Player == Networking.LocalPlayer)
        {
            TemporaryVelocity = Networking.LocalPlayer.GetVelocity();
            stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
            stationObject.UseStation(Networking.LocalPlayer);
        }
    }
    
    //public override void OnOwnershipTransferred(VRCPlayerApi player)
    //{
    //    Debug.Log("!!!!!!!! ON OWNERSHIP TRANSFERRED CALL");
    //    FFRDebug("!!!!!!!! ON OWNERSHIP TRANSFERRED CALL");
    //    if (PlayerID != -1)//after assigned the playerid has been synced to every player
    //        Player = VRCPlayerApi.GetPlayerById(PlayerID);
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
    //}

    //public override void OnDeserialization()
    //{
    //    if (playerNeedAssign && PlayerID != -1)
    //    {
    //        if (Networking.IsOwner(gameObject))
    //        {
    //            Debug.Log("registe player from OnDeserialization");
    //            FFRDebug("registe player from OnDeserialization");
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
    //}

    //public void broadcastOwnershipRecheck()
    //{
    //    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OwnershipRecheck));
    //}

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
        //Entered a vehicle or something else.
        Debug.Log(player.displayName + "-" + player.playerId + " Left Station" + gameObject.name);
        FFRDebug(player.displayName + "-" + player.playerId + " Left Station" + gameObject.name);
        stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
        //Do something over here if in case player has been using another seat.
        playerSet = false; 
    }

    //public void OwnershipRecheck()
    //{
    //    Debug.Log("Player ID:" + Networking.LocalPlayer.playerId + "- Ownership recheck : HAS STATION:" + (UIScript.stationObject ? "Yes" : "No" + "And do I own it? " + (Networking.IsOwner(gameObject))));
    //    FFRDebug("Player ID:" + Networking.LocalPlayer.playerId + "- Ownership recheck : HAS STATION:" + (UIScript.stationObject ? "Yes" : "No" + "And do I own it? " + (Networking.IsOwner(gameObject))));
    //    //gameObject.SetActive(true);
    //    if (UIScript.stationObject == null
    //        && Networking.IsOwner(gameObject)
    //        && PlayerID == Networking.LocalPlayer.playerId)
    //    {
    //        Debug.Log("registe player from OwnershipRecheck");
    //        FFRDebug("registe player from OwnershipRecheck");
    //        gameObject.SetActive(true);
    //        Player = VRCPlayerApi.GetPlayerById(PlayerID);
    //        UIScript.stationObject = this;
    //        isMe = true;

    //        PlayerID = Networking.LocalPlayer.playerId; //see if id will change after
    //        SendCustomEventDelayedSeconds(nameof(useSeat), 2);
    //    }
    //    else//不是房主，仅仅是被通知到了座椅分配
    //    {
    //        Debug.Log("OnOwnershipcall but not owner");
    //        FFRDebug("OnOwnershipcall but not owner");
    //        gameObject.SetActive(true);
    //        Player = VRCPlayerApi.GetPlayerById(PlayerID);
    //        isMe = false;
    //    }

    //}

    public void broadcastRefreshSeat()
    {
        SendCustomEventDelayedSeconds(nameof(refreshSeat), 4);
    }
    
    public void refreshSeat()
    {
        gameObject.SetActive(true);
        timeoutTimer = 0;
    }

    //public void checkIfPlayerPresent() // function call to 'synchronize' the player that's not in a station locally
    //{
    //    //aborted
    //    if (Player != null && PlayerID != -1 && !inVehicle)
    //    {
    //        // stationObject.PlayerMobility = VRCStation.Mobility.Mobile;
    //        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(checkOwner));
    //        playerSet = true;
    //    }
    //}

    //public void checkOwner() // function call to 'synchronize' the player that's not in a station, broadcasts to the owner that he has to be in one.
    //{
    //    //aborted
    //    if (Networking.IsOwner(gameObject) && UIScript.stationObject == this)
    //    {
    //        if (Player == null) Player = Networking.LocalPlayer;
    //        stationObject.transform.position = Player.GetPosition();
    //        stationObject.transform.rotation = Player.GetRotation();
    //        Debug.Log("Resynchronize Call received for station " + gameObject.name + " of player:" + Player.playerId + " for station player id:" + PlayerID);
    //        FFRDebug("Resynchronize Call received for station " + gameObject.name + " of player:" + Player.playerId + " for station player id:" + PlayerID);
    //        TemporaryVelocity = Networking.LocalPlayer.GetVelocity();
    //        nextFrame = false;
    //        IndicatorDebug.SetActive(false); // A just in case wtf shit of the hell 
    //        if (!inVehicle) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(useSeat));
    //    }
    //}

    void Update()
    {
        if (PlayerID == -1 && UIScript.stationObject != this /*|| !playerSet*/)
        {
            timeoutTimer = timeoutTimer + Time.deltaTime;

            if (timeoutTimer > UIScript.StationTimeout)
            {
                Debug.Log("Deactivating: " + gameObject.name + " due to timeout.");
                FFRDebug("Deactivating: " + gameObject.name + " due to timeout.");
                timeoutTimer = 0;
                unregister();
                //gameObject.SetActive(false);
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

            if (isMe && playerSet)
            {
                if (Player != Networking.LocalPlayer)//某种异常, 玩家退出的一瞬间发生
                {
                    Debug.Log("line388 really happend, sad,,,");
                }
                if (!nextFrame && !inVehicle)
                {
                    FFRDebug("On Chunk station Update");
                    nextFrame = true;
                    useSeat(); //下面的事件好像没有被接受到，所以这里处理了一下
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
                if (stationObject != null && playerSet)
                {
                    if (!inVehicle || UIScript.syncEvenInVehicle)
                    {
                        // stationObject.transform.position = Vector3.MoveTowards(oldPos, UIScript.Map.position + CurrentPlayerPosition, 6);
                        stationObject.transform.position = Vector3.Lerp(oldPos, UIScript.Map.position + CurrentPlayerPosition, Time.deltaTime * 10);
                        // stationObject.transform.localPosition = Vector3.Lerp(oldPos,CurrentPlayerPosition, Time.deltaTime); //<-- stationObject local position syncing *Call it a day*
                        stationObject.transform.rotation = Quaternion.Slerp(oldRot, CurrentPlayerRotation, Time.deltaTime * 10);
                        oldPos = stationObject.transform.position;
                        oldRot = stationObject.transform.rotation;
                    }
                }
            }
        }
    }

    private void FFRDebug(string x)
    {
        if (OWMLDebuger)
        {
            OWMLDebuger.Log(x);
        }
        else
        {
            Debug.Log("Debugger Not Set!");
        }
    }
}

