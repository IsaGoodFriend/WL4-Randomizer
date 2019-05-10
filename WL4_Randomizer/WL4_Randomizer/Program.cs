//#define TEMP

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using System.Threading;

namespace WL4_Randomizer
{
    enum ConnectionTypes
    {
        None = 0,
        Default = 1,
        BeforeSwitch = 2,
        AfterSwitch = 4,
        MinionJump = 8,
        HeavyMinionJump = 16,
        Zip = 32,
        DifficultMinionJumps = 64
    }
    enum PickupType
    {
        Nothing,
        Piece1,
        Piece2,
        Piece3,
        Piece4,
        CD,
        Heart,
        Gem,
        Frog,
        Keyzer
    }
    class Program
    {
        public static Dictionary<int, int> roomPointerTemp = new Dictionary<int, int>();

        public const string CONNECTION_TYPES_DISPLAY = "1:Normal ; 2:Before Timer ; 4:After Timer\n8:Minion Jump ; 16:Heavy Minion Jump ; 32:Zip ; 64:Difficult Minion Jump\nBlank will give value of 1, put 0 if blocked one way";
        private static ConnectionTypes pathingLogic = ConnectionTypes.Default;
        public static ConnectionTypes pathingLogicBefore { get { return pathingLogic | ConnectionTypes.BeforeSwitch; } }
        public static ConnectionTypes pathingLogicAfter { get { return pathingLogic | ConnectionTypes.AfterSwitch; } }
        public static string LunarIPSPath { get { return directory + "\\flips.exe"; } }
        public static string PatchPath { get { return patchPath + ".bps"; } }
        public static string OriginalRomPath { get; private set; }
        public static string RandomizedRomPath { get { return randomizedRomName + ".gba"; } }

        private const int TXT_ROOM_LENGTH = 10;
        private static string directory = Directory.GetCurrentDirectory();
        private static string randomizedRomName, originPathName, patchPath = "vanilla";

        static Random rngGen;

        static string[] options;

        public static byte[] romBuffer { get; set; }
        static bool hasFinished;

        /// <summary>
        /// Pseudo code for randomizing rom
        /// 
        /// Level Pathing Logic Data:
        ///     Array of Rooms
        ///     Array of Doors
        ///     First Room
        /// 
        /// Room Data:
        ///     Room name
        ///     Array of Doors contained within
        ///     Array of extra connections
        ///     Array of rom pointers to entities
        ///     
        /// Room Connection Data:
        ///     Pointers to the first and second room
        ///     Flags signifying if door can be entered and/or exited from for every style. (all, before/after frog, enemy jump, zips, etc)
        ///         It should be laid out in two flag enums, one for entering, one for exiting
        ///         
        /// Entity Data:
        ///     Rom Pointer to entity information (x, y, and type)
        ///     Current Entity type (defaults to gem)
        /// 
        /// Door Data
        ///     Door Type (from file)
        ///     Room Number (From rom)?
        ///     Index of door in Rom array (from rom)
        ///     Pointer to Room in code (from code)
        ///     Rom Pointer to the connected door's index (from rom)
        ///     Pointer to connected door in code (from code)
        ///     Flags signifying if door can be entered and/or exited from for every style. (all, before/after frog, enemy jump, zips, etc) (from file)
        ///         It should be laid out in two flag enums, one for entering, one for exiting
        /// 
        /// Get RNG Seed from user
        /// Clone original rom to new file.
        /// Run patch on clone
        /// Get byte array for rom buffer
        /// 
        /// foreach level
        ///     Clear base room, door, and entity array
        ///     Read file for entity, room, and door data
        ///         foreach room
        ///             Create room in code
        ///             foreach door
        ///                 Create door in code
        ///                 Set door type from file
        ///                 Get/set door rom index from file
        ///                 Set flags from file
        ///                 Set room pointer
        ///                 Set connected door rom index
        ///         
        ///     Assign first room
        ///     Assign arrays of doors that can be entered into, each array indicating what kind of door it is.
        ///     Create an array of entities for the level
        ///     Create empty Room array to count rooms able to be accessed before frog switch
        ///     Create empty Door array for doors that can only be accessed after
        ///     Branching method: (creating before frog switch layout)
        ///         Add room to room array
        ///         foreach Door in room:
        ///             if door is connected, ignore
        ///             Get a random door, check to see if it's able to be passed through.  
        ///             If it is, remove the door from array, connect it to this door, and make a branch with that door's room
        ///             If it isn't, but can be branched after switch, add to door array
        ///             foreach connection
        ///                 if is able to pass through, branch with connected room
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
        ///     Pick random rooms and place the gems, cd, and keyser in them
        ///     Foreach door, set new door connection in buffer
        ///     Foreach entity, set type and position
        /// 
        /// Shuffle levels
        /// Write changes to rom from buffer
        /// </summary>
        /// <param name="args"></param>
        private static void CreateRandomizedRom()
        {
            XmlDocument reader = new XmlDocument();
            reader.Load(directory + "\\" + patchPath + ".xml");
            XmlReader read = XmlReader.Create(directory + "\\" + patchPath + ".xml");

            foreach (XmlNode node in reader.DocumentElement)
            {
                string name = node.Name;

                int passage = int.Parse(name.Split('_')[1]);
                int level = int.Parse(name.Split('_')[2]);

                PathCreator.CreatePath(passage, level, node);
            }

            //RandomizeLevels();
            romBuffer[PathCreator.LevelHeaderIndexLocation] = 0x2;

            Console.WriteLine("Writing to " + directory + "\\" + RandomizedRomPath);
            File.WriteAllBytes(directory + "\\" + RandomizedRomPath, romBuffer);
            Console.WriteLine("Randomized rom saved.");
            Console.ReadLine();
        }

