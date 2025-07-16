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
                Koordinata pocetna = null;
                Koordinata krajnja = null;

                while (true)
                {
                    Console.Write("Početna (x y) ili 'kraj': ");
                    string unos = Console.ReadLine()?.Trim();
                    if (unos?.ToLower() == "kraj") return;

                    string[] delovi = unos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (delovi.Length == 2 &&
                        int.TryParse(delovi[0], out int x) && x >= 0 && x < 30 &&
                        int.TryParse(delovi[1], out int y) && y >= 0 && y < 30)
                    {
                        pocetna = new Koordinata(x, y);
                        break;
                    }
                    Console.WriteLine("GREŠKA: Unesite koordinate u formatu: X Y (razdvojeno razmakom, brojevi 0–29).");
                }

                while (true)
                {
                    Console.Write("Krajnja (x y): ");
                    string unos = Console.ReadLine()?.Trim();

                    string[] delovi = unos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (delovi.Length == 2 &&
                        int.TryParse(delovi[0], out int x) && x >= 0 && x < 30 &&
                        int.TryParse(delovi[1], out int y) && y >= 0 && y < 30)
                    {
                        krajnja = new Koordinata(x, y);
                        break;
                    }
                    Console.WriteLine("GREŠKA: Unesite koordinate u formatu: X Y (razdvojeno razmakom, brojevi 0–29).");
                }

                // Kreiranje zahteva
                KlijentModel zahtev = new KlijentModel
                {
                    IDKlijenta = id,
                    pocetnaTacka = pocetna,
                    krajnjaTacka = krajnja
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
