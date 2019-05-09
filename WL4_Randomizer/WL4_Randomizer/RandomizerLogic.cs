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
    delegate void ClearRoomEvent(List<RoomInfo> removeList, List<RoomInfo> addList, Dictionary<DoorType, List<DoorInfo>> doorDictionary);
    class PathCreator
    {
        public const int DoorTableLocation = 0x78F21C, LevelHeadersLocation = 0x639068, LevelHeaderIndexLocation = 0x6391C4, RoomDataTableLocation = 0x78F280;

        private static List<RoomInfo> roomList = new List<RoomInfo>(), oneDoorRooms = new List<RoomInfo>();
        private static List<RoomInfo> roomsNotInLogic = new List<RoomInfo>();
        private static List<DoorInfo> doorList = new List<DoorInfo>();
        private static List<EntityInfo> entityList = new List<EntityInfo>();

        private static List<int> vanillaRoomNumbers = new List<int>();
        private static List<List<EntityInfo>> vanillaEntities = new List<List<EntityInfo>>();

        /// <summary>
        /// Doors are sorted by the OPPPOSITE of what their own type is for easier searching
        /// </summary>
        private static Dictionary<DoorType, List<DoorInfo>> existingDoors = new Dictionary<DoorType, List<DoorInfo>>();
        private static RoomInfo portalRoom;

        private static event ClearRoomEvent ClearRoom;

        private static int count;

        public static EntityInfo[] GetEntities(int passage, int level, int room)
        {
            int roomIndex = Program.GetLevelIndex(passage, level);
            //Program.DebugLog(roomIndex);
            roomIndex = Program.GetPointer(RoomDataTableLocation + roomIndex * 4) + room * 0x2c;
            //Program.DebugLog(roomIndex.ToString("X"));
            roomIndex = Program.GetPointer(roomIndex + 0x20);
            //Program.DebugLog(roomIndex.ToString("X"));

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
            Console.WriteLine("--- Randomizing level {0};{1} ---", passage + 1, level + 1);
            int levelHeaderPointer = LevelHeadersLocation + Program.GetLevelIndex(passage, level) * 12;
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

            List<RoomInfo> accessFromStart = new List<RoomInfo>();
            roomsNotInLogic.AddRange(roomList);
            foreach (RoomInfo info in roomList)
            {
                if (info.doorList.Count == 1)
                    oneDoorRooms.Add(info);
            }

            BranchLogic(accessFromStart, portalRoom);

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
                    List<DoorPairings> pairs = new List<DoorPairings>();
                    foreach (RoomInfo outRoom in roomsNotInLogic)
                    {
                        foreach (DoorInfo outDoor in outRoom.doorList)
                        {
                            DoorType type = (DoorType)(-(int)outDoor.doorType);

                            foreach (RoomInfo inRoom in accessFromStart)
                            {
                                foreach (DoorInfo inDoor in inRoom.doorList)
                                {
                                    if (inDoor.doorType == type && inDoor.connectedDoor != null)
                                    {
                                        pairs.Add(new DoorPairings(outDoor, inDoor));
                                    }
                                }
                            }
                        }
                    }
                    DoorPairings pair = pairs[Program.GetRandomInt(pairs.Count)];
                    /// Pick a random pair.
                    /// If no pairs exist, throw error
                    /// 
                }
                /// while failed
                do
                {
                    /// Pick random room in logic...
                    int v = Program.GetRandomInt(accessFromStart.Count);
                    Program.DebugLog("Index {0}", v); 
                    outOfLogicDoor = null;

                    RoomInfo inLogicRoom = accessFromStart[v], outofLogicRoom;

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
                //if (!existingDoors[secondDoor.doorType].Contains(firstDoor))
                     existingDoors[secondDoor.doorType].Add(firstDoor);
                //if (!existingDoors[firstDoor.doorType].Contains(secondDoor))
                     existingDoors[firstDoor.doorType].Add(secondDoor);

                firstDoor.connectedDoor = null;
                secondDoor.connectedDoor = null;

                /// if one of the two in logic room isn't connected to portal
                List<RoomInfo> checkedRooms = new List<RoomInfo>();
                if (!IsConnectedToFirstRoom(roomOne, checkedRooms, false))
                {
                    ClearRoom(accessFromStart, roomsNotInLogic, existingDoors);
                    checkedRooms.Clear();
                    ClearRoom = null;
                }
                if (!IsConnectedToFirstRoom(roomTwo, checkedRooms, false))
                {
                    ClearRoom = null;
                    checkedRooms.Clear();

                    BranchLogic(accessFromStart, outOfLogicDoor.BaseRoom);

                    existingDoors[secondDoor.doorType].Remove(firstDoor);
                    existingDoors[firstDoor.doorType].Remove(secondDoor);
                    firstDoor.Connect(secondDoor);

                    continue;
                }

                existingDoors[inLogicDoor.doorType].Remove(outOfLogicDoor);
                existingDoors[outOfLogicDoor.doorType].Remove(inLogicDoor);
                inLogicDoor.Connect(outOfLogicDoor);

                BranchLogic(accessFromStart, outOfLogicDoor.BaseRoom);
                
                //if (roomsNotInLogic.Count == 0 && passage > 0)
                //{
                //    bool softlockSafe = true;

                //    foreach (RoomInfo room in oneDoorRooms)
                //    {
                //        if (!IsConnectedToFirstRoom(room, checkedRooms, true))
                //        {
                //            softlockSafe = false;
                //            ClearRoom += room.ResetRoom;
                //        }
                //        checkedRooms.Clear();
                //    }

                //    if (!softlockSafe)
                //    {
                //        ClearRoom(accessFromStart, roomsNotInLogic, existingDoors);
                //        for (int i = 0; i < Math.Ceiling(roomList.Count / 5f); i++)
                //        {
                //            roomList[Program.GetRandomInt(roomList.Count, 1)].ResetRoom(accessFromStart, roomsNotInLogic, existingDoors);
                //        }
                //    }
                //}
                checkedRooms.Clear();

                //if (count < 20 || (count < 200 && (count%10) == 0) || (count%100) == 0)
                //    Program.DebugLog(count++);
            }

            ConnectLeftoverDoors();


            RandomizeEntities(accessFromStart);

            int doorArrayStart = Program.GetPointer(DoorTableLocation + Program.GetLevelIndex(passage, level) * 4);
            foreach (RoomInfo room in accessFromStart)
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
                if (door.connectedDoor == null && door.CanWalkOutOf(Program.pathingLogicBefore) && HasConnections(door, Program.pathingLogicBefore))
                {
                    DoorInfo newDoor = null;
                    List<DoorInfo> neededList = existingDoors[door.doorType];
                    do
                    {
                        int newNumber = Program.GetRandomInt(neededList.Count);
                        newDoor = neededList[newNumber];
                        if (!newDoor.CanWalkInto(Program.pathingLogicBefore) || newDoor == door)
                            newDoor = null;
                    }
                    while (newDoor == null);
                    door.Connect(newDoor);

                    //Program.DebugLog("Door " + room.doorList.IndexOf(door) + " of room " + room.roomName + " connected to door " + newDoor.BaseRoom.doorList.IndexOf(newDoor) + " of room " + newDoor.BaseRoom.roomName);

                    neededList.Remove(newDoor);
                    existingDoors[newDoor.doorType].Remove(door);
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
                if (door.connectedDoor == null && door.CanWalkOutOf(Program.pathingLogicAfter) && HasConnections(door, Program.pathingLogicBefore))
                {
                    DoorInfo newDoor = null;
                    List<DoorInfo> neededList = existingDoors[door.doorType];
                    do
                    {
                        newDoor = neededList[Program.GetRandomInt(neededList.Count)];
                        if (!newDoor.CanWalkInto(Program.pathingLogicAfter) || newDoor == door)
                            newDoor = null;
                    }
                    while (newDoor == null);

                    neededList.Remove(newDoor);
                    existingDoors[newDoor.doorType].Remove(door);

                    door.Connect(newDoor);
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
        private static bool IsConnectedToFirstRoom(RoomInfo room, List<RoomInfo> array, bool backtracking)
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

            if (!backtracking)
                ClearRoom += room.ResetRoom;

            foreach (DoorInfo door in room.doorList)
            {
                if (door.connectedDoor != null && (!backtracking || door.CanWalkOutOf(Program.pathingLogicBefore | Program.pathingLogicAfter)))
                {
                    if (IsConnectedToFirstRoom(door.connectedDoor.BaseRoom, array, backtracking))
                        return true;
                }
            }
            foreach(RoomConnection connection in room.connectionList)
            {
                RoomInfo otherRoom = connection.GetOtherRoom(room);
                if (connection.CanPassThrough(otherRoom, Program.pathingLogicBefore))
                {
                    if (IsConnectedToFirstRoom(otherRoom, array, backtracking))
                        return true;
                }
            }
            return false;
        }
        private static bool HasConnections(DoorInfo info, ConnectionTypes types)
        {
            //return true;
            foreach (DoorInfo door in existingDoors[info.doorType])
            {
                if (door.CanWalkInto(types))
                {
                    Program.DebugLog("Found doorway");
                    return true;
                }
            }
            return false;
        }
        //private static DoorInfo GetRandomDoor(DoorInfo door)
        //{

        //}

        private static void RandomizeEntities(List<RoomInfo> accessFromStart)
        {
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
            DoorType inverseDoorType = (DoorType)(-(int)door.doorType);
            if (!existingDoors.ContainsKey(inverseDoorType))
                existingDoors.Add(inverseDoorType, new List<DoorInfo>());
            existingDoors[inverseDoorType].Add(door);

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

        private static void ConnectLeftoverDoors()
        {
            for (int i = 1; i <= 3; i++)
            {
                if (!existingDoors.ContainsKey((DoorType)i))
                    continue;
                List<DoorInfo> firstList = existingDoors[(DoorType)i];
                List<DoorInfo> secondList = existingDoors[(DoorType)(-i)];
                if (firstList.Count > 0)
                {
                    while (firstList.Count > 0)
                    {
                        DoorInfo door = firstList[0];
                        door.Connect(secondList[Program.GetRandomInt(secondList.Count)]);

                        secondList.Remove(door.connectedDoor);
                        firstList.Remove(door);
                    }
                }
            }
            if (existingDoors.ContainsKey(DoorType.Door))
            {
                List<DoorInfo> doors = existingDoors[DoorType.Door];
                while (doors.Count > 0)
                {
                    int index = Program.GetRandomInt(doors.Count - 1) + 1;
                    doors[0] = doors[index];
                    doors.RemoveAt(index);
                    doors.RemoveAt(0);
                }
            }
        }

        private static void Clear()
        {
            roomList.Clear();
            doorList.Clear();
            entityList.Clear();
            vanillaRoomNumbers.Clear();
            vanillaEntities.Clear();
            existingDoors.Clear();
        }
    }
    struct DoorPairings
    {
        public DoorInfo first, second;
        public DoorPairings(DoorInfo _first, DoorInfo _second)
        {
            first = _first;
            second = _second;
        }
    }
}