        public static void Main(string[] args)
        {
            roomPointerTemp.Add(4 * 6 + 0, 12);
            roomPointerTemp.Add(4 * 6 + 1, 13);
            roomPointerTemp.Add(4 * 6 + 2, 14);
            roomPointerTemp.Add(4 * 6 + 3, 15);

            if (args.Length == 0)
            {
                args = new string[] { directory + "\\WarioLand4Original.gba" };
            }
            else if (args.Length == 1)
            {

            }
            else
            {
                return;
            }
            OriginalRomPath = args[0].Remove(0, directory.Length+1);

            if (!File.Exists(args[0])) return;

            Console.WriteLine("Do you want to use your previous settings?");

            string s = Console.ReadLine().ToLower();

            if (s == "y" || s == "yes" || s == "yes." || s == "1")
            {
                inputs = File.ReadAllLines(directory + "\\input.txt");
            }
            else
            {
                inputs = new string[] { };
                File.WriteAllLines(directory + "\\input.txt", inputs);
            }
            
            Console.WriteLine("Do you want to create a randomized rom? y/n");

            bool createRandomizer = GetBoolFromUser(true);
            if (!createRandomizer)
            {
                Console.WriteLine("Then perhaps a randomizer info doc? y/n");

                if (!GetBoolFromUser(true))
                    return;
            }

            Console.WriteLine("A new rom will be created, and given a patch.  Please wait until the console says \"The Patch was applied successfully\" before continuing.");

            if (createRandomizer)
            {
                randomizedRomName = patchPath + SetupRNG();
            }
            else
            {
                randomizedRomName = patchPath;
            }
            Console.WriteLine();

            MakeCopy(OriginalRomPath, RandomizedRomPath, PatchPath);
            Console.ReadLine();
            romBuffer = File.ReadAllBytes(directory + "\\" + RandomizedRomPath);
            Console.Clear();

            if (!createRandomizer)
            {
                CreateRandoInformation();
            }
            else
            {
                CreateRandomizedRom();
            }
        }

