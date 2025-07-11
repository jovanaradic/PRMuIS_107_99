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
        public int IDKlijenta { get; set; }
        public int IDVozila { get; set; }

        public StatusZadatka StatusVozila { get; set; } = StatusZadatka.Aktivan;

        public double PredjenaRazdaljina { get; set; }

        public ZadatakModel(int iDKlijenta, int iDVozila, StatusZadatka statusVozila, double predjenaRazdaljina)
        {
            IDKlijenta = iDKlijenta;
            IDVozila = iDVozila;
            StatusVozila = statusVozila;
            PredjenaRazdaljina = predjenaRazdaljina;
        }

        public ZadatakModel() { }
    }
}
