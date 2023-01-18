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
[DefaultExecutionOrder(10)]
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZHK_OWML_Player : UdonSharpBehaviour
{
    [Tooltip("Required: Your Scene's  ZHK_UIScript Gameobject. ")]
    public ZHK_UIScript UIScript;
    VRCPlayerApi[] players = new VRCPlayerApi[80];
    //[System.NonSerializedAttribute] [UdonSynced] public string stationFlag ;
    //[System.NonSerializedAttribute] public string stationFlagLocal ;

    [System.NonSerializedAttribute] [UdonSynced] public sbyte[] stationFlag = new sbyte[80];

    [System.NonSerializedAttribute] public int[] stationFlagLocal = new int[80];

    [Tooltip("Required: 80 Player Stations. (ZHK_OWML_Station)")]
    public ZHK_OWML_Station[] Stations;

    public bool hasInitialized = false;

    private void Start()
    {
        //初始化座位标志
        for (int i = 0; i < Stations.Length; i++)
        {
            stationFlag[i] = -1;
            stationFlagLocal[i] = -1;
            Stations[i].stationIndex = i;
        }

        if (Networking.IsMaster && Networking.IsOwner(gameObject))
        {
            //加载过程中就是房主，说明房间现在没人
            FFRDebug("first Joiner!");
            hasInitialized = true;
            //register(Networking.LocalPlayer);
        }
    }
    
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // VRCPlayerApi.GetPlayers(players);

        FFRDebug("Player" + player.playerId + "Joined");
        if (player.playerId < Networking.LocalPlayer.playerId) 
        //之后加入的玩家也会收到之前加入的玩家的加入事件，但此时加入房间的首次序列化已经完成了
        //他们的注册，处理一下
        {
            FFRDebug("Former joiner, no need to assign");
            return;
        }
        else if(hasInitialized)
        {
            register(player);
        }
        
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
                        stationFlag[i] = (sbyte)stationFlagLocal[i];
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
        FFRDebug("Player " + player.displayName+ "[" + player.playerId + "] Left");
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
                for(int i = 0; i<Stations.Length; i++)
                    stationFlag[i] = (sbyte)stationFlagLocal[i];
                ApplyStationFlag();
                SendCustomEventDelayedSeconds(nameof(RequestSerialization), 1); //玩家退出后，等待其他玩家承认房主移交可能需要一些时间,所以这里延迟序列化一下
                //RequestSerialization();
            }
            else 
            {
                FFRDebug("Non-Owner PlayerController unregister player " + xx.playerId + " from station" + target);
            }
            
        }
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

    public override void OnPreSerialization()
    {
        var strflaglocal = "";
        var strreceive = "";
        for (int i = 0; i < 10; i++)
        {
            strflaglocal += stationFlagLocal[i];
            strreceive += stationFlag[i];
        }
        FFRDebug("local is " + strflaglocal);
        FFRDebug("receiver is " + strreceive);
        FFRDebug("PreSerialization");
    }
    
    public override void OnDeserialization()
    {
        FFRDebug("OnDeserialization");
        hasInitialized = true; //作为非owner第一次收取到初始化之后，做一个标记

        var hasMissSync = false;
        //debug
        var strflaglocal = "";
        var strreceive = "";
        for (int i = 0; i < 10; i++)
        {
            strflaglocal += stationFlagLocal[i];
            strreceive += stationFlag[i];
        }
        FFRDebug("local is " + strflaglocal);
        FFRDebug("receiver is " + strreceive);

        for (int i = 0; i < Stations.Length; i++)
        {
            if ((stationFlag[i] != stationFlagLocal[i]))
            {
                FFRDebug("station flag at" + i + " nonsync to owner");
                stationFlagLocal[i] = stationFlag[i];
                hasMissSync = true;
            }

            if(stationFlagLocal[i] != Stations[i].PlayerID)
            {
                FFRDebug("station flag at" + i + " nonsync to stations");
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
                    FFRDebug("unassinging Player " + Stations[i].PlayerID + " to station " + i);
                    Stations[i].PlayerID = -1;
                    Stations[i].gameObject.SetActive(false);
                    FFRDebug("apply station unregister");
                    Stations[i].unregister();
                }

                else if (Stations[i].PlayerID == -1 && stationFlagLocal[i] != -1) //玩家加入
                {
                    FFRDebug("assinging Player " + stationFlagLocal[i] + " to station " + i);
                    Stations[i].PlayerID = stationFlagLocal[i]; 
                    Stations[i].gameObject.SetActive(true);
                    FFRDebug("apply station register from "+stationFlagLocal[i] + "to " + Stations[i].PlayerID); 
                    Stations[i].register(); 
                    FFRDebug("apply station register DONE" + Stations[i].PlayerID);
                }
                
                else
                {
                    FFRDebug("WTFFFF！");//前一个玩家没有被正确注销可能导致这个问题
                    Stations[i].PlayerID = stationFlagLocal[i];
                    Stations[i].gameObject.SetActive(true);
                    FFRDebug("force apply Station register");
                    Stations[i].register();
                }
                
            }
        }
    } 

    public int IsPlayerInStationFlag(int id)
    {
        //check if a player id has in playerstationlocal
        int target = -1;
        for (int i = 0; i < Stations.Length; i++)//find if player already in
        {
            if (stationFlagLocal[i] == id)
            {
                target = i;
                FFRDebug("IsPlayerInStationFlag found Player " + id + " at station " + i);
            }
        }
        return target;
    }
    
    public void recheckPlayers()
    {
        //fired when someone don't have station after 15s
        if (!Networking.IsOwner(gameObject))  {return; }
        if (UIScript.stationObject == null) return; //TODO,房主没有station的情况如何处理
        FFRDebug("[PlayerController]Someone still has no station after 15 seconds. Rechecking Players");
        Debug.Log("Someone still has no station after 15 seconds. Rechecking Players");
        VRCPlayerApi.GetPlayers(players);
        
        //检查已有的玩家
        for (int x = 0; x < players.Length; x++)
        {
            if(players[x] != null)
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
                }
            }
            //如果所有的玩家在房主视角中都有station了,不会在前面return，说明有的玩家没收到座椅分配信息，重新序列化
        }

        //检查已经退出的玩家
        for (int i = 0; i < Stations.Length; i++)
        {
            if (Stations[i].PlayerID == -1)
            {
                continue;
            }
            if (VRCPlayerApi.GetPlayerById(Stations[i].PlayerID) == null)
            {
                //手动注销一下
                FFRDebug("Playid[" + Stations[i].PlayerID + "] has left, unregisting");
                Stations[i].unregister();
                stationFlagLocal[i] = -1;
                stationFlag[i] = -1;
            }
        }
        RequestSerialization();
    }

    public void resyncCall()
    {
        //fired every 15 seconds
        RequestSerialization();
    }

    private void FFRDebug(string x)
    { 
            UIScript.OWMLDebuger.Log(x);
    }

}
