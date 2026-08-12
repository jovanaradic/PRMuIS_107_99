using System;
using System.Collections.Generic;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.Lavirint
{
    public static class GeneratorLavirinta
    {
        public static Lavirint Generisi(int sirina, int visina, double verovatnocaDodatnihProlaza = 0.0, int? seme = null)
        {
            var lav = new Lavirint(sirina, visina);
            Random rnd = seme.HasValue ? new Random(seme.Value) : new Random();

            // na početku su svi zidovi podignuti
            int sirinaMreze = lav.Zidovi.GetLength(0);
            int visinaMreze = lav.Zidovi.GetLength(1);
            for (int x = 0; x < sirinaMreze; x++)
                for (int y = 0; y < visinaMreze; y++)
                    lav.Zidovi[x, y] = true;

            // same ćelije (neparne koordinate u mreži) su uvek prohodne
            for (int cx = 0; cx < sirina; cx++)
                for (int cy = 0; cy < visina; cy++)
                    lav.Zidovi[2 * cx + 1, 2 * cy + 1] = false;

            bool[,] posecena = new bool[sirina, visina];
            var stek = new Stack<Koordinata>();

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            var start = new Koordinata(0, 0);
            posecena[0, 0] = true;
            stek.Push(start);

            //recursive backtracker
            while (stek.Count > 0)
            {
                var trenutna = stek.Peek();
                var kandidati = new List<int>();

                for (int i = 0; i < 4; i++)
                {
                    int nx = trenutna.X + dx[i];
                    int ny = trenutna.Y + dy[i];
                    if (nx >= 0 && nx < sirina && ny >= 0 && ny < visina && !posecena[nx, ny])
                        kandidati.Add(i);
                }

                if (kandidati.Count == 0)
                {
                    stek.Pop();
                    continue;
                }

                int izbor = kandidati[rnd.Next(kandidati.Count)];
                int noviX = trenutna.X + dx[izbor];
                int noviY = trenutna.Y + dy[izbor];

                // rušimo zid između trenutne i novoizabrane ćelije
                int zidX = 2 * trenutna.X + 1 + dx[izbor];
                int zidY = 2 * trenutna.Y + 1 + dy[izbor];
                lav.Zidovi[zidX, zidY] = false;

                posecena[noviX, noviY] = true;
                stek.Push(new Koordinata(noviX, noviY));
            }

            //nasumično rušimo mali procenat preostalih unutrašnjih zidova čime dobijamo alternativne rute - realističnije za poređenje algoritama pretrage.
            if (verovatnocaDodatnihProlaza > 0)
            {
                for (int x = 1; x < sirina; x++)
                {
                    for (int y = 0; y < visina; y++)
                    {
                        int zidX = 2 * x;
                        int zidY = 2 * y + 1;
                        if (lav.Zidovi[zidX, zidY] && rnd.NextDouble() < verovatnocaDodatnihProlaza)
                            lav.Zidovi[zidX, zidY] = false;
                    }
                }
                for (int x = 0; x < sirina; x++)
                {
                    for (int y = 1; y < visina; y++)
                    {
                        int zidX = 2 * x + 1;
                        int zidY = 2 * y;
                        if (lav.Zidovi[zidX, zidY] && rnd.NextDouble() < verovatnocaDodatnihProlaza)
                            lav.Zidovi[zidX, zidY] = false;
                    }
                }
            }

            return lav;
        }
    }
}