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
                                Socket vozilo = serverSocketTCP.Accept();
                                vozilo.Blocking = false;
                                socketsVozila.Add(vozilo);
                            }
                            //klijent salje ZAHTEV
                            else if (socket == serverSocketUDP)
                            {
                                EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0);
                                KlijentModel zahtev1 = null;
                                int primljenihBajtovaKlijent1 = serverSocketUDP.ReceiveFrom(bufferKlijent, ref klijentEPUDP);
                                using (MemoryStream ms = new MemoryStream(bufferKlijent, 0, primljenihBajtovaKlijent1))
                                {
                                    BinaryFormatter bf = new BinaryFormatter();
                                    zahtev1 = bf.Deserialize(ms) as KlijentModel;
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
                                    ID = zadaci.Count() + 1,
                                    pozicijaKlijenta = zahtev1.pocetnaTacka,
                                    zeljenaPozicija = zahtev1.krajnjaTacka,
                                    IDKlijenta = zahtev1.IDKlijenta,
                                    IDVozila = najbolji.Id,
                                    PredjenaRazdaljina = IzracunajRazdaljinu(zahtev1.pocetnaTacka, new Koordinata(najbolji.koordinataX, najbolji.koordinataY))
                                };
                                byte[] bufferZadatak = new byte[1024];

                                zadaci[najbolji.Id] = zadatak;
                                zadatak.StatusZadatka = StatusZadatka.Aktivan;

                                //slanje zadatka vozilu
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    BinaryFormatter bf = new BinaryFormatter();
                                    bf.Serialize(ms, zadatak);
                                    bufferZadatak = ms.ToArray();

                                    Socket voziloSocket = socketPoIdVozila[najbolji.Id];
                                    IPEndPoint voziloEPTCP = voziloSocket.RemoteEndPoint as IPEndPoint;
                                    int brBajta = voziloSocket.SendTo(bufferZadatak, 0, bufferZadatak.Length, SocketFlags.None, voziloEPTCP);
                                }

                                double brzina = 1.0;
                                double vreme = zadatak.PredjenaRazdaljina / brzina;
                                string odgovorKlijentu = $"Vozilo {najbolji.Id} dolazi za priblizno {vreme} sekundi!";
                                byte[] bufferOdg = Encoding.UTF8.GetBytes(odgovorKlijentu);
                                serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                //prikazujemo listu zadataka
                                Ispisi(aktivnaVozila, zadaci);

                            }

                            if (socketsVozila.Contains(socket))
                            {
                                int primljeniBajtoviVozilo = socket.Receive(bufferVozilo);
                                using (MemoryStream ms = new MemoryStream(bufferVozilo, 0, primljeniBajtoviVozilo))
                                {
                                    BinaryFormatter bf = new BinaryFormatter();
                                    object obj = bf.Deserialize(ms);

                                    //PROVJERITI
                                    //prikljucuje se novo vozilo || update stanje vozila
                                    if (obj is TaksiVoziloModel vozilo)
                                    {
                                        aktivnaVozila[vozilo.Id] = vozilo;
                                        socketPoIdVozila[vozilo.Id] = socket;

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
                                                if (z.IDKlijenta == status.IdKlijenta && z.IDVozila == status.IdVozila)
                                                {
                                                    z.StatusZadatka = StatusZadatka.Zavrsen;
                                                    break;
                                                }
                                            }

                                            Ispisi(aktivnaVozila, zadaci);
                                        }
                                    }
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
            Console.ReadKey();
            serverSocketTCP.Close();
            serverSocketUDP.Close();
            foreach (var s in socketsKorisnici)
                s.Close();
            foreach (var s in socketsVozila)
                s.Close();
        }

        private static void Ispisi(Dictionary<int, TaksiVoziloModel> vozila, Dictionary<int, ZadatakModel> zadaci)
        {
            Console.Clear();
            Console.WriteLine($"=== TAKSI DISPEČERSKI SISTEM – {DateTime.Now:HH:mm:ss} ===\n");

            Console.WriteLine("VOZILA");
            Console.WriteLine("ID  Status      Lokacija    Km       Zarada      Musterija");
            foreach (var v in vozila.Values.OrderBy(v => v.Id))
            {
                Console.WriteLine($"{v.Id,2}  {v.Status,-10} ({v.koordinataX,2},{v.koordinataY,2})  {v.Km,7:F1}  {v.Zarada,9:C}  {v.BrojMusterija,10}");
            }

            Console.WriteLine();
            Console.WriteLine("ZADACI");
            Console.WriteLine("ID Zadatka  ID Klijenta  ID Vozila  Status");
            foreach (var z in zadaci.Values.OrderBy(z => z.ID))
            {
                Console.WriteLine($"{z.ID,-10}  {z.IDKlijenta,-11}  {z.IDVozila,-9}  {z.StatusZadatka}");
            }

            Console.WriteLine("\nMAPA (20x20):\n");
            char[,] mapa = new char[20, 20];
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    mapa[x, y] = '.';

            foreach (var v in vozila.Values)
                if (v.koordinataX < 20 && v.koordinataY < 20)
                    mapa[v.koordinataX, v.koordinataY] = 'V';

            foreach (var z in zadaci.Values.Where(z => z.StatusZadatka == StatusZadatka.Aktivan))
            {
                var vozilo = vozila[z.IDVozila];
                if (vozilo.Status == StatusVozila.NaPutu)
                {
                    if (z.pozicijaKlijenta.X < 20 && z.pozicijaKlijenta.Y < 20)
                        mapa[z.pozicijaKlijenta.X, z.pozicijaKlijenta.Y] = 'K';
                }
            }
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 20; x++)
                    Console.Write(mapa[x, y] + " ");
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
