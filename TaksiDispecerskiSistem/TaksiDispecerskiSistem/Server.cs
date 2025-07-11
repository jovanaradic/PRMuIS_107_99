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
            //TCP - vozilo

            Socket serverSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //TCP port 50000
            IPEndPoint serverEPTCP = new IPEndPoint(IPAddress.Any, 50000);
            serverSocketTCP.Bind(serverEPTCP);
            int maxKlijenata = 1;
            serverSocketTCP.Listen(maxKlijenata);
            Socket voziloSocketTCP = serverSocketTCP.Accept();
            IPEndPoint voziloEPTCP = voziloSocketTCP.RemoteEndPoint as IPEndPoint;

            Console.WriteLine($"Povezalo se vozilo, njegova adresa je {voziloEPTCP}");

            //UDP - klijent

            Socket serverSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //UDP port 500001
            IPEndPoint serverEPUDP = new IPEndPoint(IPAddress.Any, 50001); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke
            serverSocketUDP.Bind(serverEPUDP); // Povezujemo serverov soket sa njegovim EP
            EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke


            byte[] bufferKlijent = new byte[1024];
            byte[] bufferVozilo = new byte[1024];

            try
            {
                while (true)
                {
                    int primljeniBajtoviVozilo = voziloSocketTCP.Receive(bufferVozilo);
                    TaksiVoziloModel taksiVozilo = null;
                    using (MemoryStream ms = new MemoryStream(bufferVozilo, 0, primljeniBajtoviVozilo))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        taksiVozilo = bf.Deserialize(ms) as TaksiVoziloModel;
                        Console.WriteLine($"Trenutna pozicija vozila: {taksiVozilo.koordinataX}, {taksiVozilo.koordinataY}");
                        if (taksiVozilo.Status == StatusVozila.NaPutu)
                        {
                            Console.WriteLine($"Status vozila: na putu");
                        }
                        else if (taksiVozilo.Status == StatusVozila.UVoznji)
                        {
                            Console.WriteLine($"Status vozila: u voznji");
                        }
                        else
                        {
                            Console.WriteLine($"Status vozila: cekanje");
                        }

                    }

                    KlijentModel zahtev = null;
                    int primljenihBajtovaKlijent = serverSocketUDP.ReceiveFrom(bufferKlijent, ref klijentEPUDP);
                    using (MemoryStream ms = new MemoryStream(bufferKlijent, 0, primljenihBajtovaKlijent))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        zahtev = bf.Deserialize(ms) as KlijentModel;
                        Console.WriteLine("Pristigao klijentski zahtev: ");
                        Console.WriteLine($"Trenutna pozicija klijenta: {zahtev.pocetnaTacka.X}, {zahtev.pocetnaTacka.Y}.");
                        Console.WriteLine($"Zeljeno odrediste klijenta: {zahtev.krajnjaTacka.X}, {zahtev.krajnjaTacka.Y}.");
                    }

                    ZadatakModel zadatak = new ZadatakModel
                    {
                        IDKlijenta = zahtev.IDKlijenta,
                        //unijeti id u model vozila
                        IDVozila = 2,
                        PredjenaRazdaljina = 3
                    };
                    byte[] bufferZadatak = new byte[1024];

                    //slanje zahteva serveru
                    using (MemoryStream ms = new MemoryStream())
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        bf.Serialize(ms, zadatak);
                        bufferZadatak = ms.ToArray();
                        int brBajta = voziloSocketTCP.SendTo(bufferZadatak, 0, bufferZadatak.Length, SocketFlags.None, voziloEPTCP);
                    }

                }

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske {ex}");
            }

            Console.WriteLine("Server zavrsava sa radom");
            Console.ReadKey();
            serverSocketUDP.Close();
            serverSocketTCP.Close();
            voziloSocketTCP.Close();
        }
    }
}
