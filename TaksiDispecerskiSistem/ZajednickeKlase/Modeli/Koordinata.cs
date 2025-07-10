using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZajednickeKlase.Modeli
{
    [Serializable]
    public class Koordinata
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Koordinata() { }

        public Koordinata(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
