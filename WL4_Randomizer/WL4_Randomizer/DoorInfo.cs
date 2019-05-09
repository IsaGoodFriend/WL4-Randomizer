using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WL4_Randomizer
{
    enum DoorType
    {
        Door = 0,
        HallLeft = 1,
        HallRight = -1,
        Ceiling = -2,
        Pit = 2,
        PipeTop = -3,
        PipeBottom = 3,
    }
    ///         foreach door
    ///             Create door in code
    ///             Set door type from file
    ///             Get/set door rom index from file
    ///             Set flags from file
    ///             Set room pointer
    ///             Add door to room
    class DoorInfo
    {
        public bool isIgnored { get; private set; }
        public RoomInfo BaseRoom { get; private set; }
        public ConnectionTypes enterStyle { get; private set; }
        public ConnectionTypes exitStyle { get; private set; }
        public DoorType doorType { get; private set; }
        public int romArrayIndex { get; private set; }
        public DoorInfo connectedDoor { get; set; } = null;

        /// <summary>
        /// Create a new doorway for XML file
        /// </summary>
        /// <param name="baseIndex"></param>
        /// <param name="doorIndex"></param>
        /// <param name="buffer"></param>
        /// <param name="dictionary"></param>
        /// <param name="vanillaRooms"></param>
        public DoorInfo(int baseIndex, int doorIndex, ref Dictionary<string, RoomInfo> dictionary, ref Dictionary<int, List<string>> vanillaRooms)
        {
            // Door type, room, (size) Left, right, top, bottom, door to link to, offsetX, offsetY, ???, ???, ???
            int index = baseIndex + doorIndex*12, roomNumber = Program.romBuffer[index + 1];
            int width = Program.romBuffer[index + 3] - Program.romBuffer[index + 2] + 1, height = Program.romBuffer[index + 5] - Program.romBuffer[index + 4] + 1;

            Console.WriteLine("Door Types - 1:Doorway ; 2:Offscreen Passage ; 3:Pipe");
            Console.WriteLine("\nDoor Info:\nIndex: " + doorIndex + "\nRoom: " + Program.romBuffer[index + 1] + "  Type: " + Program.romBuffer[index]  + "\nX:" + Program.romBuffer[index+2] + " Y:" + Program.romBuffer[index+4] + " W:" + width + " H:" + height);

            Console.WriteLine("\nIs this door used?");
            if (isIgnored = !Program.GetBoolFromUser(true)) // Get whether door is used, defaulting to true
                return;

            romArrayIndex = doorIndex;

            Console.Write("Current subrooms: ");
            foreach (string s in dictionary.Keys) // Display current rooms in 
            {
                if (s.Contains(Program.romBuffer[index + 1] + "-"))
                    Console.Write(s.Split('-')[1] + ", ");
            }

            Console.WriteLine("\nWhat room should this be in? (blank for default)");
            string input = Program.ReadLine().ToLower();
            string roomName = roomNumber + "-" + (input == "" ? "default" : input);

            if (!dictionary.ContainsKey(roomName))
            {
                dictionary.Add(roomName, new RoomInfo(roomName));
                if (!vanillaRooms.ContainsKey(roomNumber))
                    vanillaRooms.Add(roomNumber, new List<string>());

                vanillaRooms[roomNumber].Add(roomName);
            }
            (BaseRoom = dictionary[roomName]).doorList.Add(this);


            //Determine type of door
            switch (Program.romBuffer[index])
            {
                case 0x01:
                    doorType = DoorType.Door;
                    break;
                case 0x02:
                    byte x = Program.romBuffer[index + 7], y = Program.romBuffer[index + 8]; // Get x/y offset to detect type of door

                    if (y == 0 && x != 0)
                        doorType = x > 128 ? DoorType.HallRight : DoorType.HallLeft;
                    else
                        doorType = y > 128 ? DoorType.Pit : DoorType.Ceiling;

                    break;
                case 0x03:
                    doorType = DoorType.PipeBottom;
                    break;
            }

            Console.WriteLine("This doorway looks like a " + doorType.ToString() + ".  Is this correct?");
            if (!Program.GetBoolFromUser(true))
            {

                //HallLeft = 1,
                //HallRight = -1,
                //Pit = 2,
                //Ceiling = -2,
                //PipeTop = 3,
                //PipeBottom = -3,
                Console.WriteLine("Door Types - 0:Door ; 1:HallLeft ; 2:Pit ; 3:PipeBottom ; -1:HallRight ; -2:Ceiling ; -3:PipeTop");
                input = Program.ReadLine();
                doorType = (DoorType)int.Parse(input);
            }

            Console.WriteLine("Enter/Exit Types - " + Program.CONNECTION_TYPES_DISPLAY);
            Console.WriteLine("\nHow can you enter this room from this doorway? (add commas to separate)");
            enterStyle = Program.ReadConnectionValues();
            Console.WriteLine("How can you exit this room with this doorway?");
            exitStyle = Program.ReadConnectionValues();
        }

        /// <summary>
        /// Create a new doorway for randomizing roms
        /// </summary>
        public DoorInfo(DoorType _type, ConnectionTypes _exit, ConnectionTypes _enter, int _romIndex, RoomInfo _baseRoom)
        {
            BaseRoom = _baseRoom;
            doorType = _type;
            enterStyle = _enter;
            exitStyle = _exit;
            romArrayIndex = _romIndex;
        }

        public void SetRomInfo(int offset)
        {
            Console.WriteLine(Program.romBuffer[offset + romArrayIndex * 12 + 6] + ", " + (byte)(connectedDoor == null ? 0 : connectedDoor.romArrayIndex));
            Program.romBuffer[offset + romArrayIndex * 12 + 6] = (byte)(connectedDoor == null ? romArrayIndex : connectedDoor.romArrayIndex);
        }
        public bool CanWalkInto(ConnectionTypes connect)
        {
            return (enterStyle & connect) != ConnectionTypes.None;
        }
        public bool CanWalkOutOf(ConnectionTypes connect)
        {
            return (exitStyle & connect) != ConnectionTypes.None;
        }
        public bool ClearOf(ConnectionTypes connect)
        {
            return (enterStyle & connect) == ConnectionTypes.None && (exitStyle & connect) == ConnectionTypes.None;
        }

        public void Connect(DoorInfo door)
        {
            door.connectedDoor = this;
            connectedDoor = door;
        }

        public override string ToString()
        {
            return "Base Room: " + BaseRoom.roomName;
        }
    }
}
