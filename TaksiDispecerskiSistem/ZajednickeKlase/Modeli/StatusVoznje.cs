using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZajednickeKlase.Modeli
{
    [Serializable]
    public class StatusVoznje
    {

        public int IdKlijenta { get; set; }
        public int IdVozila { get; set; }
        public double Km { get; set; }
        public double CenaVoznje { get; set; }

        public StatusVoznje() { }
    }
}
