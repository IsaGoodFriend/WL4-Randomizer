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

        public static void Main(string[] args)
        {
            //Get the options and "Which rooms to randomize" documents
            string optionsPath = Directory.GetCurrentDirectory() + "\\options.txt";
            string rngTest = Directory.GetCurrentDirectory() + "\\rooms.txt";

            if (!File.Exists(optionsPath)) // Recreate options page if gone
            {
                File.Create(optionsPath).Close();
                options = new string[] { Directory.GetCurrentDirectory(), "True" };
            }
            if (!File.Exists(rngTest)) // Unable to recreate room page.  Too much data to put into text
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
            
            //DisplayDoorwayData();
            
            // Get RNG
            Console.WriteLine("Type in your seed now.  Leave blank if you wish to have a seed made for you. ");
            string s = Console.ReadLine();
            int seed;
            if (!int.TryParse(s, out seed))
                seed = s == "" ? (int)DateTime.Now.Ticks : s.GetHashCode();

            rngGen = new Random(seed);

            // Set new rom path
            string newPath = args[0];
            newPath = newPath.Remove(newPath.LastIndexOf('\\') + 1) + "WL4-Randomizer_" + seed + ".gba";

            // Get Room RNG template
            string[] rooms = File.ReadAllLines(rngTest);

            // Room buffers
            List<int> indicies1, indicies2, indicies3;
            int singleIndex;

            // Prevent objects from spawning here
            buffer[0x59B17E] = 0x15;
            buffer[0x5F152A] = 0x15;
            buffer[0x5F152D] = 0x15;
            buffer[0x5F08BE] = 0x15;

            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].Length == 0)
                    continue;
                if (rooms[i][0] == 'M')
                {
                    indicies1 = new List<int>();
                    for (int j = 0; j < rooms[i].Length / 9; j++) // Get all rooms
                    {
                        s = rooms[i].Substring(j * 9 + 1, 8);
                        indicies1.Add(Convert.ToInt32(s, 16));
                    }
                    i++;
                    indicies2 = new List<int>();
                    for (int j = 0; j < (rooms[i].Length + 1) / 9; j++) // Get frog rooms
                    {
                        s = rooms[i].Substring(j * 9, 8);
                        indicies2.Add(Convert.ToInt32(s, 16));
                    }
                    i++;
                    indicies3 = new List<int>();
                    for (int j = 0; j < (rooms[i].Length + 1) / 9; j++) // Get Chest rooms
                    {
                        s = rooms[i].Substring(j * 9, 8);
                        indicies3.Add(Convert.ToInt32(s, 16));
                    }
                    CDLess.ChangeNormal(indicies1.ToArray(), indicies2.ToArray(), indicies3.ToArray(), ref buffer, rngGen);
                }
                else if (rooms[i][0] == 'R')
                {
                    singleIndex = Convert.ToInt32(rooms[i].Substring(1, 8), 16);
                    CDLess.Randomize(ref buffer, singleIndex, rngGen);
                }
            }

            //Reverting entities to normal state when needed
            buffer[0x59B17E] = 0x07;
            buffer[0x5F152A] = buffer[0x5F21D4];
            buffer[0x5F152D] = buffer[0x5F21DA];
            buffer[0x5F08BE] = 0x07;

            // Randomize room locations
            rooms = File.ReadAllLines(Directory.GetCurrentDirectory() + "\\path.txt");

            PathCreator test;

            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] == "")
                    continue;

                if (rooms[i][0] == 'L')
                {
                    test = new PathCreator(ref buffer, byte.Parse(rooms[i][1].ToString()), byte.Parse(rooms[i][2].ToString()), ref rooms, i + 1);
                    test.CreatePath(rngGen, ref buffer);
                }
            }
            
            File.WriteAllBytes(newPath, buffer);
        }

        private static void DisplayRoomData()
        {
            int index = 0x3F2F88;
            int count = 0;
            int levelCount = 0;
            byte maxRoom = 0;

            List<string> fileTest = new List<string>();
            string str = "";

            fileTest.Add("LTF	RID	X1	X2	Y1	Y2	LDs	XDp	YDp	ETb	BG1	BG2");

            while (buffer[index] <= 0x06)
            {
                if (buffer[index] >= 0x00)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        str += buffer[index + i].ToString("D3") + " ";
                    }

                    if (buffer[index] > 0x00)
                    {
                        str = str.Remove(str.Length - 1);
                        count++;
                        maxRoom = Math.Max(buffer[index + 1], maxRoom);
                    }
                    else
                    {
                        str += count + " " + maxRoom;
                        count = 0;
                        maxRoom = 0;
                        levelCount++;
                    }
                    fileTest.Add(str);
                    str = "";
                }
                index += 12;
            }
            fileTest.Add(levelCount.ToString());

            File.WriteAllLines(Directory.GetCurrentDirectory() + "\\test.txt", fileTest.ToArray());

            Console.ReadKey();

            return;
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
        public static void ChangeNormal(int[] rooms, int[] frog, int[] chests, ref byte[] rom, Random rng, bool hasCD = true)
        {
            List<int> possiblities = new List<int>();
            for (int i = 0; i < chests.Length; i++)
            {
                possiblities.Add(i);
            } 

            int buffer = 0;
            int index = 0;

            for (int i = 0; i < rooms.Length; i++) // Clear levels
            {
                index = rooms[i] + 2;

                while (rom[index] != 0xFF) // Check every entity in list
                {
                    if (rom[index] < 0xF && (rom[index] != 0x08 || frog.Length > 0)) // If swappable entity, turn into gem, and skip frog switch if keeping in same place
                    {
                        rom[index] = 0x07;
                    }
                    index += 3;
                }
            }

            bool loopOn = true;
            while (loopOn)
            {
                if (frog.Length == 0)
                    break;

                index = rng.Next(0, frog.Length);

                index = frog[index] + 2;
                while (rom[index] != 0xFF)
                {
                    if (rom[index] == 0x07)
                    {
                        loopOn = false;
                        rom[index] = 0x08;
                        break;
                    }
                    index += 3;
                }
            }

            for (int i = 0; i < 6; i++) // Chests
            {
                if (possiblities.Count == 0 && i >= 4)
                    break;
                if (!hasCD && i == 4)
                    continue;

                loopOn = true;
                buffer = rng.Next(possiblities.Count);

                index = possiblities[buffer];
                index = chests[index] + 2;

                while (rom[index] != 0xFF)
                {
                    if (rom[index] == 0x07 && !(rom[index - 2] == 0x8 && rom[index-1] == 0x34 && index == 0x5CEDE3))
                    {
                        rom[index] = (byte)(i + 1);
                        loopOn = false;
                        break;
                    }
                    index += 3;

                }

                if (loopOn)
                    i--;
                else
                    possiblities.RemoveAt(buffer);
            }

            List<int> potentials = new List<int>();

            loopOn = true;
            while (loopOn)
            {
                index = rooms[rng.Next(0, rooms.Length)] + 2;
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