        private static string[] inputs;
        private static int inputIndex;
        public static void WriteToFile(string logMessage, string logFile)
        {
            logFile = directory + "\\" + logFile + ".txt";
            if (!File.Exists(logFile))
                File.Create(logFile).Close();

            List<string> newFile = new List<string>(File.ReadAllLines(logFile));
            newFile.Add(logMessage);

            File.WriteAllLines(logFile, newFile.ToArray());
        }
        public static string ReadLine()
        {
            if (inputs != null)
            {
                while (inputIndex < inputs.Length && inputs[inputIndex].Contains("--comment"))
                {
                    inputIndex++;
                }
                if (inputIndex < inputs.Length)
                {
                    if (inputs[inputIndex].Contains("--pause"))
                        inputIndex++;
                    else
                    {
                        Console.WriteLine(inputs[inputIndex]);
                        return inputs[inputIndex++];
                    }
                }
            }
            string s = Console.ReadLine();
            
            if (inputIndex >= inputs.Length/* && inputs.Length > 0*/)
            {
                WriteToFile(s, "input");
            }
            return s;
        }
        public static void DebugLog(object obj)
        {
            Console.WriteLine(obj.ToString());
        }
        public static void DebugLog(string s, params object[] args)
        {
            Console.WriteLine(s, args);
        }
        public static bool GetBoolFromUser(bool defaultAnswer)
        {
            string s = ReadLine().ToLower();
            return defaultAnswer ? !(s == "n" || s == "no" || s == "no." || s == "0") : (s == "y" || s == "yes" || s == "yes." || s == "1");
        }

