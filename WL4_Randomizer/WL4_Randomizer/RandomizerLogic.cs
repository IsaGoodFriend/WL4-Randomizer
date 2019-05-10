using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WL4_Randomizer
{

    /// Read file for entity, room, and door data
    ///     foreach room
    ///         Add room's vanilla index to array if it's not in array yet
    ///         Create room in code
    ///         foreach door
    ///             Create door in code
    ///             Set door type from file
    ///             Get/set door rom index from file
    ///             Set flags from file
    ///             Set room pointer
    ///             Set connected door rom index
    ///             Add door to path logic
    ///             Add door to room
    ///         Add all entities needed

    ///     Assign first room
    ///     Create an array of entities for the level
    ///     Create empty Room array to count rooms able to be accessed before frog switch
    ///     Create empty Door and room arrays for paths that can only be accessed after
    ///     Get Enum for pathing style

    ///     Branching method: (creating before frog switch layout)
    ///         Add room to room array
    ///         Remove room from total room array
    ///         foreach Door in room:
    ///             if door is connected, ignore
    ///             if door can be used only after switch, add to door array
    ///             else Get a random door until it's able to be passed through, and connect, and make a branch with that door's room
    ///         foreach connection
    ///             if is able to pass through, branch with connected room
    ///             if is able to pass after switch, add connected room to array
    ///     /Branching method

    ///     Pick a random room from previous room array and set frog switch inside
    ///     foreach door accessable only after switch
    ///         If door isn't connected, branch on room door moves you into
    ///         Branching Method (post frog switch layout)
    ///             Add room to room array
    ///             foreach Door in room:
    ///                 if door is connected, ignore
    ///                 Get a random door, check to see if it's able to be passed through.
    ///                 If it is, remove the door from array, connect it to this door, and make a branch with that door's room
    ///                 foreach connection
    ///                     if is able to pass through, branch with connected room
    ///         /Branching Method


    /// Pick random rooms and place the gems, cd, and keyser in them
    /// Foreach door, set new door connection in buffer
    /// Foreach entity, set type and position
    /// Clear base room, door, and entity array

    // TODO: Find solution to dead end room problem, assuming it shows up again
    delegate void ClearRoomEvent(List<RoomInfo> removeList, List<RoomInfo> addList, DoorPairingsTracker tracker);
    class PathCreator
    {
        public const int DoorTableLocation = 0x78F21C, LevelHeadersLocation = 0x639068, LevelHeaderIndexLocation = 0x6391C4, RoomDataTableLocation = 0x78F280;

        private static List<RoomInfo> roomList = new List<RoomInfo>();
        private static List<RoomInfo> roomsNotInLogic = new List<RoomInfo>();
        private static List<DoorInfo> doorList = new List<DoorInfo>();
        private static List<EntityInfo> entityList = new List<EntityInfo>();

        private static List<int> vanillaRoomNumbers = new List<int>();
        private static List<List<EntityInfo>> vanillaEntities = new List<List<EntityInfo>>();
        private static DoorPairingsTracker doorPairs = new DoorPairingsTracker();

        /// <summary>
        /// Doors are sorted by the OPPPOSITE of what their own type is for easier searching
        /// </summary>
        private static RoomInfo portalRoom;

        private static event ClearRoomEvent ClearRoom;

        private static int count;

        private static bool hasEntities = true;

        private static string PassageName(int passage)
        {
            switch (passage)
            {
                default:
                    return "Entry";
                case 1:
                    return "Emerald";
                case 2:
                    return "Ruby";
                case 3:
                    return "Topaz";
                case 4:
                    return "Sapphire";
                case 5:
                    return "Golden";
            }
        }

        public static EntityInfo[] GetEntities(int passage, int level, int room)
        {
            int levelID = Program.GetLevelID(passage, level);
            int roomIndex = Program.GetPointer(RoomDataTableLocation + levelID * 4) + room * 0x2c;
            roomIndex = Program.GetPointer(roomIndex + 0x20);

            List<EntityInfo> retVal = new List<EntityInfo>();

            for (int index = 0; Program.romBuffer[roomIndex + index * 3] != 0xFF; index++)
            {
                int romIndex = roomIndex + index * 3;
                int type = Program.romBuffer[romIndex + 2];
                if (type < 0xF)
                {
                    retVal.Add(new EntityInfo(romIndex, EntityType.BigGem, Program.romBuffer[romIndex + 1], Program.romBuffer[romIndex], retVal.Count, Program.romBuffer[romIndex + 2]));
                }
            }

            return retVal.ToArray();
        }
        public static void CreatePath(int passage, int level, XmlNode levelNode)
        {
            if (passage == 5)
                hasEntities = false;

            Console.WriteLine("--- Randomizing level {0};{1} ---", PassageName(passage), level + 1);
            int levelHeaderPointer = LevelHeadersLocation + Program.GetLevelHeaderIndex(passage, level) * 12;
            int roomMax = Program.romBuffer[levelHeaderPointer + 1];

            for (int i = 0; i < roomMax; i++)
            {
                FillVanillaEntities(passage, level, i);
            }

            foreach (XmlNode node in levelNode.ChildNodes)
            {
                if (node.Name == "roomConnection")
                {
                    LoadRoomConnection(node);
                }
                else
                {
                    switch (node.Name.Split('_')[0])
                    {
                        case "room":
                            LoadRoom(node);
                            break;
                    }
                }
            }

            portalRoom = roomList[0];
            List<EntityInfo> levelEntities = new List<EntityInfo>();
            foreach (List<EntityInfo> info in vanillaEntities)
            {
                levelEntities.AddRange(info);
            }

            List<RoomInfo> inLogic = new List<RoomInfo>();
            roomsNotInLogic.AddRange(roomList);

            doorPairs.FinalizeDoors();

            RestartLogic:

            BranchLogic(inLogic, portalRoom);

            List<RoomInfo> checkedRooms = new List<RoomInfo>();

            /// While there are still rooms not in logic yet
            ///     Get two empty pointers, one for door in logic, one out of logic
            ///     while failed
            ///         Pick random room in and out of logic.
            ///         Pick random door from out of logic room.
            ///         Save door type
            ///         Save all doors of the opposite type from in logic room in array
            ///         Pick two random doors.  If second array is empty, repick rooms from in and out of logic
            ///     (We don't need in logic room anymore)
            ///     Save the two rooms on each side of the in logic door
            ///     Save the two connected doors
            ///     Disconnect doors
            ///     if one of the two in logic room isn't connected to portal
            ///         reset all rooms on that branch
            ///     Set a pointer to one of the rooms still in logic
            ///     Connect the new doors
            ///     Redo before logic path on the once out of logic door's base room
            /// 
            /// Connect up the rest of the doors
            count = 0;
            /// While there are still rooms not in logic yet
            while (roomsNotInLogic.Count > 0)
            {
                Program.DebugLog("StartOfLoop");
                for (int i = roomsNotInLogic.Count; i > 0; i--)
                {
                    if (roomsNotInLogic[i - 1].doorList.Count == 0)
                    {
                        roomsNotInLogic.RemoveAt(i - 1);
                    }
                }
                /// Get two empty pointers, one for door in logic, one out of logic
                DoorInfo inLogicDoor = null, outOfLogicDoor;

                /// Get all possible door pairings between out of logic doors and in logic doors that are already connected.
                {
                    DoorPairing[] pairs = FindPossiblePairs(roomsNotInLogic.ToArray(), inLogic.ToArray(), true);

                    if (pairs.Length == 0)
                    {
                        inLogic[Program.GetRandomInt(inLogic.Count)].ResetRoom(inLogic, roomsNotInLogic, doorPairs);
                        goto RestartLogic;
                    }
                    DoorPairing pair = pairs[Program.GetRandomInt(pairs.Length)];
                    /// Pick a random pair.
                    /// If no pairs exist, throw error
                    /// 
                }
                /// while failed
                do
                {
                    /// Pick random room in logic...
                    int v = Program.GetRandomInt(inLogic.Count);
                    Program.DebugLog("Index {0}", v); 
                    outOfLogicDoor = null;

                    RoomInfo inLogicRoom = inLogic[v], outofLogicRoom;

                    /// ...and out of logic.
                    /// Pick random door from out of logic room.
                    while (outOfLogicDoor == null || !outOfLogicDoor.CanWalkInto(Program.pathingLogicBefore))
                    {
                        outofLogicRoom = roomsNotInLogic[Program.GetRandomInt(roomsNotInLogic.Count)];
                        if (outofLogicRoom.doorList.Count > 0)
                            outOfLogicDoor = outofLogicRoom.doorList[Program.GetRandomInt(outofLogicRoom.doorList.Count)];
                    }

                    /// Save door type
                    DoorType type = outOfLogicDoor.doorType;

                    List<DoorInfo> inLogicDoors = new List<DoorInfo>();
                    /// Save all doors of the opposite type from in logic room in array
                    foreach (DoorInfo door in inLogicRoom.doorList)
                    {
                        if ((int)door.doorType == -(int)type && door.connectedDoor != null)
                        {
                            inLogicDoors.Add(door);
                        }
                    }
                    /// If second array is empty, redo loop
                    if (inLogicDoors.Count == 0)
                    {
                        continue;
                    }
                    /// Pick two random doors.  
                    inLogicDoor = inLogicDoors[Program.GetRandomInt(inLogicDoors.Count)];

                    break;
                } while (true);

                /// Save the two connected doors
                DoorInfo firstDoor  = inLogicDoor,          secondDoor  = inLogicDoor.connectedDoor;
                /// Save the two rooms on each side of the in logic door
                RoomInfo roomOne    = firstDoor.BaseRoom,   roomTwo     = secondDoor.BaseRoom;

                /// Disconnect doors

                doorPairs.DisconnectDoor(firstDoor);

                /// if one of the two in logic room isn't connected to portal

                checkedRooms.Clear();
                if (!IsConnectedToFirstRoom(roomOne, checkedRooms))
                {
                    ClearRoom(inLogic, roomsNotInLogic, doorPairs);
                    checkedRooms.Clear();
                    ClearRoom = null;
                }
                if (!IsConnectedToFirstRoom(roomTwo, checkedRooms))
                {
                    ClearRoom = null;
                    checkedRooms.Clear();

                    BranchLogic(inLogic, outOfLogicDoor.BaseRoom);

                    doorPairs.ConnectDoors(firstDoor, secondDoor);

                    continue;
                }

                doorPairs.ConnectDoors(inLogicDoor, outOfLogicDoor);

                BranchLogic(inLogic, outOfLogicDoor.BaseRoom);
            }
            Program.DebugLog("NoMoreRooms out of Logic");

            //List<RoomInfo> tempRooms = new List<RoomInfo>();
            //checkedRooms.Clear();
            //foreach (RoomInfo info in roomList)
            //{
            //    if (checkedRooms.Contains(info))
            //        continue;
            //    if (!CanEnterFirstRoom(info, tempRooms, true))
            //    {
            //        foreach (RoomInfo roomBad in tempRooms)
            //        {
            //            roomBad.ResetRoom(inLogic, roomsNotInLogic, doorPairs);
            //        }
            //        goto RestartLogic;
            //    }
            //    checkedRooms.AddRange(tempRooms);
            //}

            //checkedRooms.Clear();
            //tempRooms.Clear();
            //foreach (RoomInfo info in roomList)
            //{
            //    if (checkedRooms.Contains(info))
            //        continue;
            //    if (!CanEnterFirstRoom(info, tempRooms, false))
            //    {
            //        foreach (RoomInfo roomBad in tempRooms)
            //        {
            //            roomBad.ResetRoom(inLogic, roomsNotInLogic, doorPairs);
            //        }
            //        goto RestartLogic;
            //    }
            //    checkedRooms.AddRange(tempRooms);
            //}

            doorPairs.ConnectLeftoverDoors();
            
            RandomizeEntities(inLogic);

            int levelID = Program.GetLevelID(passage, level);
            int doorArrayStart = Program.GetPointer(DoorTableLocation + levelID * 4);

            foreach (RoomInfo room in inLogic)
            {
                foreach (DoorInfo door in room.doorList)
                {
                    door.SetRomInfo(doorArrayStart);
                }
                foreach (EntityInfo entity in room.entityList)
                {
                    entity.ChangeRom();
                }
            }

            Clear();
        }

        private static void BranchLogic(List<RoomInfo> accessFromStart, RoomInfo startingRoom)
        {
            List<DoorInfo> openAfterSwitchDoor = new List<DoorInfo>();
            List<RoomInfo> openAfterSwitchConn = new List<RoomInfo>();
            List<RoomInfo> found = new List<RoomInfo>();

            BranchPathBeforeSwitch(startingRoom, accessFromStart, openAfterSwitchDoor, openAfterSwitchConn);

            foreach (DoorInfo door in openAfterSwitchDoor)
            {
                BranchPathAfterSwitch(found, door.BaseRoom);
            }
            found.Clear();
            foreach (RoomInfo connect in openAfterSwitchConn)
            {
                BranchPathAfterSwitch(found, connect);
            }
        }

        private static void BranchPathBeforeSwitch(RoomInfo room, List<RoomInfo> array, List<DoorInfo> doorArray, List<RoomInfo> roomConnections)
        {
            if (!array.Contains(room))
                array.Add(room);
            else
                return;
            if (roomsNotInLogic.Contains(room))
                roomsNotInLogic.Remove(room);

            foreach (DoorInfo door in room.doorList)
            {
                if (door.connectedDoor == null && door.CanWalkOutOf(Program.pathingLogicBefore) && doorPairs.HasConnections(door, Program.pathingLogicBefore))
                {
                    DoorPairing[] pairings = FindPossiblePairs(roomList.ToArray(), door, false);
                    if (pairings.Length == 0)
                        return;

                    DoorInfo newDoor = pairings[Program.GetRandomInt(pairings.Length)].first;

                    doorPairs.ConnectDoors(newDoor, door);
                    
                    BranchPathBeforeSwitch(newDoor.BaseRoom, array, doorArray,roomConnections);
                }
                else if (door.CanWalkOutOf(Program.pathingLogicAfter))
                {
                    if (doorArray != null)
                        doorArray.Add(door);
                }
            }
            foreach (RoomConnection connection in room.connectionList)
            {
                if (connection.CanPassThrough(room, Program.pathingLogicBefore))
                {
                    BranchPathBeforeSwitch(connection.GetOtherRoom(room), array, doorArray, roomConnections);
                }
                else if (connection.CanPassThrough(room, Program.pathingLogicAfter))
                {
                    if (roomConnections != null)
                        roomConnections.Add(connection.firstRoom == room ? connection.secondRoom : connection.firstRoom);
                }
            }
        }
        private static void BranchPathAfterSwitch(List<RoomInfo> alreadyFound, RoomInfo room)
        {
            if (alreadyFound.Contains(room))
                return;
            alreadyFound.Add(room);
            if (roomsNotInLogic.Contains(room))
                roomsNotInLogic.Remove(room);

            foreach (DoorInfo door in room.doorList)
            {
                if (door.connectedDoor == null && door.CanWalkOutOf(Program.pathingLogicAfter) && doorPairs.HasConnections(door, Program.pathingLogicBefore))
                {
                    DoorPairing[] pairings = FindPossiblePairs(roomList.ToArray(), door, false);
                    if (pairings.Length == 0)
                        return;

                    DoorInfo newDoor = pairings[Program.GetRandomInt(pairings.Length)].first;

                    doorPairs.ConnectDoors(door, newDoor);
                    
                    BranchPathAfterSwitch(alreadyFound, newDoor.BaseRoom);
                }
            }
            foreach (RoomConnection connection in room.connectionList)
            {
                if (connection.CanPassThrough(room, Program.pathingLogicAfter))
                {
                    BranchPathAfterSwitch(alreadyFound, connection.GetOtherRoom(room));
                }
            }
        }
        private static void FillVanillaEntities(int _passage, int _level, int room)
        {
            List<EntityInfo> list;
            //int roomIndex = Program.GetPointer(Program.GetPointer(RoomDataTableLocation + Program.GetLevelIndex(_passage, _level) * 4) + room * 0x2c + 32);
            vanillaEntities.Add(list = new List<EntityInfo>(GetEntities(_passage, _level, room)));
        }
        private static bool IsConnectedToFirstRoom(RoomInfo room, List<RoomInfo> array)
        {
            if (room == roomList[0])
            {
                ClearRoom = null;
                array.Clear();
                return true;
            }
            if (array.Contains(room))
                return false;
            array.Add(room);
            
            ClearRoom += room.ResetRoom;

            foreach (DoorInfo door in room.doorList)
            {
                if (door.connectedDoor != null)
                {
                    if (IsConnectedToFirstRoom(door.connectedDoor.BaseRoom, array))
                        return true;
                }
            }
            foreach (RoomConnection connection in room.connectionList)
            {
                RoomInfo otherRoom = connection.GetOtherRoom(room);
                if (connection.CanPassThrough(otherRoom, Program.pathingLogicBefore))
                {
                    if (IsConnectedToFirstRoom(otherRoom, array))
                        return true;
                }
            }
            return false;
        }
        private static bool CanEnterFirstRoom(RoomInfo room, List<RoomInfo> array, bool afterSwitch)
        {
            if (room == roomList[0])
            {
                ClearRoom = null;
                return true;
            }
            if (array.Contains(room))
                return false;
            array.Add(room);

            ClearRoom += room.ResetRoom;

            foreach (DoorInfo door in room.doorList)
            {
                if (door.connectedDoor != null && door.CanWalkInto(afterSwitch ? Program.pathingLogicAfter : Program.pathingLogicBefore))
                {
                    if (CanEnterFirstRoom(door.connectedDoor.BaseRoom, array, afterSwitch))
                        return true;
                }
            }
            foreach (RoomConnection connection in room.connectionList)
            {
                RoomInfo otherRoom = connection.GetOtherRoom(room);
                if (connection.CanPassThrough(otherRoom, afterSwitch ? Program.pathingLogicAfter : Program.pathingLogicBefore))
                {
                    if (CanEnterFirstRoom(otherRoom, array, afterSwitch))
                        return true;
                }
            }
            return false;
        }
        private static DoorPairing[] FindPossiblePairs(RoomInfo[] listOne, RoomInfo[] listTwo, bool? onlyConnected = null)
        {
            List<DoorPairing> retVal = new List<DoorPairing>();
            foreach (RoomInfo outRoom in listOne)
            {
                foreach (DoorInfo outDoor in outRoom.doorList)
                {
                    retVal.AddRange(FindPossiblePairs(listTwo, outDoor, onlyConnected));
                }
            }
            return retVal.ToArray();
        }
        private static DoorPairing[] FindPossiblePairs(RoomInfo[] listOne, DoorInfo two, bool? onlyConnected = null)
        {
            List<DoorPairing> retVal = new List<DoorPairing>();
            foreach (RoomInfo outRoom in listOne)
            {
                foreach (DoorInfo outDoor in outRoom.doorList)
                {
                    if (outDoor == two)
                        continue;

                    DoorType type = (DoorType)(-(int)outDoor.doorType);

                    if ((onlyConnected == null || (onlyConnected.Value == (two.connectedDoor != null))) && two.doorType == type)
                    {
                        retVal.Add(new DoorPairing(outDoor, two));
                    }
                }
            }
            return retVal.ToArray();
        }
        //private static DoorInfo GetRandomDoor(DoorInfo door)
        //{

        //}

        private static void RandomizeEntities(List<RoomInfo> accessFromStart)
        {
            if (!hasEntities)
                return;
            RoomInfo randomRoom = null;
            do
            {
                randomRoom = accessFromStart[Program.GetRandomInt(accessFromStart.Count)];
                if (randomRoom.entityList.Count == 0)
                    randomRoom = null;
            }
            while (randomRoom == null);

            randomRoom.entityList[Program.GetRandomInt(randomRoom.entityList.Count)].entityType = EntityType.FrogSwitch;
            randomRoom = null;

            for (int i = 1; i <= 9; i++)
            {
                randomRoom = null;
                do
                {
                    randomRoom = roomList[Program.GetRandomInt(roomList.Count)];
                    if (randomRoom.entityList.Count == 0)
                    {
                        randomRoom = null;
                        continue;
                    }
                    bool canPlaceEntity = false;
                    foreach (EntityInfo ent in randomRoom.entityList)
                    {
                        if (ent.entityType == EntityType.BigGem)
                        {
                            canPlaceEntity = true;
                            break;
                        }
                    }
                    if (!canPlaceEntity)
                        randomRoom = null;
                }
                while (randomRoom == null);

                int entityNumber = -1;
                do
                {
                    entityNumber = Program.GetRandomInt(randomRoom.entityList.Count);

                    if (randomRoom.entityList[entityNumber].entityType != EntityType.BigGem)
                        entityNumber = -1;
                }
                while (entityNumber == -1);
                randomRoom.entityList[entityNumber].entityType = (EntityType)i;

                if (i == 6) i = 8;
            }
        }

        private static void LoadRoomConnection(XmlNode node)
        {
            RoomInfo roomOne = null, roomTwo = null;

            foreach (RoomInfo room in roomList)
            {
                if (room.roomName == node["firstRoom"].InnerText)
                {
                    roomOne = room;
                }
                if (room.roomName == node["secondRoom"].InnerText)
                {
                    roomTwo = room;
                }
            }

            if (roomOne != null && roomTwo != null)
            {
                ConnectionTypes f2S = (ConnectionTypes)int.Parse(node["firstToSecond"].InnerText), s2F = (ConnectionTypes)int.Parse(node["secondToFirst"].InnerText);

                RoomConnection connection;
                roomOne.connectionList.Add(connection = new RoomConnection(roomOne, roomTwo, f2S, s2F));
                roomTwo.connectionList.Add(connection);
            }
        }
        private static void LoadRoom(XmlNode roomNode)
        {
            string name = roomNode.Name.Split('_')[1];
            int vanillaRoomNumber = int.Parse(name.Contains('-') ? name.Split('-')[0] : name);
            if (!vanillaRoomNumbers.Contains(vanillaRoomNumber))
                vanillaRoomNumbers.Add(vanillaRoomNumber);

            RoomInfo room;
            roomList.Add(room = new RoomInfo(name));

            foreach (XmlNode node in roomNode.ChildNodes)
            {
                switch (node.Name)
                {
                    case "door":
                        DoorInfo door = LoadDoor(node, room);
                        break;
                    case "entity":
                        room.entityList.Add(vanillaEntities[vanillaRoomNumber][int.Parse(node.InnerText)]);
                        break;
                }
            }
        }
        private static DoorInfo LoadDoor(XmlNode doorNode, RoomInfo baseRoom)
        {
            DoorInfo door;
            ConnectionTypes enterType = ConnectionTypes.Default, exitType = ConnectionTypes.Default;
            DoorType doorType = DoorType.Door;
            int index = 0;
            
            foreach (XmlNode node in doorNode)
            {
                switch (node.Name)
                {
                    case "romIndex":
                        index = int.Parse(node.InnerText);
                        break;
                    case "type":
                        doorType = GetDoorType(node.InnerText);
                        break;
                    case "exitStyle":
                        exitType = (ConnectionTypes)int.Parse(node.InnerText);
                        break;
                    case "enterStyle":
                        enterType = (ConnectionTypes)int.Parse(node.InnerText);
                        break;
                }
            }

            doorList.Add(door = new DoorInfo(doorType, exitType, enterType, index, baseRoom));
            baseRoom.doorList.Add(door);
            doorPairs.AddDoor(door);

            return door;
        }
        private static DoorType GetDoorType(string value)
        {
            int typeValue;
            if (!int.TryParse(value, out typeValue))
            {
                return (DoorType)Enum.Parse(typeof(DoorType), value);
            }
            return (DoorType)typeValue;
        }

        private static void Clear()
        {
            roomList.Clear();
            doorList.Clear();
            entityList.Clear();
            vanillaRoomNumbers.Clear();
            vanillaEntities.Clear();
            doorPairs.Clear();
        }
    }
    struct DoorPairing
    {
        public DoorInfo first, second;
        public DoorPairing(DoorInfo _first, DoorInfo _second)
        {
            first = _first;
            second = _second;
        }
    }
    class DoorPairingsTracker
    {
        private Dictionary<DoorType, List<DoorInfo>> unconnectedDoors;
        private List<DoorPairing> connectedPairs;

        public DoorPairingsTracker()
        {
            connectedPairs = new List<DoorPairing>();
            unconnectedDoors = new Dictionary<DoorType, List<DoorInfo>>();
            unconnectedDoors.Add(DoorType.HallLeft, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.HallRight, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.PipeBottom, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.PipeTop, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.Ceiling, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.Pit, new List<DoorInfo>());
            unconnectedDoors.Add(DoorType.Door, new List<DoorInfo>());
        }

        public void Clear()
        {
            foreach (List<DoorInfo> info in unconnectedDoors.Values)
            {
                info.Clear();
            }
        }
        public void FinalizeDoors()
        {
        }
        public void ConnectLeftoverDoors()
        {
            for (int i = 1; i <= 3; i++)
            {
                if (!unconnectedDoors.ContainsKey((DoorType)i))
                    continue;
                List<DoorInfo> firstList = unconnectedDoors[(DoorType)i];
                List<DoorInfo> secondList = unconnectedDoors[(DoorType)(-i)];
                if (firstList.Count > 0)
                {
                    while (firstList.Count > 0)
                    {
                        ConnectDoors(firstList[0], secondList[Program.GetRandomInt(secondList.Count)]);
                    }
                }
            }
            if (unconnectedDoors.ContainsKey(DoorType.Door))
            {
                List<DoorInfo> doors = unconnectedDoors[DoorType.Door];
                while (doors.Count > 0)
                {
                    int index = Program.GetRandomInt(doors.Count - 1) + 1;
                    ConnectDoors(doors[0], doors[index]);
                }
            }
        }

        public void AddDoor(DoorInfo door)
        {
            unconnectedDoors[door.doorType].Add(door);
        }

        public void ConnectDoors(DoorInfo one, DoorInfo two)
        {
            if (!unconnectedDoors[one.doorType].Contains(one) || !unconnectedDoors[two.doorType].Contains(two))
                return;
            unconnectedDoors[one.doorType].Remove(one);
            unconnectedDoors[two.doorType].Remove(two);
            connectedPairs.Add(new DoorPairing(one, two));
            one.Connect(two);
        }
        public void DisconnectDoor(DoorInfo door)
        {
            DoorPairing? pairing = null;
            foreach (DoorPairing pair in connectedPairs)
            {
                if (door == pair.first || door == pair.second)
                {
                    pairing = pair;
                    break;
                }
            }
            
            if (pairing != null)
            {
                DoorInfo one = pairing.Value.first, two = pairing.Value.second;
                connectedPairs.Remove(pairing.Value);
                unconnectedDoors[one.doorType].Add(one);
                unconnectedDoors[two.doorType].Add(two);
                one.connectedDoor = null;
                two.connectedDoor = null;
            }
        }

        public DoorInfo GetRandomUnusedDoorOfType(DoorType type)
        {
            if (unconnectedDoors[type].Count == 0)
                return null;
            return unconnectedDoors[type][Program.GetRandomInt(unconnectedDoors[type].Count)];
        }
        public DoorInfo GetRandomUnusedDoorOfConnectingType(DoorType type)
        {
            if (type != DoorType.Door)
                type = (DoorType)(-(int)type);

            if (unconnectedDoors[type].Count == 0)
                return null;
            return unconnectedDoors[type][Program.GetRandomInt(unconnectedDoors[type].Count)];
        }

        public bool HasConnections(DoorInfo info, ConnectionTypes types)
        {
            DoorType type = (DoorType)(-(int)info.doorType);
            //return true;
            foreach (DoorInfo door in unconnectedDoors[type])
            {
                if (door.CanWalkInto(types))
                {
                    return true;
                }
            }
            return false;
        }
        public int GetUnusedSize(DoorType type)
        {
            return unconnectedDoors[type].Count;
        }
    }
}
