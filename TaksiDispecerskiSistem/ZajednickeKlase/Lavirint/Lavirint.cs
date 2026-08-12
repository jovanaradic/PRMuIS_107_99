using System;
using System.Collections.Generic;
using ZajednickeKlase.Modeli;

namespace ZajednickeKlase.Lavirint
{
    [Serializable]
    public class Lavirint
    {
        public int Sirina { get; set; }
        public int Visina { get; set; }

        // true = zid
        public bool[,] Zidovi { get; set; }

        public Lavirint() { }

        public Lavirint(int sirina, int visina)
        {
            Sirina = sirina;
            Visina = visina;
            Zidovi = new bool[2 * sirina + 1, 2 * visina + 1];
        }

        // dozvoljeno je isključivo kretanje gore/dole/levo/desno, bez dijagonala
        public bool PostojiProlaz(int x1, int y1, int x2, int y2)
        {
            if (!UGranicama(x1, y1) || !UGranicama(x2, y2))
                return false;

            int dx = x2 - x1;
            int dy = y2 - y1;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return false;

            int zidX = 2 * x1 + 1 + dx;
            int zidY = 2 * y1 + 1 + dy;
            return !Zidovi[zidX, zidY];
        }

        public bool UGranicama(int x, int y)
        {
            return x >= 0 && x < Sirina && y >= 0 && y < Visina;
        }

        // pronalazi sve prohodne susede ćelije (x,y) - koristi se u algoritmima pretrage
        public List<Koordinata> Susedi(int x, int y)
        {
            var rezultat = new List<Koordinata>();
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (PostojiProlaz(x, y, nx, ny))
                    rezultat.Add(new Koordinata(nx, ny));
            }

            return rezultat;
        }
    }
}