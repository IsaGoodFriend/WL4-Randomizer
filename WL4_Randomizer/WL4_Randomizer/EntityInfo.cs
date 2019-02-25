using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WL4_Randomizer
{
    enum EntityType : byte
    {
        Gem1 = 1,
        Gem2 = 2,
        Gem3 = 3,
        Gem4 = 4,
        CD = 5,
        BigGem = 7,
        FrogSwitch = 8,
        Keyzer = 9,
    }
    class EntityInfo
    {
        public int X, Y, originalType, entityListIndex;
        public int romIndex { get; private set; }
        public EntityType entityType { get; set; }

        public EntityInfo(int _index, EntityType _type, int _x = 0, int _y = 0, int _entityIndex = 0, int _originalType = 0)
        {
            romIndex = _index;
            entityType = _type;
            X = _x;
            Y = _y;
            entityListIndex = _entityIndex;
            originalType = _originalType;
        }

        public void ChangeRom()
        {
            Program.romBuffer[romIndex + 2] = (byte)entityType;
        }
    }
}
