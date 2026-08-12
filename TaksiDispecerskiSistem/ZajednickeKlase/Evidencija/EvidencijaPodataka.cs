using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ZajednickeKlase.AlgoritmiPretrage;

namespace ZajednickeKlase.Evidencija
{
    [Serializable]
    public class EvidencijaVoznje
    {
        public DateTime Vreme { get; set; }
        public int IdZadatka { get; set; }
        public int IdKlijenta { get; set; }
        public int IdVozila { get; set; }
        public string Algoritam { get; set; }
        public double PredjenaRazdaljina { get; set; }
        public double CenaVoznje { get; set; }
    }

    public static class EvidencijaPodataka
    {
        private static readonly string FolderPodataka = "Podaci";
        private static readonly object Katanac = new object();

        private static string PutanjaDo(string imeFajla)
        {
            if (!Directory.Exists(FolderPodataka))
                Directory.CreateDirectory(FolderPodataka);
            return Path.Combine(FolderPodataka, imeFajla);
        }

        public static void SacuvajPoredjenjeAlgoritama(int idZadatka, string oznakaSegmenta, List<RezultatPretrage> rezultati, string izabraniAlgoritam,
            int sirinaLavirinta, int visinaLavirinta, int brojAktivnihVozila, int brojAktivnihZadataka)
        {
            lock (Katanac)
            {
                try
                {
                    string putanjaFajla = PutanjaDo("poredjenje_algoritama.csv");
                    bool postoji = File.Exists(putanjaFajla);

                    using (var pisac = new StreamWriter(putanjaFajla, true, Encoding.UTF8))
                    {
                        if (!postoji)
                            pisac.WriteLine("Vreme;IdZadatka;Segment;Algoritam;DuzinaPuta;PosecenihCvorova;VremeMs;Izabran;SirinaLavirinta;VisinaLavirinta;BrojAktivnihVozila;BrojAktivnihZadataka");

                        foreach (var r in rezultati)
                        {
                            pisac.WriteLine(string.Join(";", new[]
                            {
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                idZadatka.ToString(),
                                Escape(oznakaSegmenta),
                                Escape(r.Algoritam),
                                r.DuzinaPuta.ToString(),
                                r.PosecenihCvorova.ToString(),
                                r.VremeMs.ToString("0.0000", CultureInfo.InvariantCulture),
                                (r.Algoritam == izabraniAlgoritam).ToString(),
                                sirinaLavirinta.ToString(),
                                visinaLavirinta.ToString(),
                                brojAktivnihVozila.ToString(),
                                brojAktivnihZadataka.ToString()
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UPOZORENJE: Neuspesno pisanje u poredjenje_algoritama.csv: " + ex.Message);
                }
            }
        }
        public static void SacuvajZavrsenuVoznju(EvidencijaVoznje v)
        {
            lock (Katanac)
            {
                try
                {
                    string putanjaFajla = PutanjaDo("voznje.csv");
                    bool postoji = File.Exists(putanjaFajla);

                    using (var pisac = new StreamWriter(putanjaFajla, true, Encoding.UTF8))
                    {
                        if (!postoji)
                            pisac.WriteLine("Vreme;IdZadatka;IdKlijenta;IdVozila;Algoritam;PredjenaRazdaljina;CenaVoznje");

                        pisac.WriteLine(string.Join(";", new[]
                        {
                            v.Vreme.ToString("yyyy-MM-dd HH:mm:ss"),
                            v.IdZadatka.ToString(),
                            v.IdKlijenta.ToString(),
                            v.IdVozila.ToString(),
                            Escape(v.Algoritam),
                            v.PredjenaRazdaljina.ToString("0.0", CultureInfo.InvariantCulture),
                            v.CenaVoznje.ToString("0.00", CultureInfo.InvariantCulture)
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UPOZORENJE: Neuspesno pisanje u voznje.csv: " + ex.Message);
                }
            }
        }

        private static string Escape(string vrednost)
        {
            if (vrednost == null) return "";
            return vrednost.Replace(";", ",");
        }
    }
}