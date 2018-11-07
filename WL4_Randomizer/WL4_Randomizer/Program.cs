//#define TEMP

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WL4_Randomizer
{
    enum Difficulty
    {
        MinionJump = 1,
        HeavyMinionJump = 2,
        Zips = 4,

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
        static Random rngGen;

        static string[] options;

        static int[] testLocations = new int[] { 0x5991D2 };

        static byte[] buffer;

        public static bool useZips = false;

        private static int TXT_ROOM_LENGTH = 10;

        public static void Main(string[] args)
        {
            args = new string[] { Directory.GetCurrentDirectory() + "\\WarioLand4Original.gba" };

            //Get the options and "Which rooms to randomize" documents
            string optionsPath = Directory.GetCurrentDirectory() + "\\options.txt";
            string roomInfoPath = Directory.GetCurrentDirectory() + "\\rooms.txt";

            if (!File.Exists(optionsPath)) // Recreate options page if gone
            {
                File.Create(optionsPath).Close();
                options = new string[] { Directory.GetCurrentDirectory(), "True" };
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
            buffer = File.ReadAllBytes(args[0]);

            //DisplayDoorwayData(4, 3);
            
            // Get RNG
            Console.WriteLine("Type in your seed now.  Leave blank if you wish to have a seed made for you. ");
            string s = Console.ReadLine();
            s = "";
            int seed;
            if (!int.TryParse(s, out seed))
                seed = s == "" ? (int)DateTime.Now.Ticks : s.GetHashCode();

            rngGen = new Random(seed);

            // Set new rom path
            string newPath = args[0];
            newPath = newPath.Remove(newPath.LastIndexOf('\\') + 1) + "WL4-Randomizer_" + "Test" + ".gba";
            
            // Prevent objects from spawning here
            buffer[0x5F152A] = 0x15;
            buffer[0x5F152D] = 0x15;

            PathCreator[] levels = new PathCreator[18];
            int levelIndex = 0;
            
            // Randomize room locations
            string[] rooms = File.ReadAllLines(Directory.GetCurrentDirectory() + "\\path.txt");

            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] == "")
                    continue;

                if (rooms[i][0] == 'L')
                {
                    if (rooms[i + 1] == "")
                    {
                        levelIndex++;
                        continue;
                    }
                    levels[levelIndex] = new PathCreator(ref buffer, byte.Parse(rooms[i][1].ToString()), byte.Parse(rooms[i][2].ToString()), ref rooms, i + 1);
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

                    CDLess.ChangeNormal(indicies1.ToArray(), frogIndexes.ToArray(), chestIndexes.ToArray(), ref buffer, rngGen, levels[levelIndex]);

                    levelIndex++;
                }
            }

            //Reverting entities to normal state when needed
            buffer[0x5F152A] = buffer[0x5F21D4];
            buffer[0x5F152D] = buffer[0x5F21DA];

            // Shuffle Levels
            List<byte> bytes = new List<byte>();
            for (int hallway = 1; hallway < 5; hallway++)
            {
                for (int level = 0; level < 4; level++)
                {
                    if ((hallway == 0 || hallway == 5) && level > 0)
                        break;

                    bytes.Add(buffer[PathCreator.LevelIndexLocation + hallway * 24 + level * 4]);
                }
            }
            for (int hallway = 1; hallway < 5; hallway++)
            {
                for (int level = 0; level < 4; level++)
                {
                    if ((hallway == 0 || hallway == 5) && level > 0)
                        break;

                    int rng = rngGen.Next(bytes.Count);

                    //buffer[PathCreator.LevelIndexLocation + hallway * 24 + level * 4] = bytes[rng];
                    bytes.RemoveAt(rng);
                }
            }

            File.WriteAllBytes(newPath, buffer);
        }

        private static int[][] offsets = new int[][] { new int[] { 1, 2, 3, 4 }, new int[] { 5, 6, 7, 8 }, new int[] { 9, 10, 11, 12 }, new int[] { 13, 14, 16, 15 }, };

        public static void FillLocationsList(string[] substring, PathCreator level, out List<int[]> temp)
        {
            temp = new List<int[]>();
            int romIndex, count;

            // Foreach subsection
            for (int j = 0; j < substring.Length; j++)
            {
                //Format for picking individual items (useful for preventing items from spawning in the wrong location, and keeping items located in the right subroom)
                if (substring[j].Contains("-"))
                {
                    int roomIndex = int.Parse(substring[j].Split('-')[0]); // Get the room index
                    romIndex = level.rooms[roomIndex].EntityListIndex + 2; // Entity list index

                    count = 0;
                    while (buffer[romIndex] != 0xFF)
                    {
                        if (buffer[romIndex] < 0x10 && count++ == int.Parse(substring[j].Split('-')[1]))
                        {
                            temp.Add(new int[] { romIndex, roomIndex, int.Parse(substring[j].Split('-')[2]) } );
                            break;
                        }
                        romIndex += 3;
                    }
                }
                else
                {
                    int levelIndex = int.Parse(substring[j]); // Get the room index
                    romIndex = level.rooms[int.Parse(substring[j])].EntityListIndex + 2; // Entity list index

                    while (buffer[romIndex] != 0xFF)
                    {
                        if (buffer[romIndex] < 0x10)
                        {
                            temp.Add(new int[] { romIndex, levelIndex, 0 }); // Add each entity to list, assuming all are contained in one sub room
                        }
                        romIndex += 3;
                    }
                }
            }
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

        public static int GetPointer(int offset)
        {
            return buffer[offset] + buffer[offset + 1] * 256 + buffer[offset + 2] * 65536;
        }
    }
    class CDLess
    {
        public static void ChangeNormal(RoomNode[] rooms, int[][] frog, int[][] chests, ref byte[] rom, Random rng, PathCreator level, bool hasCD = true)
        {

            int buffer = 0;
            int index = 0;

            for (int i = 0; i < rooms.Length; i++) // Clear levels
            {
                index = rooms[i].EntityListIndex + 2;

                while (rom[index] != 0xFF) // Check every entity in list
                {
                    if (rom[index] < 0xF && (rom[index] != 0x08 || frog.Length > 0)) // If swappable entity, turn into gem, and skip frog switch if keeping in same place
                    {
                        rom[index] = 0x07;
                    }
                    index += 3;
                }
            }
            
            if (frog.Length != 0)
            {
                index = rng.Next(0, frog.Length);

                rom[frog[index][0]] = 0x08;
                level.rooms[frog[index][1]].subrooms[frog[index][2]].itemsContained |= ItemFound.Frog;
            }

            List<int> possiblities = new List<int>();
            for (int i = 0; i < chests.Length; i++)
            {
                possiblities.Add(i);
            }

            for (int i = 0; i < 6; i++) // Chests
            {
                if (possiblities.Count == 0 && i >= 4)
                    break;
                if (!hasCD && i == 4)
                    continue;
                
                while (true)
                {
                    buffer = rng.Next(possiblities.Count);

                    index = possiblities[buffer];

                    if (rom[chests[index][0]] != 0x07)
                        continue;

                    rom[chests[index][0]] = (byte)(i + 1);

                    possiblities.RemoveAt(buffer);
                    break;
                }
            }

            List<int> potentials = new List<int>();

            bool loopOn = true;
            while (loopOn)
            {
                index = rooms[rng.Next(0, rooms.Length)].EntityListIndex + 2;

                while (rom[index] != 0xFF)
                {
                    if (rom[index] == 0x07)
                    {
                        loopOn = false;
                        potentials.Add(index);
                    }
                    index += 3;
                }

                if (!loopOn)
                {
                    rom[potentials[rng.Next(potentials.Count)]] = 0x09;
                }
            }

        }

        public static void Randomize(ref byte[] rom, int index, Random rng)
        {
            List<byte> list = new List<byte>();

            for (int i = index + 2; rom[i] != 0xFF; i += 3)
            {
                list.Add(rom[i]);
            }
            for (int i = index + 2; rom[i] != 0xFF; i += 3)
            {
                index = rng.Next(list.Count);
                rom[i] = list[index];
                list.RemoveAt(index);
            }
        }
    }
}
