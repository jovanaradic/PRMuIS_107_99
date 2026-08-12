using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.AlgoritmiPretrage
{
    
    public static class AStar
    {
        public static RezultatPretrage Pretrazi(Lavirint.Lavirint lab, Koordinata start, Koordinata cilj)
        {
            var sw = Stopwatch.StartNew();
            var gVrednost = new Dictionary<string, double>();
            var fVrednost = new Dictionary<string, double>();
            var prethodnik = new Dictionary<string, Koordinata>();
            var obradjeni = new HashSet<string>();
            var otvoreni = new List<Koordinata>();

            gVrednost[Kljuc(start)] = 0;
            fVrednost[Kljuc(start)] = Heuristika(start, cilj);
            otvoreni.Add(start);
            int posecenihCvorova = 0;
            bool pronadjeno = false;

            while (otvoreni.Count > 0)
            {
                int indeksMin = NajmanjaFVrednost(otvoreni, fVrednost);
                var trenutna = otvoreni[indeksMin];
                otvoreni.RemoveAt(indeksMin);

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
                    double novoG = gVrednost[kljucTrenutne] + 1;
                    double staroG;
                    bool postoji = gVrednost.TryGetValue(kljucSuseda, out staroG);

                    if (!postoji || novoG < staroG)
                    {
                        gVrednost[kljucSuseda] = novoG;
                        fVrednost[kljucSuseda] = novoG + Heuristika(sused, cilj);
                        prethodnik[kljucSuseda] = trenutna;
                        otvoreni.Add(sused);
                    }
                }
            }
            sw.Stop();

            var putanja = pronadjeno ? Rekonstruisi(prethodnik, start, cilj) : new List<Koordinata>();
            return new RezultatPretrage
            {
                Algoritam = "A*",
                Putanja = putanja,
                PosecenihCvorova = posecenihCvorova,
                VremeMs = sw.Elapsed.TotalMilliseconds,
                Pronadjeno = pronadjeno
            };
        }

        private static double Heuristika(Koordinata a, Koordinata cilj)
        {
            return Math.Abs(a.X - cilj.X) + Math.Abs(a.Y - cilj.Y);
        }

        private static int NajmanjaFVrednost(List<Koordinata> otvoreni, Dictionary<string, double> fVrednost)
        {
            int indeksMin = 0;
            double minVrednost = fVrednost[Kljuc(otvoreni[0])];
            for (int i = 1; i < otvoreni.Count; i++)
            {
                double vrednost = fVrednost[Kljuc(otvoreni[i])];
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