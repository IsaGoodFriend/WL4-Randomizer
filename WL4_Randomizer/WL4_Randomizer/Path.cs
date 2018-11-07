using System;
using System.Collections.Generic;
using System.IO;

namespace WL4_Randomizer
{
    class PathCreator
    {
        public const int DoorTableLocation = 0x78F21C, LevelHeadersLocation = 0x639068, LevelIndexLocation = 0x6391C4;

        public RoomNode[] rooms;
        public List<PathNode> path;

        public PathCreator(ref byte[] romReference, byte hall, byte level, ref string[] file, int fileOffset)
        {
            path = new List<PathNode>();

            // Get the start of the level header
            int doorTableLocation = LevelIndexLocation + hall * 24 + level * 4;
            doorTableLocation = Program.GetPointer(DoorTableLocation + romReference[doorTableLocation] * 4);

            int romIndex;
            int index = 0;
            byte roomMax = 0;
            List<byte> roomDoorIsIn = new List<byte>();
            PathType type = PathType.Door;

            // Door type, room, (size) Left, right, top, bottom, door to link to, offsetX, offsetY, ???, ???, ???

            // Reading rom for doorways/pipes
            while (romReference[doorTableLocation + index] != 0x00)
            {
                romIndex = doorTableLocation + index;
                
                if (romReference[romIndex] == 0x01 && romReference[romIndex + 6] == 0x00) // Ignore portal
                {
                    index += 12;
                    continue;
                }

                roomMax = Math.Max(roomMax, romReference[romIndex + 1]); // Get the room count, based on the largest room number
                roomDoorIsIn.Add(romReference[romIndex + 1]); // Add room index for door for future reference

                switch (romReference[romIndex])
                {
                    case 0x01:
                        type = PathType.Door;
                        break;
                    case 0x02:
                        byte x = romReference[romIndex + 7], y = romReference[romIndex + 8]; // Get x/y offset to detect type of door

                        if (y == 0 && x != 0)
                            type = x > 128 ? PathType.HallRight : PathType.HallLeft;
                        else
                            type = y > 128 ? PathType.Ceiling : PathType.Pit;

                        break;
                    case 0x03:
                        type = PathType.PipeBottom;
                        break;
                }

                path.Add(new PathNode(romIndex, type, romReference[romIndex + 6] - 1));

                if (path[path.Count - 1].connectedNodeIndex == -1)
                {
                    path[path.Count - 1].blockType = PathBlockType.Impassable;
                }

                index += 12;
            }

            rooms = new RoomNode[roomMax + 1];
            string[] substring;
            

            for (int i = 0; i <= roomMax; i++) // Foreach room
            {
                RoomNode roomNew = new RoomNode();
                roomNew.subrooms = new List<RoomSubsection>(new RoomSubsection[] { new RoomSubsection(roomNew) });

                List<PathNode> fullnodeList = new List<PathNode>();
                substring = file[fileOffset + i].Split(',');

                index = 0;
                // Take all the doors in the room and add them to their proper place
                for (int j = 0; j < roomDoorIsIn.Count; j++)
                {
                    if (roomDoorIsIn[j] == i)
                    {
                        int subroom = int.Parse(substring[index++]); // Get the subroom door needs to be contained in

                        while (subroom >= roomNew.subrooms.Count) // Make sure that the subroom the door is contained in exists
                        {
                            roomNew.subrooms.Add(new RoomSubsection(roomNew));
                        }

                        roomNew.subrooms[subroom].doorWays.Add(path[j]); // Add door to subroom
                        path[j].subroom = roomNew.subrooms[subroom];
                    }
                }

                rooms[i] = roomNew;
            }

            fileOffset += roomMax + 1;

            List<int> remove = new List<int>();

            while (file[fileOffset] != "" && file[fileOffset][0] != '/')
            {
                substring = file[fileOffset].Split(',');

                if (substring[0] == "C")
                {
                    path[int.Parse(substring[1])].pathType = (PathType)(int.Parse(substring[2]));
                }
                else if (substring[0] == "B")
                {
                    path[int.Parse(substring[1])].blockType = (PathBlockType)(int.Parse(substring[2]));
                }
                else if (substring[0] == "K")
                {
                    path[int.Parse(substring[1])].Exclude = true;
                }
                else if (substring[0] == "R")
                {
                    rooms[int.Parse(substring[1])].Exclude = true;
                }
                else if (substring[0] == "S")
                {
                    // Take the room from the second value, find the subroom from the third value, and add a connection to the subroom in the same main room at the index at the fourth value
                    rooms[int.Parse(substring[1])].subrooms[int.Parse(substring[2])].connections.Add(new SubroomConnection((PathBlockType)int.Parse(substring[4]), rooms[int.Parse(substring[1])].subrooms[int.Parse(substring[3])]));
                }
                fileOffset++;
            }

            if (hall == 1 && level == 0)
            {
                bool stop = false;
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i].Exclude)
                    {
                        foreach (RoomNode main in rooms)
                        {
                            foreach (RoomSubsection sub in main.subrooms)
                            {
                                sub.doorWays.Remove(path[i]);
                            }
                        }
                    }
                }
            }

            roomExclusive = new List<RoomNode>(rooms);
            for (int i = 0; i < rooms.Length; i++)
            {
                if (path[i].Exclude)
                {
                    roomExclusive.Remove(rooms[i]);
                }
            }

            rooms[0].subrooms[0].itemsContained |= ItemFound.Portal;
        }

        List<RoomNode> roomExclusive;
        List<PathNode> pathExclusive;

        public void CreatePath(Random rng, ref byte[] rom)
        {
            pathExclusive = new List<PathNode>(path);
            //List<PathNode> test2 = new List<PathNode>(path);
            for (int i = 0; i < pathExclusive.Count; i++)
            {
                if (path[i].Exclude)
                {
                    pathExclusive.Remove(path[i]);
                }
            }
            List<PathNode> test = new List<PathNode>(pathExclusive);

            bool isSafe = false;

            while (!isSafe)
            {
                if (test.Count > 0)
                {
                    int randomDoor = rng.Next(test.Count - 1) + 1;

                    //Grab a random door, and if it's the right type for the first in the lists, connect and remove from the list
                    if ((int)test[0].pathType == -(int)(test[randomDoor].pathType))
                    {
                        if (test[0].connectedNodeIndex >= 0) //only connect if door should be connected
                        {
                            test[0].connectedNodeIndex = path.IndexOf(test[randomDoor]);
                        }
                        if (test[randomDoor].connectedNodeIndex >= 0) //only connect if door should be connected
                        {
                            test[randomDoor].connectedNodeIndex = path.IndexOf(test[0]);
                        }
                        test.RemoveAt(randomDoor);
                        test.RemoveAt(0);
                    }
                }
                else // After all rooms have been randomized
                {
                    //Temp list to trace path
                    PathNode issue = null;
                    isSafe = TestPath(rng, out issue);

                    if (!isSafe)
                    {
                        while (true)
                        {
                            PathNode randomPath = pathExclusive[rng.Next(0, pathExclusive.Count)];
                            if (path.Contains(randomPath) && !test.Contains(randomPath) && randomPath.connectedNodeIndex != -1)
                            {
                                if (Math.Abs((int)randomPath.pathType) == Math.Abs((int)issue.pathType))
                                {
                                    if (rng.Next(2) == 0)
                                    {
                                        randomPath.connectedNodeIndex = path.IndexOf(issue);
                                        path[randomPath.connectedNodeIndex].connectedNodeIndex = issue.connectedNodeIndex;
                                    }
                                    else
                                    {
                                        randomPath.connectedNodeIndex = issue.connectedNodeIndex;
                                        path[randomPath.connectedNodeIndex].connectedNodeIndex = path.IndexOf(issue);
                                    }

                                    break;
                                }
                            }
                        }
                    }


                }
            }

            foreach (PathNode node in pathExclusive)
            {
                rom[node.doorIndex + 6] = (byte)(node.connectedNodeIndex + 1);
            }     
        }

        private bool TestPath(Random rng, out PathNode issue)
        {
            issue = null;
            
            LinkedList<RoomSubsection> roomPath = new LinkedList<RoomSubsection>();
            List<PathNode> safePaths = new List<PathNode>();
            PathBlockType blockType = PathBlockType.Impassable;

            if (!Program.useZips)
                blockType |= PathBlockType.Zip;

            if (!CheckPathSoftlocks(roomExclusive[0].subrooms[0], roomPath, safePaths, ref issue))
                return false;
            
            //Check to see if all rooms are connected
            List<RoomSubsection> roomsNotFound = new List<RoomSubsection>();
            foreach (RoomNode r in roomExclusive)
            {
                roomsNotFound.AddRange(r.subrooms);
            }
            ItemFound found = ItemFound.None;
            CheckPathOneWeb(roomExclusive[0].subrooms[0], roomPath, roomsNotFound, blockType, ref found);

            // If room has not been found, restart
            if (roomsNotFound.Count > 0)
            {
                GetIssueDoorRandom(ref issue, rng, roomsNotFound);

                return false;
            }

            RoomSubsection frog = null;
            roomsNotFound = new List<RoomSubsection>();
            foreach (RoomNode r in roomExclusive)
            {
                foreach (RoomSubsection sub in r.subrooms)
                {
                    if (frog != null)
                        break;

                    if ((sub.itemsContained & ItemFound.Frog) != ItemFound.None)
                    {
                        frog = sub;
                        break;
                    }
                }
                roomsNotFound.AddRange(r.subrooms);
            }

            // Check 
            blockType |= PathBlockType.FrogSwitchPre;
            found = ItemFound.None;
            roomPath = new LinkedList<RoomSubsection>();
            roomsNotFound = new List<RoomSubsection>();

            CheckPathOneWeb(roomExclusive[0].subrooms[0], roomPath, roomsNotFound, blockType, ref found);

            if ((found & ItemFound.Frog) == ItemFound.None) // If didn't find frog reset
            {
                GetIssueDoorRandom(ref issue, rng, roomsNotFound);

                return false;
            }
            
            blockType -= PathBlockType.FrogSwitchPre;
            blockType |= PathBlockType.FrogSwitchPost;
            found -= ItemFound.Portal;
            roomPath = new LinkedList<RoomSubsection>();
            foreach (RoomNode r in roomExclusive)
            {
                roomsNotFound.AddRange(r.subrooms);
            }

            CheckPathOneWeb(frog, roomPath, roomsNotFound, blockType, ref found);

            if ((found & ItemFound.Portal) == ItemFound.None) // If didn't find portal reset
            {
                GetIssueDoorRandom(ref issue, rng, roomsNotFound);

                return false;
            }

            RoomSubsection noLocking = null;

            blockType -= PathBlockType.FrogSwitchPost;

            foreach (RoomNode r in roomExclusive)
            {
                foreach (RoomSubsection sub in r.subrooms)
                {
                    roomPath = new LinkedList<RoomSubsection>();
                    found = ItemFound.None;

                    if (!CheckPathLockInRoom(sub, roomPath, blockType, ref found))
                        noLocking = sub;
                }
                if (noLocking != null)
                    break;
            }

            if (noLocking != null)
            {
                foreach (RoomNode r in roomExclusive)
                {
                    roomsNotFound.AddRange(r.subrooms);
                }
                roomsNotFound.Remove(noLocking);
                issue = noLocking.doorWays[0];

                GetIssueDoorRandom(ref issue, rng, roomsNotFound);
                return false;
            }

            roomPath = new LinkedList<RoomSubsection>();
            CheckPathOneWeb(roomExclusive[0].subrooms[0], roomPath, roomsNotFound, blockType, ref found);

            return true;
        }

        public void GetIssueDoorRandom(ref PathNode issue, Random rng, List<RoomSubsection> roomsNotFound)
        {
            bool loop = true;
            do
            {
                int randomDoor = rng.Next(roomsNotFound.Count);
                issue = roomsNotFound[randomDoor].doorWays[rng.Next(roomsNotFound[randomDoor].doorWays.Count)];

                if (issue.connectedNodeIndex == -1)
                    continue;

                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i].Exclude)
                        continue;

                    if (path[i] != issue && i != issue.connectedNodeIndex && Math.Abs((int)path[i].pathType) == Math.Abs((int)issue.pathType))
                    {
                        loop = false;
                        break;
                    }
                }
            }
            while (loop);
        }

        public bool CheckPathSoftlocks(RoomSubsection roomSub, LinkedList<RoomSubsection> tracingPath, List<PathNode> safe, ref PathNode issue)
        {
            bool retVal = true; // Boolean on whether path is still safe

            if (tracingPath.Contains(roomSub))
                return true;
            
            tracingPath.AddLast(roomSub); // Add subroom to not retrace steps
            
            foreach (PathNode n in roomSub.doorWays) // Check each path if safe
            {
                if (n.blockType != PathBlockType.Impassable) // If the pathway isn't impassable and the room hasn't been on the path already, 
                {
                    if (n.blockType == PathBlockType.BreakBlock_SoftLock)
                    {
                        safe.Add(n);
                    }
                    if (!safe.Contains(path[n.connectedNodeIndex]) && path[n.connectedNodeIndex].blockType == PathBlockType.BreakBlock_SoftLock)
                    {
                        retVal = false;
                        issue = n;
                        break;
                    }

                    if (!CheckPathSoftlocks(path[n.connectedNodeIndex].subroom, tracingPath, safe, ref issue))
                        retVal = false;

                    foreach (SubroomConnection sub in n.subroom.connections)
                    {
                        if (!tracingPath.Contains(sub.nextRoom) && !CheckPathSoftlocks(sub.nextRoom, tracingPath, safe, ref issue))
                        {
                            retVal = false;
                            break;
                        }
                    }
                }
                if (!retVal) break;
            }

            tracingPath.Remove(roomSub);

            return retVal;
        }
        
        public void CheckPathOneWeb(RoomSubsection roomSub, LinkedList<RoomSubsection> tracingPath, List<RoomSubsection> hasntFound, PathBlockType blocks, ref ItemFound found)
        {
            found |= roomSub.itemsContained;
            
            if (tracingPath.Contains(roomSub))
                return;

            tracingPath.AddLast(roomSub); // Add subroom to not retrace steps
            if (hasntFound.Contains(roomSub))
            {
                hasntFound.Remove(roomSub);
            }

            foreach (PathNode n in roomSub.doorWays) // Check each path if safe
            {
                if ((n.blockType & blocks) == PathBlockType.None) // If the pathway isn't impassable and the room hasn't been on the path already, 
                {
                    CheckPathOneWeb(path[n.connectedNodeIndex].subroom, tracingPath, hasntFound, blocks, ref found);
                }
                foreach (SubroomConnection sub in n.subroom.connections)
                {
                    if ((sub.issue & blocks) == PathBlockType.None)
                    {
                        CheckPathOneWeb(sub.nextRoom, tracingPath, hasntFound, blocks, ref found);
                    }
                }
            }

            tracingPath.Remove(roomSub);
        }
        public bool CheckPathLockInRoom(RoomSubsection roomSub, LinkedList<RoomSubsection> tracingPath, PathBlockType blocks, ref ItemFound found)
        {
            found |= roomSub.itemsContained;

            if (found != ItemFound.None)
                return true;

            if (tracingPath.Contains(roomSub))
                return false;

            tracingPath.AddLast(roomSub); // Add subroom to not retrace steps
            

            foreach (PathNode n in roomSub.doorWays) // Check each path if safe
            {
                if ((n.blockType & blocks) == PathBlockType.None) // If the pathway isn't impassable and the room hasn't been on the path already, 
                {
                    if (CheckPathLockInRoom(path[n.connectedNodeIndex].subroom, tracingPath, blocks, ref found))
                        return true;
                }
                foreach (SubroomConnection sub in n.subroom.connections)
                {
                    if ((sub.issue & blocks) == PathBlockType.None)
                    {
                        if (CheckPathLockInRoom(sub.nextRoom, tracingPath, blocks, ref found))
                            return true;
                    }
                }
            }
            
            tracingPath.Remove(roomSub);

            return false;
        }
    }

    struct SubroomConnection
    {
        public PathBlockType issue;
        public RoomSubsection nextRoom;

        public SubroomConnection(PathBlockType block, RoomSubsection sub)
        {
            issue = block;
            nextRoom = sub;
        }
    }
    enum ItemFound
    {
        None = 0,
        Portal = 1,
        Frog = 2,
        Keyzer = 4,
        Gem1 = 8,
        Gem2 = 16,
        Gem3 = 32,
        Gem4 = 64
    }
    enum PathType
    {
        Door = 0,
        HallLeft = 1,
        HallRight = -1,
        Pit = 2,
        Ceiling = -2,
        PipeTop = 3,
        PipeBottom = -3,
    }
    enum PathBlockType
    {
        None = 0,
        FrogSwitchPre = 1,
        FrogSwitchPost = 2,
        RedToggle = 4,
        PurpleToggle = 8,
        GreenToggle = 16,
        BigBoardBlocks = 32,
        Zip = 64,

        Impassable = 65536,
        BreakBlock_SoftLock = 128
    }
    class PathNode
    {
        public PathType pathType;
        public PathBlockType blockType;
        public int connectedNodeIndex;
        public RoomSubsection subroom;
        public bool Exclude = false;

        public int doorIndex;

        public PathNode(int index, PathType type, int _nodeIndex)
        {
            pathType = type;
            doorIndex = index;
            connectedNodeIndex = _nodeIndex;
        }
    }

    class RoomNode
    {
        public int EntityListIndex;
        public List<RoomSubsection> subrooms;
        public bool Exclude = false;

        public RoomNode(params RoomSubsection[] rooms)
        {
            subrooms = new List<RoomSubsection>(rooms);
        }
    }
    class RoomSubsection
    {
        public RoomNode parentRoom;
        public List<PathNode> doorWays;
        public List<SubroomConnection> connections;
        public ItemFound itemsContained;

        public RoomSubsection(RoomNode room)
        {
            parentRoom = room;
            doorWays = new List<PathNode>();
            connections = new List<SubroomConnection>();
        }
    }
}