        /// <summary>
        /// Eventual features:
        ///     Start Level with switch at start (golden passage style)
        /// 
        /// Pseudo code for creating randomizer information
        /// Create an XML file to write to.
        /// foreach level
        ///     foreach door in game (ignoring portal)
        ///         Tell which vanilla room the door is in, and the door index
        ///         Ask if door is ignored.  If so, continue
        ///         Ask for room name to put door into
        ///         if room exists, add.
        ///         Display assumed door type, asking for correction
        ///         Ask for ways to enter doorway
        ///         Ask for ways to exit doorway
        ///     /foreach
        ///     foreach vanilla room
        ///         if room has subrooms
        ///             List subroom names
        ///             foreach Entity in vanilla room
        ///                 Ask if entity is contained within a certain subroom.
        ///                 If so, add entity to room
        ///                 
        ///             Ask for connections between subrooms
        ///             if no, then continue to next room
        ///             if yes, ask for connection information (which two rooms, connection type from 1 to 2, and connection type from 2 to 1)
        ///             foreach subroom
        ///                 Create header for subroom
        ///                 Write all connections
        ///                 Write all entity indexes
        ///                 foreach doorway
        ///                     if doorway ignored, write that and move on
        ///                     otherwise...
        ///                     Write rom index for doorway
        ///                     Write door type
        ///                     Write the enter/exit styles
        ///                 
        ///             /foreach
        ///         else
        ///             foreach Entity in vanilla room
        ///                 Ask if entity should be used.
        ///                 If so, add entity to room
        ///             Create header for room
        ///             foreach doorway
        ///                 if doorway ignored, write that and move on
        ///                 otherwise...
        ///                 Write rom index for doorway
        ///                 Write door type
        ///                 Write the enter/exit styles
        /// 
        /// </summary>
        private static void CreateRandoInformation()
        {
            using (XmlWriter doc = XmlWriter.Create(directory + "\\" + patchPath + ".xml"))
            {
                //This is a dictionary displaying a list of subroom indexes (from the room dictionary) for each given room number
                Dictionary<int, List<string>> vanillaRooms = new Dictionary<int, List<string>>();
                Dictionary<string, RoomInfo> rooms = new Dictionary<string, RoomInfo>();

                doc.WriteStartElement("info");
                for (int passage = 0; passage < 6; passage++)
                {
                    for (int level = 0; level < (passage == 0 || passage == 5 ? 1 : 4); level++)
                    {
                        Console.Write("Starting Level " + passage + " - " + (level + 1) + ".  Do you wish randomize this level? ");
                        if (!GetBoolFromUser(true))
                        {
                            Console.Clear();
                            continue;
                        }

                        rooms.Clear();
                        vanillaRooms.Clear();
                        Console.WriteLine('\n');
                        doc.WriteStartElement("level_" + passage + "_" + level);

                        // Get the start of the level's doorway array

                        int levelID = GetLevelID(passage, level);
                        int doorArrayIndex = GetPointer(PathCreator.DoorTableLocation + levelID * 4);
                        
                        DebugLog(romBuffer[doorArrayIndex]);

                        int currentDoor = 1;
                        while (romBuffer[doorArrayIndex + currentDoor * 12] != 0x00)
                        {
                            new DoorInfo(doorArrayIndex, currentDoor++, ref rooms, ref vanillaRooms);
                            Console.Clear();
                        }

                        Console.WriteLine("Do you want to make any extra connections between rooms?  (format is \"VanillaRoomIndex-SubroomName\")");
                        List<RoomConnection> roomConnections = new List<RoomConnection>();

                        if (GetBoolFromUser(true))
                        {
                            do
                            {
                                Console.Write("Name of the first: ");
                                string first = ReadLine();
                                Console.Write("Name of the second: ");
                                string second = ReadLine();

                                RoomInfo roomOne = null, roomTwo = null;

                                for (int i = 0; i < vanillaRooms.Count; i++)
                                {
                                    if (vanillaRooms[i].Contains(first))
                                    {
                                        roomOne = rooms[first];
                                    }
                                    if (vanillaRooms[i].Contains(second))
                                    {
                                        roomTwo = rooms[second];
                                    }

                                }
                                if (roomOne == null && first.Split('-').Length > 1)
                                {
                                    Console.WriteLine(first + " does not exist.  Do you wish to create it?");
                                    if (GetBoolFromUser(true))
                                    {
                                        int value = int.Parse(first.Split('-')[0]);
                                        vanillaRooms[value].Add(first);
                                        rooms.Add(first, roomOne = new RoomInfo(first));
                                    }
                                }
                                if (roomTwo == null && second.Split('-').Length > 1)
                                {
                                    Console.WriteLine(second + " does not exist.  Do you wish to create it?");
                                    if (GetBoolFromUser(true))
                                    {
                                        int value = int.Parse(second.Split('-')[0]);
                                        vanillaRooms[value].Add(second);
                                        rooms.Add(second, roomTwo = new RoomInfo(second));
                                    }
                                }
                                if (roomOne != null && roomTwo != null)
                                {
                                    Console.WriteLine("Connection types - " + CONNECTION_TYPES_DISPLAY);
                                    roomConnections.Add(new RoomConnection(roomOne, roomTwo, ReadConnectionValues("Connection types from first to second: "), ReadConnectionValues("Connection types from second to first: ")));
                                }
                                else
                                {
                                    Console.WriteLine("Rooms name(s) invalid.");
                                }

                                Console.WriteLine("Do you want to make another?");
                            }
                            while (GetBoolFromUser(true));
                        }

                        //Foreach vanilla room in game
                        foreach (int i in vanillaRooms.Keys)
                        {
                            if (vanillaRooms[i].Count == 1)
                            {
                                ///             Create header for room
                                ///             foreach Entity in vanilla room
                                ///                 Ask if entity should be used.
                                ///                 If so, write entityIndex
                                ///             foreach doorway
                                ///                 if doorway ignored, write that and move on
                                ///                 otherwise...
                                ///                 Write rom index for doorway
                                ///                 Write door type
                                ///                 Write the enter/exit styles

                                Console.Clear();
                                EntityInfo[] entities = PathCreator.GetEntities(passage, level, i);

                                doc.WriteStartElement("room_" + i);

                                for (int eI = 0; eI < entities.Length; eI++)
                                {
                                    WriteEntityToDoc(doc, entities[eI], eI, i);
                                }
                                foreach (DoorInfo door in rooms[vanillaRooms[i][0]].doorList)
                                {
                                    WriteDoorToDoc(doc, door);
                                }
                                doc.WriteEndElement();
                            }
                            else
                            {
                                ///             List subroom names
                                ///             foreach Entity in vanilla room
                                ///                 Ask if entity is contained within a certain subroom.
                                ///                 If so, add entity to room
                                ///                 
                                ///             Ask for connections between subrooms
                                ///             if no, then continue to next room
                                ///             if yes, ask for connection information (which two rooms, connection type from 1 to 2, and connection type from 2 to 1)
                                ///             foreach subroom
                                ///                 Create header for subroom
                                ///                 Write all connections
                                ///                 Write all entity indexes
                                ///                 foreach doorway
                                ///                     if doorway ignored, write that and move on
                                ///                     otherwise...
                                ///                     Write rom index for doorway
                                ///                     Write door type
                                ///                     Write the enter/exit styles
                                ///                 
                                ///             /foreach
                                Console.Clear();

                                Console.WriteLine("Current Room: " + i);
                                Console.WriteLine("Subroom names");
                                foreach (string s in vanillaRooms[i])
                                {
                                    Console.Write("\"{0}\" - ", s);
                                }
                                
                                EntityInfo[] entities = PathCreator.GetEntities(passage, level, i);

                                for (int eI = 0; eI < entities.Length; eI++)
                                {
                                    Console.WriteLine("\nRoom:{2} X:{0} Y:{1} Type:{3}", entities[eI].X, entities[eI].Y, i, entities[eI].originalType);
                                    Console.WriteLine("What subroom do you want this entity to be placed in? (leave blank if ignoring entity)");
                                    string input = ReadLine();
                                    if (input == "")
                                        continue;
                                    else if (vanillaRooms[i].Contains(input))
                                    {
                                        rooms[input].entityList.Add(entities[eI]);
                                    }
                                    else if (vanillaRooms[i].Contains(i + "-" +input))
                                    {
                                        rooms[i + "-" + input].entityList.Add(entities[eI]);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Sorry, but that subroom doesn't exist.  Please try again!");
                                        eI--;
                                    }
                                }
                                
                                foreach (string s in vanillaRooms[i])
                                {
                                    doc.WriteStartElement("room_" + s);
                                    RoomInfo room = rooms[s];
                                    foreach (DoorInfo door in room.doorList)
                                    {
                                        WriteDoorToDoc(doc, door);
                                    }
                                    foreach (EntityInfo entity in room.entityList)
                                    {
                                        WriteEntityToDoc(doc, entity);
                                    }
                                    doc.WriteEndElement();
                                }
                            }
                        }
                        foreach (RoomConnection connection in roomConnections)
                        {
                            WriteRoomConnectionToDoc(doc, connection);
                        }
                        Console.Clear();
                        doc.WriteEndElement();
                    }
                }
                doc.WriteEndElement();

                doc.Flush();
            }
        }

