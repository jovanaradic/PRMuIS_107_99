using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.AlgoritmiPretrage
{
    public static class BFS
    {
        public static RezultatPretrage Pretrazi(Lavirint.Lavirint lab, Koordinata start, Koordinata cilj)
        {
            var sw = Stopwatch.StartNew();
            var posecen = new HashSet<string>();
            var prethodnik = new Dictionary<string, Koordinata>();
            var red = new Queue<Koordinata>();

            posecen.Add(Kljuc(start));
            red.Enqueue(start);
            int posecenihCvorova = 1;
            bool pronadjeno = false;

            while (red.Count > 0)
            {
                var trenutna = red.Dequeue();
                if (trenutna.X == cilj.X && trenutna.Y == cilj.Y)
                {
                    pronadjeno = true;
                    break;
                }

                foreach (var sused in lab.Susedi(trenutna.X, trenutna.Y))
                {
                    string kljuc = Kljuc(sused);
                    if (!posecen.Contains(kljuc))
                    {
                        posecen.Add(kljuc);
                        prethodnik[kljuc] = trenutna;
                        red.Enqueue(sused);
                        posecenihCvorova++;
                    }
                }
            }
            sw.Stop();

            var putanja = pronadjeno ? Rekonstruisi(prethodnik, start, cilj) : new List<Koordinata>();
            return new RezultatPretrage
            {
                Algoritam = "BFS",
                Putanja = putanja,
                PosecenihCvorova = posecenihCvorova,
                VremeMs = sw.Elapsed.TotalMilliseconds,
                Pronadjeno = pronadjeno
            };
        }

        private static List<Koordinata> Rekonstruisi(Dictionary<string, Koordinata> prethodnik, Koordinata start, Koordinata cilj)
        {
            var putanja = new List<Koordinata>();
            var trenutna = cilj;
            putanja.Add(trenutna);

            while (trenutna.X != start.X || trenutna.Y != start.Y)
            {
                trenutna = prethodnik[Kljuc(trenutna)];
                putanja.Add(trenutna);
            }

            putanja.Reverse();
            return putanja;
        }

        private static string Kljuc(Koordinata k)
        {
            return k.X.ToString() + "_" + k.Y.ToString();
        }
    }
}