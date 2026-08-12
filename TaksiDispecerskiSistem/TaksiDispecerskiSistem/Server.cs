using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using ZajednickeKlase;
using ZajednickeKlase.Modeli;
using ZajednickeKlase.Enumeracije;
using ZajednickeKlase.Lavirint;
using ZajednickeKlase.AlgoritmiPretrage;
using ZajednickeKlase.Evidencija;

namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {
            Console.Title = "SERVER - Taksi Dispečerski Sistem";

            Lavirint lavirint = GeneratorLavirinta.Generisi(
                Konfiguracija.SirinaLavirinta,
                Konfiguracija.VisinaLavirinta,
                Konfiguracija.VerovatnocaDodatnihProlaza,
                Konfiguracija.SemeLavirinta);

            //TCP - vozilo
            Socket serverSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //TCP port 50000
            IPEndPoint serverEPTCP = new IPEndPoint(IPAddress.Any, 50000);
            serverSocketTCP.Bind(serverEPTCP);
            serverSocketTCP.Blocking = false;
            int maxKlijenata = 10;
            serverSocketTCP.Listen(maxKlijenata);

            //UDP - klijent
            Socket serverSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //UDP port 50001
            IPEndPoint serverEPUDP = new IPEndPoint(IPAddress.Any, 50001);
            serverSocketUDP.Bind(serverEPUDP);
            serverSocketUDP.Blocking = false;

            byte[] bufferVozilo = new byte[4096];
            byte[] bufferKlijent = new byte[1024];

            Dictionary<int, TaksiVoziloModel> aktivnaVozila = new Dictionary<int, TaksiVoziloModel>();
            Dictionary<int, Socket> socketPoIdVozila = new Dictionary<int, Socket>();
            Dictionary<int, ZadatakModel> zadaci = new Dictionary<int, ZadatakModel>();
            int idZadatkaBrojac = 0;
            //pracenje koraka za obavjestavanje klijenta
            Dictionary<int, int> brojacKorakaPoZadatku = new Dictionary<int, int>();
            Dictionary<int, EndPoint> EPPoIDKlijenta = new Dictionary<int, EndPoint>();
            Dictionary<int, int> VoziloKlijentID = new Dictionary<int, int>();

            List<Socket> socketsVozila = new List<Socket>();
            List<Socket> socketsKorisnici = new List<Socket>();

            try
            {
                //na pocetku ispisujemo
                Ispisi(lavirint, aktivnaVozila, zadaci);
                while (true)
                {
                    List<Socket> checkRead = new List<Socket>();
                    List<Socket> checkError = new List<Socket>();

                    checkRead.Add(serverSocketTCP);
                    checkRead.Add(serverSocketUDP);

                    foreach (Socket socket in socketsVozila)
                    {
                        checkRead.Add(socket);
                    }

                    foreach (Socket socket in socketsKorisnici)
                    {
                        checkRead.Add(socket);
                    }

                    Socket.Select(checkRead, null, null, 1000);

                    if (checkRead.Count > 0)
                    {

                        foreach (Socket socket in checkRead)
                        {
                            //zahtjev vozila za konekciju
                            if (socket == serverSocketTCP)
                            {
                                try
                                {
                                    Socket vozilo = serverSocketTCP.Accept();
                                    vozilo.Blocking = false;
                                    socketsVozila.Add(vozilo);
                                }
                                catch (SocketException ex)
                                {
                                    Console.WriteLine("Greška prilikom prihvatanja konekcije vozila: " + ex.Message);
                                }
                            }
                            //klijent salje ZAHTEV
                            else if (socket == serverSocketUDP)
                            {
                                try
                                {
                                    EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0);
                                    KlijentModel zahtev1 = null;
                                    int primljenihBajtovaKlijent1 = serverSocketUDP.ReceiveFrom(bufferKlijent, ref klijentEPUDP);
                                    using (MemoryStream ms = new MemoryStream(bufferKlijent, 0, primljenihBajtovaKlijent1))
                                    {
                                        BinaryFormatter bf = new BinaryFormatter();
                                        zahtev1 = bf.Deserialize(ms) as KlijentModel;
                                    }

                                    //ako klijent sa istim ID vec ima aktivan zadatak
                                    bool klijentImaZadatak = zadaci.Values.Any(z => z.IDKlijenta == zahtev1.IDKlijenta && z.StatusZadatka == StatusZadatka.Aktivan);

                                    if (klijentImaZadatak)
                                    {
                                        string poruka = "Zahtev odbijen: Klijent sa ID " + zahtev1.IDKlijenta + " vec ima aktivnu voznju.";
                                        byte[] bufferOdbijeno = Encoding.UTF8.GetBytes(poruka);
                                        serverSocketUDP.SendTo(bufferOdbijeno, klijentEPUDP);
                                        continue;
                                    }

                                    //provera da su koordinate unutar granica lavirinta
                                    if (!lavirint.UGranicama(zahtev1.pocetnaTacka.X, zahtev1.pocetnaTacka.Y) ||
                                        !lavirint.UGranicama(zahtev1.krajnjaTacka.X, zahtev1.krajnjaTacka.Y))
                                    {
                                        string odgovorVanGranica = "Zahtev odbijen: koordinate moraju biti u opsegu 0-" + (Konfiguracija.SirinaLavirinta - 1) + " (X) i 0-" + (Konfiguracija.VisinaLavirinta - 1) + " (Y).";
                                        byte[] bufferVanGranica = Encoding.UTF8.GetBytes(odgovorVanGranica);
                                        serverSocketUDP.SendTo(bufferVanGranica, klijentEPUDP);
                                        continue;
                                    }

                                    //server pronalazi najbolje (najblize) slobodno vozilo
                                    TaksiVoziloModel najbolji = NadjiNajblizeVozilo(lavirint, aktivnaVozila, zahtev1.pocetnaTacka);

                                    if (najbolji == null)
                                    {
                                        string odgovorKlijentu1 = "Nema dostupnih vozila u ovom trenutku";
                                        byte[] bufferOdg1 = Encoding.UTF8.GetBytes(odgovorKlijentu1);
                                        serverSocketUDP.SendTo(bufferOdg1, klijentEPUDP);
                                        continue;
                                    }

                                    //--- LOGIKA PRETRAGE NA DISPECERSKOJ STANICI ---
                                    //za odabrano vozilo racunamo optimalnu putanju kroz lavirint u dva segmenta:
                                    //  1) od trenutne pozicije vozila do klijenta
                                    //  2) od pozicije klijenta do zeljenog odredista
                                    //Za svaki segment isprobavamo tri algoritma (BFS, Dijkstra, A*) i biramo najbolji
                                    //(najkraca putanja, a kod izjednacenja - najbrze pronadjena), a rezultate poredjenja
                                    //belezimo radi kasnije analize.
                                    Koordinata pozicijaVozila = new Koordinata(najbolji.koordinataX, najbolji.koordinataY);

                                    List<RezultatPretrage> rezultatiDoKlijenta;
                                    RezultatPretrage optimalanDoKlijenta = PronalazacPuta.NadjiOptimalnuPutanju(
                                        lavirint, pozicijaVozila, zahtev1.pocetnaTacka, out rezultatiDoKlijenta);

                                    List<RezultatPretrage> rezultatiDoOdredista;
                                    RezultatPretrage optimalanDoOdredista = PronalazacPuta.NadjiOptimalnuPutanju(
                                        lavirint, zahtev1.pocetnaTacka, zahtev1.krajnjaTacka, out rezultatiDoOdredista);

                                    if (optimalanDoKlijenta == null || optimalanDoOdredista == null)
                                    {
                                        string odgovorNemaPuta = "Zahtev odbijen: nije moguće pronacžći putanju kroz lavirint.";
                                        byte[] bufferNemaPuta = Encoding.UTF8.GetBytes(odgovorNemaPuta);
                                        serverSocketUDP.SendTo(bufferNemaPuta, klijentEPUDP);
                                        continue;
                                    }

                                    int brojAktivnihZadatakaSad = zadaci.Values.Count(z => z.StatusZadatka == StatusZadatka.Aktivan);

                                    EvidencijaPodataka.SacuvajPoredjenjeAlgoritama(idZadatkaBrojac, "Vozilo->Klijent", rezultatiDoKlijenta, optimalanDoKlijenta.Algoritam,
                                        Konfiguracija.SirinaLavirinta, Konfiguracija.VisinaLavirinta, aktivnaVozila.Count, brojAktivnihZadatakaSad);
                                    EvidencijaPodataka.SacuvajPoredjenjeAlgoritama(idZadatkaBrojac, "Klijent->Odrediste", rezultatiDoOdredista, optimalanDoOdredista.Algoritam,
                                        Konfiguracija.SirinaLavirinta, Konfiguracija.VisinaLavirinta, aktivnaVozila.Count, brojAktivnihZadatakaSad);

                                    //klijent uspostavio komunikaciju -> saljemo zadatak najblizem vozilu, saljemo odgovor klijentu
                                    ZadatakModel zadatak = new ZadatakModel
                                    {
                                        ID = idZadatkaBrojac,
                                        pozicijaKlijenta = zahtev1.pocetnaTacka,
                                        zeljenaPozicija = zahtev1.krajnjaTacka,
                                        IDKlijenta = zahtev1.IDKlijenta,
                                        IDVozila = najbolji.Id,
                                        //naplacuje se stvarna duzina voznje (klijent -> odrediste)
                                        PredjenaRazdaljina = optimalanDoOdredista.DuzinaPuta,
                                        PutanjaDoKlijenta = optimalanDoKlijenta.Putanja,
                                        PutanjaDoOdredista = optimalanDoOdredista.Putanja,
                                        IzabraniAlgoritam = optimalanDoKlijenta.Algoritam + " / " + optimalanDoOdredista.Algoritam
                                    };
                                    byte[] bufferZadatak;

                                    zadaci[idZadatkaBrojac] = zadatak;
                                    zadatak.StatusZadatka = StatusZadatka.Aktivan;

                                    //slanje zadatka (ukljucujuci celu putanju - niz koordinata) vozilu
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        BinaryFormatter bf = new BinaryFormatter();
                                        bf.Serialize(ms, zadatak);
                                        bufferZadatak = ms.ToArray();

                                        try
                                        {
                                            Socket voziloSocket = socketPoIdVozila[najbolji.Id];
                                            IPEndPoint voziloEPTCP = voziloSocket.RemoteEndPoint as IPEndPoint;
                                            int brBajta = voziloSocket.SendTo(bufferZadatak, 0, bufferZadatak.Length, SocketFlags.None, voziloEPTCP);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("Greška pri slanju zadatka vozilu: " + ex.Message);
                                        }
                                    }

                                    double vreme = optimalanDoKlijenta.DuzinaPuta * (Konfiguracija.PauzaKretanjaMs / 1000.0);
                                    //slanje odgovora klijentu
                                    string odgovorKlijentu = "Vozilo " + najbolji.Id + " dolazi za priblizno " + vreme.ToString("0.0") + " sekundi! (Ruta: " + optimalanDoKlijenta.Algoritam + ", " + optimalanDoKlijenta.DuzinaPuta + " polja)";
                                    byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                    serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                    //dodavanje klijenta u rijecnik za pracenje zadatka
                                    brojacKorakaPoZadatku[najbolji.Id] = 0;
                                    EPPoIDKlijenta[zahtev1.IDKlijenta] = klijentEPUDP;
                                    VoziloKlijentID[najbolji.Id] = zahtev1.IDKlijenta;

                                    //za sledeci zadatak
                                    idZadatkaBrojac++;

                                    //prikazujemo listu zadataka
                                    Ispisi(lavirint, aktivnaVozila, zadaci);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Greška prilikom obrade zahteva klijenta: " + ex.Message);
                                }
                            }

                            if (socketsVozila.Contains(socket))
                            {
                                try
                                {
                                    int primljeniBajtoviVozilo = socket.Receive(bufferVozilo);
                                    if (primljeniBajtoviVozilo == 0)
                                    {
                                        int idVozilaZaBrisanje = -1;
                                        foreach (var par in socketPoIdVozila)
                                        {
                                            if (par.Value == socket)
                                            {
                                                idVozilaZaBrisanje = par.Key;
                                                break;
                                            }
                                        }
                                        if (idVozilaZaBrisanje != -1)
                                        {
                                            aktivnaVozila.Remove(idVozilaZaBrisanje);
                                            socketPoIdVozila.Remove(idVozilaZaBrisanje);
                                        }
                                        socketsVozila.Remove(socket);
                                        socket.Close();

                                        Ispisi(lavirint, aktivnaVozila, zadaci);
                                        continue;
                                    }
                                    using (MemoryStream ms = new MemoryStream(bufferVozilo, 0, primljeniBajtoviVozilo))
                                    {
                                        BinaryFormatter bf = new BinaryFormatter();
                                        object obj = bf.Deserialize(ms);

                                        if (obj is TaksiVoziloModel)
                                        {
                                            TaksiVoziloModel vozilo = (TaksiVoziloModel)obj;
                                            if (aktivnaVozila.ContainsKey(vozilo.Id))
                                            {
                                                var postojeci = aktivnaVozila[vozilo.Id];

                                                // Ažuriraj samo stvari koje se menjaju
                                                postojeci.koordinataX = vozilo.koordinataX;
                                                postojeci.koordinataY = vozilo.koordinataY;
                                                postojeci.Status = vozilo.Status;

                                                var zadatak = zadaci.Values.FirstOrDefault(z => z.IDVozila == postojeci.Id && z.StatusZadatka == StatusZadatka.Aktivan);
                                                if (zadatak != null)
                                                {
                                                    if (postojeci.Status == StatusVozila.NaPutu)
                                                    {
                                                        brojacKorakaPoZadatku[postojeci.Id]++;

                                                        int indeksTrenutneCelije = zadatak.PutanjaDoKlijenta.FindIndex(
                                                            k => k.X == postojeci.koordinataX && k.Y == postojeci.koordinataY);
                                                        int preostaliKoraci = indeksTrenutneCelije >= 0
                                                            ? zadatak.PutanjaDoKlijenta.Count - 1 - indeksTrenutneCelije
                                                            : 0;

                                                        if (brojacKorakaPoZadatku[postojeci.Id] % 4 == 0 && preostaliKoraci > 2)
                                                        {
                                                            int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                            EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];
                                                            double vrijeme = preostaliKoraci * (Konfiguracija.PauzaKretanjaMs / 1000.0);

                                                            string odgovorKlijentu = "Vozilo se priblizava... Dolazi na odrediste za " + vrijeme.ToString("0.0") + " sekundi!";
                                                            byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                            serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);
                                                        }
                                                    }

                                                    if (zadatak.pozicijaKlijenta.X == postojeci.koordinataX && zadatak.pozicijaKlijenta.Y == postojeci.koordinataY && postojeci.Status != StatusVozila.UVoznji)
                                                    {
                                                        int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                        EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];

                                                        string odgovorKlijentu = "Vozilo se trenutno nalazi na vasoj poziciji!";
                                                        byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                        serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                aktivnaVozila[vozilo.Id] = vozilo;
                                                socketPoIdVozila[vozilo.Id] = socket;
                                            }

                                            Ispisi(lavirint, aktivnaVozila, zadaci);
                                        }

                                        //zavrsetak voznje
                                        else if (obj is StatusVoznje)
                                        {
                                            StatusVoznje status = (StatusVoznje)obj;
                                            if (aktivnaVozila.ContainsKey(status.IdVozila))
                                            {
                                                var v = aktivnaVozila[status.IdVozila];
                                                v.Km += status.Km;
                                                v.Zarada += status.CenaVoznje;
                                                v.BrojMusterija++;

                                                // Oznaci zadatak kao zavrsen
                                                string algoritamZadatka = null;
                                                int idZadatkaZavrsenog = -1;
                                                foreach (var z in zadaci.Values)
                                                {
                                                    if (z.IDKlijenta == status.IdKlijenta && z.IDVozila == status.IdVozila && z.StatusZadatka == StatusZadatka.Aktivan)
                                                    {
                                                        z.StatusZadatka = StatusZadatka.Zavrsen;
                                                        algoritamZadatka = z.IzabraniAlgoritam;
                                                        idZadatkaZavrsenog = z.ID;
                                                        break;
                                                    }
                                                }

                                                //evidentiramo zavrsenu voznju (CSV)
                                                EvidencijaPodataka.SacuvajZavrsenuVoznju(new EvidencijaVoznje
                                                {
                                                    Vreme = DateTime.Now,
                                                    IdZadatka = idZadatkaZavrsenog,
                                                    IdKlijenta = status.IdKlijenta,
                                                    IdVozila = status.IdVozila,
                                                    Algoritam = algoritamZadatka,
                                                    PredjenaRazdaljina = status.Km,
                                                    CenaVoznje = status.CenaVoznje
                                                });

                                                int idKlijenta = status.IdKlijenta;
                                                EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];

                                                string odgovorKlijentu = "Stigli ste na odrediste! Voznja je placena " + status.CenaVoznje + " RSD!";
                                                byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                                Ispisi(lavirint, aktivnaVozila, zadaci);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Greška prilikom obrade poruke vozila: " + ex.Message);
                                    //vozilo prisilno zatvorilo konekciju
                                    int idZaBrisanje = -1;
                                    foreach (var par in socketPoIdVozila)
                                    {
                                        if (par.Value == socket)
                                        {
                                            idZaBrisanje = par.Key;
                                            break;
                                        }
                                    }
                                    if (idZaBrisanje != -1)
                                    {
                                        socketPoIdVozila.Remove(idZaBrisanje);
                                        aktivnaVozila.Remove(idZaBrisanje);
                                    }
                                    socketsVozila.Remove(socket);
                                    socket.Close();
                                    Console.WriteLine("Vozilo se isključilo (greška).");
                                    Ispisi(lavirint, aktivnaVozila, zadaci);
                                }
                            }
                        }
                    }
                }

            }
            catch (SocketException ex)
            {
                Console.WriteLine("Došlo je do greške " + ex);
            }

            Console.WriteLine("Server zavrsava sa radom");
            serverSocketTCP.Close();
            serverSocketUDP.Close();
            foreach (var s in socketsKorisnici)
                s.Close();
            foreach (var s in socketsVozila)
                s.Close();
            Console.ReadKey();
        }

        private static void Ispisi(Lavirint lavirint, Dictionary<int, TaksiVoziloModel> vozila, Dictionary<int, ZadatakModel> zadaci)
        {
            Console.Clear();
            Console.WriteLine("=== TAKSI DISPECERSKI SISTEM - " + DateTime.Now.ToString("HH:mm:ss") + " ===\n");

            Console.WriteLine("VOZILA");
            Console.WriteLine("ID  Status      Lokacija    Km       Zarada      Musterija");
            foreach (var v in vozila.Values.OrderBy(v => v.Id))
            {
                Console.WriteLine(v.Id.ToString().PadRight(3) + " " + v.Status.ToString().PadRight(10) + "  (" + v.koordinataX.ToString().PadLeft(2) + "," + v.koordinataY.ToString().PadLeft(2) + ")  " + v.Km.ToString("0.0").PadLeft(6) + "  " + v.Zarada.ToString("0.00").PadLeft(8) + " RSD  " + v.BrojMusterija.ToString().PadLeft(3));
            }

            Console.WriteLine();
            Console.WriteLine("ZADACI");
            Console.WriteLine("ID Zadatka  ID Klijenta  ID Vozila  Algoritam            Status");
            foreach (var z in zadaci.Values.OrderBy(z => z.ID))
            {
                Console.WriteLine(z.ID.ToString().PadRight(10) + "  " + z.IDKlijenta.ToString().PadRight(11) + "  " + z.IDVozila.ToString().PadRight(9) + "  " + (z.IzabraniAlgoritam ?? "").PadRight(20) + "  " + z.StatusZadatka);
            }

            Console.WriteLine("\nMAPA - LAVIRINT (" + lavirint.Sirina + "x" + lavirint.Visina + "):\n");

            //oznake koje se ispisuju u odgovarajucim celijama lavirinta
            Dictionary<string, string> oznake = new Dictionary<string, string>();

            foreach (var v in vozila.Values)
                if (lavirint.UGranicama(v.koordinataX, v.koordinataY))
                    oznake[v.koordinataX + "_" + v.koordinataY] = "V" + v.Id;

            //prikaz klijenta i vozila+klijenta
            foreach (var z in zadaci.Values.Where(z => z.StatusZadatka == StatusZadatka.Aktivan))
            {
                var vozilo = vozila.ContainsKey(z.IDVozila) ? vozila[z.IDVozila] : null;
                if (vozilo == null) continue;

                bool voziloNaPozicijiKlijenta = vozilo.koordinataX == z.pozicijaKlijenta.X && vozilo.koordinataY == z.pozicijaKlijenta.Y;

                // klijent ceka
                if (!voziloNaPozicijiKlijenta && lavirint.UGranicama(z.pozicijaKlijenta.X, z.pozicijaKlijenta.Y) && vozilo.Status != StatusVozila.UVoznji)
                {
                    oznake[z.pozicijaKlijenta.X + "_" + z.pozicijaKlijenta.Y] = "K" + z.IDKlijenta;
                }

                //(V+K) samo ako je pokupio klijenta ili su na istoj poziciji
                if (vozilo.Status == StatusVozila.UVoznji || voziloNaPozicijiKlijenta)
                {
                    if (lavirint.UGranicama(vozilo.koordinataX, vozilo.koordinataY))
                        oznake[vozilo.koordinataX + "_" + vozilo.koordinataY] = "V" + vozilo.Id + "K";
                }
            }

            IscrtajLavirint(lavirint, oznake);
        }

        private static void IscrtajLavirint(Lavirint lavirint, Dictionary<string, string> oznake)
        {
            const int sirinaCelije = 4;
            int mrezaSirina = 2 * lavirint.Sirina + 1;
            int mrezaVisina = 2 * lavirint.Visina + 1;

            for (int my = 0; my < mrezaVisina; my++)
            {
                StringBuilder red = new StringBuilder();

                if (my % 2 == 0)
                {
                    // red horizontalnih zidova i coskova
                    for (int mx = 0; mx < mrezaSirina; mx++)
                    {
                        if (mx % 2 == 0)
                        {
                            red.Append("+");
                        }
                        else
                        {
                            bool zid = lavirint.Zidovi[mx, my];
                            red.Append(zid ? new string('-', sirinaCelije) : new string(' ', sirinaCelije));
                        }
                    }
                }
                else
                {
                    for (int mx = 0; mx < mrezaSirina; mx++)
                    {
                        if (mx % 2 == 0)
                        {
                            bool zid = lavirint.Zidovi[mx, my];
                            red.Append(zid ? "|" : " ");
                        }
                        else
                        {
                            int cx = (mx - 1) / 2;
                            int cy = (my - 1) / 2;
                            string kljuc = cx + "_" + cy;
                            string sadrzaj;
                            if (oznake.TryGetValue(kljuc, out sadrzaj))
                                red.Append((sadrzaj.Length > sirinaCelije ? sadrzaj.Substring(0, sirinaCelije) : sadrzaj).PadRight(sirinaCelije));
                            else
                                red.Append(new string(' ', sirinaCelije));
                        }
                    }
                }

                Console.WriteLine(red.ToString());
            }
        }

        private static TaksiVoziloModel NadjiNajblizeVozilo(Lavirint lavirint, Dictionary<int, TaksiVoziloModel> vozila, Koordinata klijent)
        {
            TaksiVoziloModel najblizi = null;
            int minDuzinaPuta = int.MaxValue;

            foreach (var vozilo in vozila.Values)
            {
                if (vozilo.Status == StatusVozila.Slobodno)
                {
                    Koordinata pozicijaVozila = new Koordinata(vozilo.koordinataX, vozilo.koordinataY);
                    RezultatPretrage rezultat = BFS.Pretrazi(lavirint, pozicijaVozila, klijent);

                    if (rezultat.Pronadjeno && rezultat.DuzinaPuta < minDuzinaPuta)
                    {
                        minDuzinaPuta = rezultat.DuzinaPuta;
                        najblizi = vozilo;
                    }
                }
            }

            return najblizi;
        }
    }
}