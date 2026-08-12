using System;
using System.Collections.Generic;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.AlgoritmiPretrage
{
    //Pokreće sva tri algoritma pretrage (BFS, Dijkstra, A*) nad istim lavirintom
    public static class PronalazacPuta
    {
        public static RezultatPretrage NadjiOptimalnuPutanju(Lavirint.Lavirint lab, Koordinata start, Koordinata cilj, out List<RezultatPretrage> sviRezultati)
        {
            var rezultatBfs = BFS.Pretrazi(lab, start, cilj);
            var rezultatDijkstra = Dijkstra.Pretrazi(lab, start, cilj);
            var rezultatAStar = AStar.Pretrazi(lab, start, cilj);

            sviRezultati = new List<RezultatPretrage> { rezultatBfs, rezultatDijkstra, rezultatAStar };

            RezultatPretrage najbolji = null;
            foreach (var rezultat in sviRezultati)
            {
                if (!rezultat.Pronadjeno)
                    continue;

                // biramo najkraću putanju, a kod izjednačenja - onu koja je najbrže pronađena
                if (najbolji == null
                    || rezultat.DuzinaPuta < najbolji.DuzinaPuta
                    || (rezultat.DuzinaPuta == najbolji.DuzinaPuta && rezultat.VremeMs < najbolji.VremeMs))
                {
                    najbolji = rezultat;
                }
            }

            return najbolji;
        }
    }
}