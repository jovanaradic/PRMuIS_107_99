using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.AlgoritmiPretrage
{
    public static class Dijkstra
    {
        public static RezultatPretrage Pretrazi(Lavirint.Lavirint lab, Koordinata start, Koordinata cilj)
        {
            var sw = Stopwatch.StartNew();
            var udaljenost = new Dictionary<string, double>();
            var prethodnik = new Dictionary<string, Koordinata>();
            var obradjeni = new HashSet<string>();
            var red = new List<Koordinata>();

            udaljenost[Kljuc(start)] = 0;
            red.Add(start);
            int posecenihCvorova = 0;
            bool pronadjeno = false;

            while (red.Count > 0)
            {
                int indeksMin = NajmanjaUdaljenost(red, udaljenost);
                var trenutna = red[indeksMin];
                red.RemoveAt(indeksMin);

                string kljucTrenutne = Kljuc(trenutna);
                if (obradjeni.Contains(kljucTrenutne))
                    continue;
                obradjeni.Add(kljucTrenutne);
                posecenihCvorova++;

                if (trenutna.X == cilj.X && trenutna.Y == cilj.Y)
                {
                    pronadjeno = true;
                    break;
                }

                foreach (var sused in lab.Susedi(trenutna.X, trenutna.Y))
                {
                    string kljucSuseda = Kljuc(sused);
                    double novaUdaljenost = udaljenost[kljucTrenutne] + 1;
                    double staraUdaljenost;
                    bool postoji = udaljenost.TryGetValue(kljucSuseda, out staraUdaljenost);

                    if (!postoji || novaUdaljenost < staraUdaljenost)
                    {
                        udaljenost[kljucSuseda] = novaUdaljenost;
                        prethodnik[kljucSuseda] = trenutna;
                        red.Add(sused);
                    }
                }
            }
            sw.Stop();

            var putanja = pronadjeno ? Rekonstruisi(prethodnik, start, cilj) : new List<Koordinata>();
            return new RezultatPretrage
            {
                Algoritam = "Dijkstra",
                Putanja = putanja,
                PosecenihCvorova = posecenihCvorova,
                VremeMs = sw.Elapsed.TotalMilliseconds,
                Pronadjeno = pronadjeno
            };
        }

        private static int NajmanjaUdaljenost(List<Koordinata> red, Dictionary<string, double> udaljenost)
        {
            int indeksMin = 0;
            double minVrednost = udaljenost[Kljuc(red[0])];
            for (int i = 1; i < red.Count; i++)
            {
                double vrednost = udaljenost[Kljuc(red[i])];
                if (vrednost < minVrednost)
                {
                    minVrednost = vrednost;
                    indeksMin = i;
                }
            }
            return indeksMin;
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