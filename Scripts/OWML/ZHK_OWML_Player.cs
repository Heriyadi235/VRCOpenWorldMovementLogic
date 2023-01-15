using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

/*
OWML Player Controller
For SaccFlight 1.5

by: Zhakami Zhako
Discord: ZhakamiZhako#2147
Twitter: @ZZhako
Email: zhintamizhakami@gmail.com
*/
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZHK_OWML_Player : UdonSharpBehaviour
{
    [Tooltip("Required: Your Scene's  ZHK_UIScript Gameobject. ")]
    public ZHK_UIScript UIScript;
    VRCPlayerApi[] players = new VRCPlayerApi[80];
    //[System.NonSerializedAttribute] [UdonSynced] public string stationFlag ;
    //[System.NonSerializedAttribute] public string stationFlagLocal ;

    [System.NonSerializedAttribute] [UdonSynced] public int[] stationFlag = new int[80];
    //TODO:之后压缩一下这个变量，反正范围不大，减少网络开销
    [System.NonSerializedAttribute] public int[] stationFlagLocal = new int[80];

    [Tooltip("Required: 80 Player Stations. (ZHK_OWML_Station)")]
    public ZHK_OWML_Station[] Stations;

    FFRDEBUGSCRIPT OWMLDebuger;
    private void Start()
    {
        //初始化座位标志
        for (int i = 0; i < stationFlagLocal.Length; i++)
        {
            stationFlag[i] = -1;
            stationFlagLocal[i] = -1;
        }
        //debug
        OWMLDebuger = UIScript.Debugger.GetComponent<FFRDEBUGSCRIPT>();

        if (Networking.IsMaster && Networking.IsOwner(gameObject))
        {
            //加载过程中就是房主，说明房间现在没人
            FFRDebug("first Joiner!");
            //register(Networking.LocalPlayer);
        }
    }
    
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // VRCPlayerApi.GetPlayers(players);
        FFRDebug("Player" + player.playerId + "Joined");
        register(player); 
    }

    public void register(VRCPlayerApi xx)
    {
        int target = -1;
        bool alreadyRegisted = false;

        target = IsPlayerInStationFlag(xx.playerId); //玩家是否已经被分配了座椅

        if (target == -1)
        {
            for (int i = 0; i < Stations.Length; i++)//find empty station
            {
                if (stationFlagLocal[i] == -1)
                {
                    target = i;
                    stationFlagLocal[i] = xx.playerId;
                    break;
                }
            }
        } //玩家还未被分配座椅
        else
        {
            alreadyRegisted = true;
        }

        if (target == -1) FFRDebug("No Empty Station!");//没空座了
        else //有空坐，target
        {
            if (!alreadyRegisted)
            {
                if (Networking.IsOwner(gameObject))
                {
                    FFRDebug("Owner PlayerController register player " + xx.playerId + " to station" + target);
                    for (int i = 0; i < Stations.Length; i++)//C#里能不能直接等于赋值？
                    {
                        stationFlag[i] = stationFlagLocal[i];
                    }
                    RequestSerialization();
                    ApplyStationFlag();
                }
                else
                {
                    FFRDebug("Non-Owner PlayerController register player " + xx.playerId + " to station" + target);
                }
            }
            else
            {
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }
            }
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        // VRCPlayerApi.GetPlayers(players);
        FFRDebug("Player" + player.playerId + "Left");
        unregister(player);
    }

    public void unregister(VRCPlayerApi xx)
    {
        int target = -1;
        for (int i = 0; i < Stations.Length; i++)
        {
            if (stationFlagLocal[i] == xx.playerId)
            {
                target = i;
                stationFlagLocal[i] = -1;
                break;
            }
        }
        if (target == -1) FFRDebug("Left Player" + xx.playerId + " have no station"); //没找到这个玩家的座椅
        else//离开的玩家座位位于target
        {
            if (Networking.IsOwner(gameObject))
            {
                FFRDebug("Owner PlayerController unregister player " + xx.playerId + " from station" + target);
                stationFlag = stationFlagLocal;
                ApplyStationFlag();
                RequestSerialization();
            }
            else 
            {
                FFRDebug("Non-Owner PlayerController unregister player " + xx.playerId + " from station" + target);
            }
            
        }
    }

    public override void OnPreSerialization()
    {
        base.OnPreSerialization();
        FFRDebug("PreSerialization");
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        //如果先收到OnPlayerLeft,再收到OnOwnershipTransferred
        //数据应该都是正常的
        FFRDebug("Player Controller Owner changed to " + player.playerId);
        //如果先收到OnOwnershipTransferred，再收到如果先收到OnPlayerLeft
        //if (player.isLocal)
        //{
        //    Debug.Log("Owner Disconnected. Re-Sorting");
        //    foreach (var owmlStation in Stations)
        //    {
        //        if (owmlStation.PlayerID == -1)
        //            owmlStation.delaySetOwner();
        //    }
        //}
    }

    public override void OnDeserialization()
    {
        FFRDebug("OnDeserialization");
        var hasMissSync = false;
        for (int i = 0; i < Stations.Length; i++)
        {
            if (stationFlag[i] != stationFlagLocal[i])
            {
                FFRDebug("station" + i + "nonsync");
                stationFlagLocal[i] = stationFlag[i];
                hasMissSync = true;
            }
        }
        if(hasMissSync)
            ApplyStationFlag();
        else
            FFRDebug("Sync Check Pass");
    }

    public void ApplyStationFlag()
    {
        for (int i = 0; i < Stations.Length; i++)
        {
            if (stationFlagLocal[i] != Stations[i].PlayerID)
            {
                FFRDebug("station " + i + " flag is " + stationFlagLocal[i] 
                        + " but station" + i + " is " + Stations[i].PlayerID);
                if (Stations[i].PlayerID != -1 && stationFlagLocal[i] == -1) //玩家离开
                {
                    FFRDebug("unassinging Player " + Stations[i].PlayerID + " to station " + i + ", applying");
                    Stations[i].PlayerID = -1;
                    Stations[i].gameObject.SetActive(false);
                    FFRDebug("Station unregister from apply");
                    Stations[i].SendCustomEventDelayedFrames("unregister",1);
                    //Stations[i].unregister();
                }

                else if (Stations[i].PlayerID == -1 && stationFlagLocal[i] != -1) //玩家加入
                {
                    FFRDebug("assinging Player " + stationFlagLocal[i] + " to station " + i + ", applying");
                    Stations[i].PlayerID = stationFlagLocal[i];
                    Stations[i].gameObject.SetActive(true);
                    FFRDebug("Station register from apply");
                    Stations[i].SendCustomEventDelayedFrames("register", 1);
                    //Stations[i].register();
                }
                
                else
                {
                    FFRDebug("WTFFFF！");
                    FFRDebug("station " + i + " flag is " + stationFlagLocal[i]
                        + " but station" + i + " is " + Stations[i].PlayerID);
                }
                
            }
            //else if(stationFlagLocal[i] == Stations[i].PlayerID 
            //        && stationFlagLocal[i] != -1
            //        && stationFlagLocal[i] != Networking.LocalPlayer.playerId)//player has assigned
            //{
            //    FFRDebug("Repeat Apply");
            //}
        }
    }
    
    //public void checkPlayer()
    //{
    //    FFRDebug("[PlayerController]checkPlayer");
    //    foreach (var x in Stations)
    //    {
    //        x.checkIfPlayerPresent();
    //    }
    //}

    public void recheckPlayerIDs()
    {
        FFRDebug("[PlayerController]recheckPlayerIDs");
        Debug.Log("recheck Players request");
        //SendCustomNetworkEvent(NetworkEventTarget.All, nameof(recheckPlayers));
         
        if (Networking.IsOwner(gameObject))
        {
            recheckPlayers();
        }
    }
    
    public bool debugPrintPlayers()
    {
        FFRDebug("[PlayerController]debugPrintPlayers");
        foreach (var x in players)
        {
            if (x != null) Debug.Log("P:id:" + x.playerId);
        }

        return true;

    }

    public int IsPlayerInStationFlag(int id)
    {
        int target = -1;
        for (int i = 0; i < Stations.Length; i++)//find if player already in
        {
            if (stationFlagLocal[i] == id)
            {
                target = i;
                FFRDebug("Player " + id + " Already have station " + i);
            }
        }
        return target;
    }
    
    public void recheckPlayers()
    {
        if (!Networking.IsOwner(gameObject))  {return; }
        if (UIScript == null) return; //TODO,房主没有station的情况如何处理
        FFRDebug("[PlayerController]Someone still has no station after 15 seconds. Rechecking Players");
        Debug.Log("Someone still has no station after 15 seconds. Rechecking Players");
        players = VRCPlayerApi.GetPlayers(players);
        
        Debug.Log("TestPlayers");
        Debug.Log(players.ToString());

        for (int x = 0; x < players.Length; x++)
        {
            if(players[x].IsValid())
            {
                var target = IsPlayerInStationFlag(players[x].playerId);
                if (target != -1)
                {
                    FFRDebug("Player " + players[x].playerId + " have station " + target);
                }
                else
                {
                    FFRDebug("Player " + players[x].playerId + " dont have station, registering");
                    register(players[x]);
                    return;
                }
            }
            //如果所有的玩家在房主视角中都有station了,不会在前面return，说明有的玩家没收到座椅分配信息，重新序列化
            RequestSerialization();
        }

        // if (noMiss)
        // {
        //Debug.Log("Ownership recheck...");
        //    ownershipRechecks();
        // }
    }

    //public void ownershipRechecks()
    //{
    //    FFRDebug("[PlayerController]ownershipRechecks");
    //    foreach (var x in Stations)
    //    {
    //        if (x.gameObject.activeInHierarchy)
    //        {
    //            x.broadcastOwnershipRecheck();
    //        }
    //    }
    //}

    public void resyncCall()
    {
        //Debug.Log("Resynchronize Call!");
        RequestSerialization();
    }

    //public void doDelay()
    //{
    //    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(checkPlayer));
    //}

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

    //private float timeoutTimer = 0f;
    //public void Update()
    //{
    //    timeoutTimer = timeoutTimer + Time.deltaTime;

    //    if (timeoutTimer > 10)
    //    {
    //        FFRDebug("player controller still alive");
    //        timeoutTimer = 0;
    //    }
    //}
}