        private static void WriteEntityToDoc(XmlWriter doc, EntityInfo entity, int index, int roomNumber)
        {
            Console.WriteLine("Do you wish this entity to be randomized? Room:{2} X:{0} Y:{1} Type:{3}", entity.X, entity.Y, roomNumber, entity.originalType);
            if (GetBoolFromUser(true))
                doc.WriteElementString("entity", index.ToString());
        }

        private static void WriteEntityToDoc(XmlWriter doc, EntityInfo entity)
        {
            doc.WriteElementString("entity", entity.entityListIndex.ToString());
        }
        private static void WriteDoorToDoc(XmlWriter doc, DoorInfo door)
        {
            doc.WriteStartElement("door");
            if (door.isIgnored)
            {
                doc.WriteElementString("isIgnored", "1");
            }
            else
            {
                doc.WriteElementString("romIndex", door.romArrayIndex.ToString());
                doc.WriteElementString("type", door.doorType.ToString());
                doc.WriteElementString("enterStyle", ((int)door.enterStyle).ToString());
                doc.WriteElementString("exitStyle", ((int)door.exitStyle).ToString());
            }
            doc.WriteEndElement();
        }
        private static void WriteRoomConnectionToDoc(XmlWriter doc, RoomConnection connection)
        {
            doc.WriteStartElement("roomConnection");
            doc.WriteElementString("firstRoom", connection.firstRoom.roomName);
            doc.WriteElementString("secondRoom", connection.secondRoom.roomName);
            doc.WriteElementString("firstToSecond", ((int)connection.firstToSecond).ToString());
            doc.WriteElementString("secondToFirst", ((int)connection.secondToFirst).ToString());
            doc.WriteEndElement();
        }

