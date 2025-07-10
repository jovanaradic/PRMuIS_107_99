using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZajednickeKlase.Enumeracije;

namespace ZajednickeKlase.Modeli
{
    [Serializable]
    public class KlijentModel
    {
        public int IDKlijenta { get; set; }
        public Koordinata pocetnaTacka { get; set; }

        public Koordinata krajnjaTacka { get; set; }

        public StatusZahteva StatusZahteva { get; set; } = StatusZahteva.Cekanje;

        public KlijentModel() { }

        public KlijentModel(Koordinata pocetnaTacka, Koordinata krajnjaTacka)
        {
            this.pocetnaTacka = pocetnaTacka;
            this.krajnjaTacka = krajnjaTacka;
        }
    }
}
