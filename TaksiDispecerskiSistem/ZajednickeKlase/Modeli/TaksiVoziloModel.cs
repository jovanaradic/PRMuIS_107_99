using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZajednickeKlase.Enumeracije;

namespace ZajednickeKlase.Modeli
{
    [Serializable]
    public class TaksiVoziloModel
    {
        public int Id { get; set; }
        public int koordinataX { get; set; }
        public int koordinataY { get; set; }
        public StatusVozila Status { get; set; } = StatusVozila.Slobodno;
        public double Km { get; set; }
        public double Zarada { get; set; }

        public int BrojMusterija { get; set; } = 0;

        public TaksiVoziloModel() { }

        public TaksiVoziloModel(int koordinataX, int koordinataY)
        {
            this.koordinataX = koordinataX;
            this.koordinataY = koordinataY;
        }
    }
}