        private static void MakeCopy(string _originalPath, string _newPath, string _patchPath)
        {
            if (File.Exists(directory + "\\" + _newPath))
                File.Delete(directory + "\\" + _newPath);

            File.Copy(directory + "\\" + _originalPath, directory + "\\" + _newPath);
            Process patchProcess = Process.Start(LunarIPSPath, "-a " + _patchPath + " " + _originalPath + " " + _newPath);
        }

        private static int SetupRNG()
        {
            Console.WriteLine("Type in your seed now.  Leave blank if you wish to have a seed made for you. ");
            string input = ReadLine();
            
            int seed;
            if (!int.TryParse(input, out seed))
            {
                seed = input == "" ? (int)DateTime.Now.Ticks : input.GetHashCode();
            }

            Console.WriteLine("Your seed is " + seed);

            File.WriteAllLines(directory + "//lastSeed.txt", new string[] { seed.ToString() });

            rngGen = new Random(seed);
            return 100;
        }
        private static void RandomizeLevels()
        {
            List<byte> bytes = new List<byte>();
            for (int hallway = 1; hallway < 6; hallway++)
            {
                for (int level = 0; level < 4; level++)
                {
                    if ((hallway == 0 || hallway == 5) && level > 0)
                        break;

                    bytes.Add(romBuffer[PathCreator.LevelHeaderIndexLocation + hallway * 24 + level * 4]);
                }
            }
            for (int hallway = 1; hallway < 6; hallway++)
            {
                for (int level = 0; level < 4; level++)
                {
                    if ((hallway == 0 || hallway == 5) && level > 0)
                        break;

                    int rng = rngGen.Next(bytes.Count);

                    romBuffer[PathCreator.LevelHeaderIndexLocation + hallway * 24 + level * 4] = bytes[rng];
                    bytes.RemoveAt(rng);
                }
            }
        }
        
        public static void OldMain(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { directory + "\\wl4.gba" };
            }

            randomizedRomName = args[0];
            File.Copy(randomizedRomName, directory + "\\backup\\" + randomizedRomName.Substring(randomizedRomName.LastIndexOf('\\') + 1, randomizedRomName.LastIndexOf('.') - randomizedRomName.LastIndexOf('\\') - 1) + ".gba");
            
            string patch = directory + "\\flips\\patch.ips";
            Process patchProcess = Process.Start(LunarIPSPath, patch);
            Console.ReadLine();
            return;

            //Get the options and "Which rooms to randomize" documents
            string optionsPath = directory + "\\options.txt";
            string roomInfoPath = directory + "\\rooms.txt";
            string roomRandoInfoPath = directory + "\\path.txt";

            if (!File.Exists(optionsPath)) // Recreate options page if gone
            {
                File.Create(optionsPath).Close();
                options = new string[] { directory, "True" };
            }
            if (!File.Exists(roomInfoPath)) // If not able to find room info, return.  Unable to recreate room page.  Too much data to put into text
            {
                return;
            }

            // If rom isn't selected, quit
            if (args.Length == 0 || !File.Exists(args[0]))
                return;

            // Get old rom Path
            string oldPath = args[0];
            // Read rom
            romBuffer = File.ReadAllBytes(args[0]);
            
            // Get RNG

            // Set new rom path
            string newPath = args[0];
            newPath = newPath.Remove(newPath.LastIndexOf('\\') + 1) + "WL4-Randomizer_" + "Test" + ".gba";
            
