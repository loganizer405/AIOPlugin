using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace AIOPlugin
{
    public class Player
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public int IdleTime { get; set; }
        public int LastX { get; set; }
        public int LastY { get; set; }
        public bool StaffChat { get; set; }
        public Player(int index)
        {
            Index = index;
            IdleTime = 0;
            LastX = TShock.Players[Index].TileX;
            LastY = TShock.Players[Index].TileY;
            StaffChat = false;
        }
    }
    public class Grief
    {
        public int X;
        public int Y;
        public string Name;
        public DateTime Date;
        public Grief(int x, int y, string name, DateTime date)
        {
            X = x;
            Y = y;
            Name = name;
            Date = date;
        }
    }
    public class Suggestion
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public Suggestion(string name, string suggestion)
        {
            Message = suggestion;
            Name = name;
        }
    }
}
