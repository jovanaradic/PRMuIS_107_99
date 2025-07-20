using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using ZajednickeKlase.Modeli;
using ZajednickeKlase.Enumeracije;
using System.Diagnostics.Eventing.Reader;

namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {
            Console.Title = "SERVER – Taksi Dispečerski Sistem";

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


            byte[] bufferVozilo = new byte[1024];
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
                Ispisi(aktivnaVozila, zadaci);
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
                                    Console.WriteLine($"Greška prilikom prihvatanja konekcije vozila: {ex.Message}");
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
                                        string poruka = $"Zahtev odbijen: Klijent sa ID {zahtev1.IDKlijenta} već ima aktivnu vožnju.";
                                        byte[] bufferOdbijeno = Encoding.UTF8.GetBytes(poruka);
                                        serverSocketUDP.SendTo(bufferOdbijeno, klijentEPUDP);
                                        continue;
                                    }

                                    //server pronalazi najbolje vozilo
                                    TaksiVoziloModel najbolji = NadjiNajblizeVozilo(aktivnaVozila, zahtev1.pocetnaTacka);

                                    if (najbolji == null)
                                    {
                                        string odgovorKlijentu1 = $"Nema dostupnih vozila u ovom trenutku";
                                        byte[] bufferOdg1 = Encoding.UTF8.GetBytes(odgovorKlijentu1);
                                        serverSocketUDP.SendTo(bufferOdg1, klijentEPUDP);
                                        continue;
                                    }

                                    //klijent uspostavio komunikaciju -> saljemo zadatak najblizem vozilu, saljemo odgovor klijentu
                                    ZadatakModel zadatak = new ZadatakModel
                                    {
                                        ID = idZadatkaBrojac,
                                        pozicijaKlijenta = zahtev1.pocetnaTacka,
                                        zeljenaPozicija = zahtev1.krajnjaTacka,
                                        IDKlijenta = zahtev1.IDKlijenta,
                                        IDVozila = najbolji.Id,
                                        PredjenaRazdaljina = IzracunajRazdaljinu(zahtev1.pocetnaTacka, new Koordinata(najbolji.koordinataX, najbolji.koordinataY))
                                    };
                                    byte[] bufferZadatak = new byte[1024];

                                    zadaci[idZadatkaBrojac] = zadatak;
                                    zadatak.StatusZadatka = StatusZadatka.Aktivan;

                                    //slanje zadatka vozilu
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
                                            Console.WriteLine($"Greška pri slanju zadatka vozilu: {ex.Message}");
                                        }
                                    }

                                    double brzina = 0.8;
                                    double vreme = zadatak.PredjenaRazdaljina / brzina;
                                    //slanje odgovora klijentu
                                    string odgovorKlijentu = $"Vozilo {najbolji.Id} dolazi za priblizno {vreme} sekundi!";
                                    byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                    serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                    //dodavanje klijenta u rijecnik za pracenje zadatka
                                    brojacKorakaPoZadatku[najbolji.Id] = 0;
                                    EPPoIDKlijenta[zahtev1.IDKlijenta] = klijentEPUDP;
                                    VoziloKlijentID[najbolji.Id] = zahtev1.IDKlijenta;

                                    //za sledeci zadatak
                                    idZadatkaBrojac++;

                                    //prikazujemo listu zadataka
                                    Ispisi(aktivnaVozila, zadaci);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Greška prilikom obrade zahteva klijenta: {ex.Message}");
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

                                        Ispisi(aktivnaVozila, zadaci);
                                        continue;
                                    }
                                    using (MemoryStream ms = new MemoryStream(bufferVozilo, 0, primljeniBajtoviVozilo))
                                    {
                                        BinaryFormatter bf = new BinaryFormatter();
                                        object obj = bf.Deserialize(ms);

                                        if (obj is TaksiVoziloModel vozilo)
                                        {
                                            if (aktivnaVozila.ContainsKey(vozilo.Id))
                                            {
                                               

                                                var postojeci = aktivnaVozila[vozilo.Id];

                                                // Ažuriraj samo stvari koje se menjaju
                                                postojeci.koordinataX = vozilo.koordinataX;
                                                postojeci.koordinataY = vozilo.koordinataY;
                                                postojeci.Status = vozilo.Status;


                                                var zadatak = zadaci.Values.FirstOrDefault(z => z.IDVozila == postojeci.Id && z.StatusZadatka == StatusZadatka.Aktivan);
                                                if (postojeci.Status == StatusVozila.NaPutu)
                                                {
                                                    brojacKorakaPoZadatku[postojeci.Id]++;
                                                    int udaljenost = IzracunajRazdaljinu(new Koordinata(postojeci.koordinataX, postojeci.koordinataY), zadatak.pozicijaKlijenta);

                                                    if (brojacKorakaPoZadatku[postojeci.Id] % 4 == 0 && udaljenost > 2)
                                                    {
                                                        int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                        EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];
                                                        double vrijeme = udaljenost / 0.8;

                                                        string odgovorKlijentu = $"Vozilo se priblizava... Dolazi na odrediste za {vrijeme} sekundi!";
                                                        byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                        serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);
                                                    }
                                                }
                                                if (zadatak.pozicijaKlijenta.X == postojeci.koordinataX && zadatak.pozicijaKlijenta.Y == postojeci.koordinataY && postojeci.Status != StatusVozila.UVoznji)
                                                {
                                                    int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                    EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];

                                                    string odgovorKlijentu = $"Vozilo se trenutno nalazi na vasoj poziciji!";
                                                    byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                    serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);
                                                }
                                            }
                                            else
                                            {
                                                aktivnaVozila[vozilo.Id] = vozilo;
                                                socketPoIdVozila[vozilo.Id] = socket;
                                            }

                                            Ispisi(aktivnaVozila, zadaci);
                                        }

                                        //zavrsetak voznje
                                        else if (obj is StatusVoznje status)
                                        {
                                            if (aktivnaVozila.ContainsKey(status.IdVozila))
                                            {
                                                var v = aktivnaVozila[status.IdVozila];
                                                v.Km += status.Km;
                                                v.Zarada += status.CenaVoznje;
                                                v.BrojMusterija++;

                                                // Oznaci zadatak kao zavrsen
                                                foreach (var z in zadaci.Values)
                                                {
                                                    if (z.IDKlijenta == status.IdKlijenta && z.IDVozila == status.IdVozila && z.StatusZadatka == StatusZadatka.Aktivan)
                                                    {
                                                        z.StatusZadatka = StatusZadatka.Zavrsen;
                                                        break;
                                                    }
                                                }

                                                int idKlijenta = status.IdKlijenta;
                                                EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];

                                                string odgovorKlijentu = $"Stigli ste na odrediste! Voznja je placena {status.CenaVoznje} RSD!";
                                                byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                                serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                                Ispisi(aktivnaVozila, zadaci);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Greška prilikom obrade poruke vozila: {ex.Message}");
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
                                    Ispisi(aktivnaVozila, zadaci);
                                }
                            }
                        }
                    }
                }

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske {ex}");
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

        private static void Ispisi(Dictionary<int, TaksiVoziloModel> vozila, Dictionary<int, ZadatakModel> zadaci)
        {
            Console.Clear();
            Console.WriteLine($"=== TAKSI DISPEČERSKI SISTEM – {DateTime.Now:HH:mm:ss} ===\n");

            Console.WriteLine("VOZILA");
            Console.WriteLine("ID  Status      Lokacija    Km       Zarada      Musterija");
            foreach (var v in vozila.Values.OrderBy(v => v.Id))
            {
                Console.WriteLine($"{v.Id,-3} {v.Status,-10}  ({v.koordinataX,2},{v.koordinataY,2})  {v.Km,6:0.0}  {v.Zarada,8:0.00} RSD  {v.BrojMusterija,3}");
            }

            Console.WriteLine();
            Console.WriteLine("ZADACI");
            Console.WriteLine("ID Zadatka  ID Klijenta  ID Vozila  Status");
            foreach (var z in zadaci.Values.OrderBy(z => z.ID))
            {
                Console.WriteLine($"{z.ID,-10}  {z.IDKlijenta,-11}  {z.IDVozila,-9}  {z.StatusZadatka}");
            }

            Console.WriteLine("\nMAPA (20x20):\n");
            string[,] mapa = new string[20, 20];
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    mapa[x, y] = ".";

            foreach (var v in vozila.Values)
                if (v.koordinataX < 20 && v.koordinataY < 20)
                    mapa[v.koordinataX, v.koordinataY] = "V" + v.Id.ToString();

            //prikaz klijent i vozilo+klijent
            foreach (var z in zadaci.Values.Where(z => z.StatusZadatka == StatusZadatka.Aktivan))
            {
                var vozilo = vozila.ContainsKey(z.IDVozila) ? vozila[z.IDVozila] : null;

                if (vozilo != null)
                {
                    bool voziloNaPozicijiKlijenta = vozilo.koordinataX == z.pozicijaKlijenta.X && vozilo.koordinataY == z.pozicijaKlijenta.Y;

                    // klijent ceka
                    if (!voziloNaPozicijiKlijenta && z.pozicijaKlijenta.X < 20 && z.pozicijaKlijenta.Y < 20 && !vozilo.Status.Equals(StatusVozila.UVoznji))
                    {
                        mapa[z.pozicijaKlijenta.X, z.pozicijaKlijenta.Y] = "K" + z.IDKlijenta;
                    }

                    //(V+K) samo ako je pokupio klijenta ili su na istoj poziciji
                    if (vozilo.Status == StatusVozila.UVoznji || voziloNaPozicijiKlijenta)
                    {
                        if (vozilo.koordinataX < 20 && vozilo.koordinataY < 20)
                        {
                            mapa[vozilo.koordinataX, vozilo.koordinataY] = $"V{vozilo.Id}+K";
                        }
                    }
                }
            }
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 20; x++)
                    Console.Write($"{mapa[x, y],-6}");
                Console.WriteLine();
            }
        }

        private static TaksiVoziloModel NadjiNajblizeVozilo(Dictionary<int, TaksiVoziloModel> vozila, Koordinata klijent)
        {
            TaksiVoziloModel najblizi = null;
            double minUdaljenost = double.MaxValue;

            foreach (var vozilo in vozila.Values)
            {
                if (vozilo.Status == StatusVozila.Slobodno)
                {
                    double dist = Math.Sqrt(Math.Pow(vozilo.koordinataX - klijent.X, 2) + Math.Pow(vozilo.koordinataY - klijent.Y, 2));
                    if (dist < minUdaljenost)
                    {
                        minUdaljenost = dist;
                        najblizi = vozilo;
                    }
                }
            }

            return najblizi;
        }

        public static int IzracunajRazdaljinu(Koordinata a, Koordinata b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }
    }
}
