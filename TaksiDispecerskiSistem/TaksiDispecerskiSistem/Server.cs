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
            int maxKlijenata = 10; // da li ostavljamo ovako?
            serverSocketTCP.Listen(maxKlijenata);


            //UDP - klijent
            Socket serverSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //UDP port 500001
            IPEndPoint serverEPUDP = new IPEndPoint(IPAddress.Any, 50001);
            serverSocketUDP.Bind(serverEPUDP); 
            EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0); 

            byte[] bufferKlijent = new byte[1024];
            byte[] bufferVozilo = new byte[1024];

            Dictionary<int, TaksiVoziloModel> aktivnaVozila = new Dictionary<int, TaksiVoziloModel>();
            Dictionary<int, Socket> socketPoIdVozila = new Dictionary<int, Socket>();
            Dictionary<int, ZadatakModel> zadaci = new Dictionary<int, ZadatakModel>();

            List<Socket> socketsVozila = new List<Socket>();
            try
            {
                //na pocetku ispisujemo
                Ispisi(aktivnaVozila, zadaci);
                while (true)
                {

                    List<Socket> checkRead = new List<Socket>();
                    //obraditi checkError?

                    checkRead.Add(serverSocketTCP);
                    checkRead.Add(serverSocketUDP);

                    foreach (Socket socket in socketsVozila)
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

                            //klijentsko slanje zahteva
                            //izbor najblizeg vozila
                            //slanje zadatka vozilu
                            //slanje odgovora klijentu

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

            //ispisivanje zadataka

            Console.WriteLine("\nMAPA (20x20):\n");
            char[,] mapa = new char[20, 20];
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    mapa[x, y] = '.';

            foreach (var v in vozila.Values)
                if (v.koordinataX < 20 && v.koordinataY < 20)
                    mapa[v.koordinataX, v.koordinataY] = 'V';

            //potrebno dodati prikaz klijenta na mapi

            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 20; x++)
                    Console.Write(mapa[x, y] + " ");
                Console.WriteLine();
            }
        }
    }
}
