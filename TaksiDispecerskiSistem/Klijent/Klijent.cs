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

namespace Klijent
{
    public class Klijent
    {
        static void Main(string[] args)
        {
            Console.Title = "KLIJENT ";
            Console.Write("ID klijenta: ");
            int id;
            while (true)
            {
                
                string input = Console.ReadLine();
                if (int.TryParse(input, out id) && id >= 0)
                    break;
                Console.WriteLine("GREŠKA: ID mora biti pozitivan ceo broj. Pokušajte ponovo.");
            }

            Socket clientSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50001);

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("============================================");
                Console.WriteLine($"      KLIJENT ({id}) – Novi zahtev");
                Console.WriteLine("============================================");
                Console.Write("Početna (x y) ili 'kraj': ");
                string ln = Console.ReadLine();
                if (ln == "kraj") break;

                string[] p1 = ln.Split(' ');
                Console.Write("Krajnja (x y): ");
                string[] p2 = Console.ReadLine().Split(' ');

                // Kreiranje zahteva
                KlijentModel zahtev = new KlijentModel
                {
                    //potrebno kasnije dodati id mozda
                    //dodala
                    IDKlijenta = id,
                    pocetnaTacka = new Koordinata(int.Parse(p1[0]), int.Parse(p1[1])),
                    krajnjaTacka = new Koordinata(int.Parse(p2[0]), int.Parse(p2[1]))
                };

                byte[] bufferZahtev = new byte[1024];

                //slanje zahteva serveru
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, zahtev);
                    bufferZahtev = ms.ToArray();
                    int brBajta = clientSocketUDP.SendTo(bufferZahtev, 0, bufferZahtev.Length, SocketFlags.None, serverEP);
                }

                byte[] bufferOdgovorServera = new byte[1024];
                
                if (clientSocketUDP.Poll(4000 * 1000, SelectMode.SelectRead))
                {
                    int brBajtaOdg = clientSocketUDP.ReceiveFrom(bufferOdgovorServera, ref posiljaocEP);

                    string odgovorServera = Encoding.UTF8.GetString(bufferOdgovorServera, 0, brBajtaOdg);
                    Console.WriteLine("\n-------------------------------------------------");
                    Console.WriteLine("Odgovor servera:");
                    Console.WriteLine($"   {odgovorServera}");
                    Console.WriteLine("-------------------------------------------------");
                }
                else
                {
                    Console.WriteLine("\n-------------------------------------------------");
                    Console.WriteLine(" Server ne odgovara (nema dostupnih vozila?).");
                    Console.WriteLine("-------------------------------------------------");
                }
                

                Console.WriteLine("\nPritisni Enter za novi zahtev ili 'kraj' da izadješ...");
                if (Console.ReadLine()?.Trim().ToLower() == "kraj") break;

            }

            Console.WriteLine("Klijen zavrsava sa radom");
            clientSocketUDP.Close(); // Zatvaramo soket na kraju rada
            Console.ReadKey();
        }
    }
}
