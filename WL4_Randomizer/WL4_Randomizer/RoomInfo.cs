using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WL4_Randomizer
{
    class RoomInfo
    {
        public string roomName { get; private set; }
        public List<DoorInfo> doorList = new List<DoorInfo>();
        public List<EntityInfo> entityList = new List<EntityInfo>();
        public List<RoomConnection> connectionList = new List<RoomConnection>();

        public RoomInfo(string _name)
        {
            roomName = _name;
        }

        public void ResetRoom(List<RoomInfo> removeList, List<RoomInfo> addList, Dictionary<DoorType, List<DoorInfo>> dictionary)
        {
            foreach (DoorInfo door in doorList)
            {
                if (door.connectedDoor != null)
                {
                    if (!dictionary[door.doorType].Contains(door.connectedDoor))
                        dictionary[door.doorType].Add(door.connectedDoor);
                    if (!dictionary[door.connectedDoor.doorType].Contains(door))
                        dictionary[door.connectedDoor.doorType].Add(door);

                    door.connectedDoor.connectedDoor = null;
                    door.connectedDoor = null;
                }
            }
            if (removeList != null)
                removeList.Remove(this);
            if (addList != null && !addList.Contains(this) && doorList.Count > 0)
                addList.Add(this);

            Program.DebugLog("Clearing room " + roomName);
        }

        public override string ToString()
        {
            return "Room " + roomName + ", " + doorList.Count + " doors";
        }
    }
}
