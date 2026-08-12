using System;
using System.Collections.Generic;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.AlgoritmiPretrage
{
    // koristi se i za slanje vozilu i za evidenciju/poređenje algoritama (ostala polja)
    [Serializable]
    public class RezultatPretrage
    {
        public string Algoritam { get; set; }
        public List<Koordinata> Putanja { get; set; } = new List<Koordinata>();
        public int PosecenihCvorova { get; set; }
        public double VremeMs { get; set; }
        public bool Pronadjeno { get; set; }

        public int DuzinaPuta
        {
            get { return Putanja != null && Putanja.Count > 0 ? Putanja.Count - 1 : -1; }
        }
    }
}