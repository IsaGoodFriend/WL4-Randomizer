using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WL4_Randomizer
{
    class RoomConnection
    {
        public RoomInfo firstRoom { get; private set;}
        public RoomInfo secondRoom { get; private set; }

        public ConnectionTypes firstToSecond { get; private set; }
        public ConnectionTypes secondToFirst { get; private set; }

        public RoomConnection(RoomInfo _first, RoomInfo _second, ConnectionTypes _firstToSecond, ConnectionTypes _secondToFirst)
        {
            firstRoom = _first;
            secondRoom = _second;
            firstToSecond = _firstToSecond;
            secondToFirst = _secondToFirst;
        }

        public bool CanPassThrough(RoomInfo room, ConnectionTypes type)
        {
            if (room != firstRoom && room != secondRoom) return false;

            return room == firstRoom ? ((firstToSecond & type) != ConnectionTypes.None) : ((secondToFirst & type) != ConnectionTypes.None);
        }

        public RoomInfo GetOtherRoom(RoomInfo room)
        {
            return room == firstRoom ? secondRoom : firstRoom;
        }
    }
}