            PathCreatorOld[] levels = new PathCreatorOld[18];
            int levelIndex = 0;
            
            // Read path file, to get room randomizer info
            string[] rooms = File.ReadAllLines(roomRandoInfoPath);

            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] == "") // Ignore all empty lines
                    continue;

                if (rooms[i][0] == 'L')
                {
                    if (rooms[i + 1] == "")
                    {
                        levelIndex++;
                        continue;
                    }
                    //levels[levelIndex] = new PathCreatorOld(ref romBuffer, byte.Parse(rooms[i][1].ToString()), byte.Parse(rooms[i][2].ToString()), ref rooms, i + 1);
                    levelIndex++;
                }
            }

            rooms = File.ReadAllLines(roomInfoPath);

            string[] substring;
            List<RoomNode> indicies1;
            List<int[]> frogIndexes, chestIndexes;
            int romIndex;

            levelIndex = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].Length == 0)
                    continue;
                if (rooms[i][0] == 'M')
                {
                    if (rooms[i + 1] == "") // If level isn't complete, give notes to help complete entity randomizer
                    {
                        for (int j = 0; j < rooms[i].Length / TXT_ROOM_LENGTH; j++) // Get all rooms
                        {
                            romIndex = int.Parse(rooms[i].Substring(j * TXT_ROOM_LENGTH + 8, 2));
                            
                            int offset = Convert.ToInt32("0x" + rooms[i].Substring(j * TXT_ROOM_LENGTH + 1, 6), 16);
                            int start = offset;
                            offset += 2;

                            while (romBuffer[offset] != 0xFF)
                            {
                                Console.Write(start.ToString("X6") + " - ");
                                WriteEntity(offset);

                                offset += 3;
                            }
                            Console.WriteLine();
                        }

                        Console.ReadKey();
                        return;
                    }

                    indicies1 = new List<RoomNode>();
                    for (int j = 0; j < rooms[i].Length / TXT_ROOM_LENGTH; j++) // Get all rooms
                    {
                        romIndex = int.Parse(rooms[i].Substring(j * TXT_ROOM_LENGTH + 8, 2));

                        indicies1.Add(levels[levelIndex].rooms[romIndex]);
                        levels[levelIndex].rooms[romIndex].EntityListIndex = Convert.ToInt32("0x" + rooms[i].Substring(j * TXT_ROOM_LENGTH + 1, 6), 16);
                    }

                    frogIndexes = new List<int[]>();
                    chestIndexes = new List<int[]>();

                    // Get frog locations
                    i++;
                    substring = rooms[i].Split(',');

                    FillLocationsList(substring, levels[levelIndex], out frogIndexes);

                    // Get chest locations
                    i++;
                    substring = rooms[i].Split(',');

                    FillLocationsList(substring, levels[levelIndex], out chestIndexes);

                    //CDLess.ChangeNormal(indicies1.ToArray(), frogIndexes.ToArray(), chestIndexes.ToArray(), ref romBuffer, rngGen, levels[levelIndex]);

                    levelIndex++;
                }
            }

            // Shuffle Levels

            Console.ReadKey();

            File.WriteAllBytes(newPath, romBuffer);
        }

        private static int[][] offsets = new int[][] { new int[] { 1, 2, 3, 4 }, new int[] { 5, 6, 7, 8 }, new int[] { 9, 10, 11, 12 }, new int[] { 13, 14, 16, 15 }, };

        /// <summary>
        /// Get All Entity
        /// </summary>
        /// <param name="substring"></param>
        /// <param name="level"></param>
        /// <param name="temp"></param>
        public static void FillLocationsList(string[] substring, PathCreatorOld level, out List<int[]> temp)
        {
            temp = new List<int[]>();
            int romIndex, count;

            // Foreach room subsection
            for (int j = 0; j < substring.Length; j++)
            {
                //Format for picking individual items (useful for preventing items from spawning in the wrong location, and keeping items located in the right subroom)
                if (substring[j].Contains("-"))
                {
                    bool foundEntity = false;
                    int roomIndex = int.Parse(substring[j].Split('-')[0]); // Get the room index
                    romIndex = level.rooms[roomIndex].EntityListIndex + 2; // Entity list index

                    count = 0;
                    while (romBuffer[romIndex] != 0xFF)
                    {
                        if (romBuffer[romIndex] < 0x10)
                        {
                            if (!foundEntity && count++ == int.Parse(substring[j].Split('-')[1]))
                            {
                                foundEntity = true;
                                temp.Add(new int[] { romIndex, roomIndex, int.Parse(substring[j].Split('-')[2]) });
                            }
                        }
                        romIndex += 3;
                    }
                }
                else
                {
                    int levelIndex = int.Parse(substring[j]); // Get the room index
                    romIndex = level.rooms[int.Parse(substring[j])].EntityListIndex + 2; // Entity list index

                    while (romBuffer[romIndex] != 0xFF) // foreach entity in the room
                    {
                        if (romBuffer[romIndex] < 0x10) // Add entity if it's an essential item (chest, keyzer, etc.)
                        {
                            temp.Add(new int[] { romIndex, levelIndex, 0 }); // Add each entity to list, assuming all are contained in one sub room
                        }

                        romIndex += 3; // NEEEEEEEEEEXT!!
                    }
                }
            }
        }

        private static void WriteEntity(int offset)
        {
            Console.WriteLine("X: " + romBuffer[offset-2].ToString("D2") + ", Y: " + romBuffer[offset - 1].ToString("D2") + ", Type: " + romBuffer[offset] + (romBuffer[offset] < 0x10 ? " -- ":""));
        }

        public static int LevelOffset(int hall, int level)
        {
            if (hall == 0)
            {
                return 0;
            }
            else if (hall == 5)
            {
                return 23;
            }
            else
            {
                return offsets[hall][level];
            }
        }

        // General:
        // 1-6 chests (gem, cd, heart)
        // 7 Gem
        // 8 Frog
        // 9 Keyzer
        // 0x11 Portal

        //

        public static int RandomizeDoorDifference()
        {
            return 1;
        }
        public static int GetRandomInt(int max, int min = 0)
        {
            return rngGen.Next(min, max);
        }
        public static int GetPointer(int offset)
        {
            int retVal = romBuffer[offset] + romBuffer[offset + 1] * 0x100 + romBuffer[offset + 2] * 0x10000;
            //Console.WriteLine(retVal.ToString("X"));
            return retVal;
        }
        public static int GetLevelHeaderIndex(int _passage, int _level)
        {
            int val = _passage * 6 + _level;
            return romBuffer[PathCreator.LevelHeaderIndexLocation + val * 4];
        }
        public static int GetLevelID(int _passage, int _level)
        {
            int header = GetLevelHeaderIndex(_passage, _level);
            header = romBuffer[PathCreator.LevelHeadersLocation + header * 12];
            
            return header;
        }
        public static ConnectionTypes ReadConnectionValues(string display = "")
        {
            Console.Write(display);
            ConnectionTypes retVal = ConnectionTypes.None;

            string input = Program.ReadLine();
            input.Replace(" ", "");
            input.Replace("\t", "");
            if (input == "")
            {
                return ConnectionTypes.Default;
            }
            string[] inputs = input.Split(',');

            foreach (string s in inputs)
            {
                retVal = (ConnectionTypes)((int)retVal + int.Parse(s));
            }

            return retVal;
        }
        public static ConnectionTypes ReadConnectionValuesFromFile(string display = "")
        {
            ConnectionTypes retVal = ConnectionTypes.None;

            string input = Program.ReadLine();
            input.Replace(" ", "");
            input.Replace("\t", "");
            if (input == "")
            {
                return ConnectionTypes.Default;
            }
            string[] inputs = input.Split(',');

            foreach (string s in inputs)
            {
                retVal = (ConnectionTypes)((int)retVal + int.Parse(s));
            }

            return retVal;
        }
    }
}
