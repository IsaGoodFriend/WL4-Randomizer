//#define TEMP

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GBA_Decoder
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
            CDLess.ChangeCDLess(new int[] { 0x5991D0, 0x59AC40, 0x5995D4, 0x599B1C, 0x59A5F4, 0x59AD14, 0x59B17C, 0x59B2DC, 0x599FFC },
                new int[] { 0x5991D0, 0x59AC40, 0x5995D4, 0x599B1C, 0x59A5F4, 0x59AD14, 0x59B17C, 0x59B2DC },
                new int[] { 0x5991D0, 0x59AC40, 0x5995D4, 0x599B1C, 0x59A5F4, 0x59B17C, 0x59B2DC }, ref buffer, rngGen);

            //Reverting entities to normal state when needed
            buffer[0x5F152A] = buffer[0x5F21D4];
            buffer[0x5F152D] = buffer[0x5F21DA];
            buffer[0x5F08BE] = 0x7;

            File.WriteAllBytes(newPath, buffer);
        }
        
        // General:
        // 1-6 chests (gem, cd, heart)
        // 7 Gem
        // 8 Frog
        // 9 Keyzer
        // 0x11 Portal

        //
    }
    class CDLess
    {

        public static void ChangeCDLess(int[] rooms, int[] frog, int[] chests, ref byte[] rom, Random rng)
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

                index = frog[rng.Next(0, frog.Length)] + 2;
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
                if (possiblities.Count == 0 && i >= 4 || i == 4)
                    break;

                loopOn = true;
                buffer = rng.Next(possiblities.Count);

                index = possiblities[buffer];
                index = chests[index] + 2;

                while (rom[index] != 0xFF)
                {
                    if (rom[index] == 0x07 &&
                        !(rom[index - 2] == 0x8 && rom[index - 1] == 0x34 && index == 0x5CEDE3))
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
        public static void ChangeNormal(int[] rooms, int[] frog, int[] chests, ref byte[] rom, Random rng)
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

                index = frog[rng.Next(0, frog.Length)] + 2;
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
