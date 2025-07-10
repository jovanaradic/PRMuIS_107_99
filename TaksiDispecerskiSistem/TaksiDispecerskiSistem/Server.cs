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

namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {

            //UDP - klijent

            Socket serverSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //UDP port 500001
            IPEndPoint serverEPUDP = new IPEndPoint(IPAddress.Any, 50001); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke
            serverSocketUDP.Bind(serverEPUDP); // Povezujemo serverov soket sa njegovim EP
            EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke


            byte[] bufferKlijent = new byte[1024];

            try
            {
                while (true)
                {
                    KlijentModel zahtev = null;
                    int primljenihBajtovaKlijent = serverSocketUDP.ReceiveFrom(bufferKlijent, ref klijentEPUDP);
                    using (MemoryStream ms = new MemoryStream(bufferKlijent, 0, primljenihBajtovaKlijent))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        zahtev = bf.Deserialize(ms) as KlijentModel;
                        Console.WriteLine("Pristigao klijentski zahtev: ");
                        Console.WriteLine($"Trenutna pozicija klijenta: {zahtev.pocetnaTacka.X}, {zahtev.pocetnaTacka.Y}.");
                        Console.WriteLine($"Zeljeno odrediste klijenta: {zahtev.krajnjaTacka.X}, {zahtev.krajnjaTacka.Y}.");
                    } // kreiranje zadatka nakon zavrsetka voyila

                }

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske {ex}");
            }

            Console.WriteLine("Server zavrsava sa radom");
            Console.ReadKey();
            serverSocketUDP.Close();
        }
    }
}
