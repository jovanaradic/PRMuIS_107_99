using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZajednickeKlase.Enumeracije;

namespace ZajednickeKlase.Modeli
{
    [Serializable]

    public class ZadatakModel
    {
        public int ID { get; set; }
        public int IDKlijenta { get; set; }
        public int IDVozila { get; set; }

        public Koordinata pozicijaKlijenta { get; set; }

        public Koordinata zeljenaPozicija {  get; set; }

        public StatusZadatka StatusZadatka { get; set; } = StatusZadatka.Aktivan;

        public double PredjenaRazdaljina { get; set; }
    }
}
