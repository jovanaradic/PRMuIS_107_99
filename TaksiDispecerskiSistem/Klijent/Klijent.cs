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

            //KREIRANJE UDP SOCKETA
            //udp je brzi i jednostavniji a on nema stalnu vezu sa serverom pa nije bitna pouzdanost
            Socket clientSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50001);

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0); //adresa za primanje odgovora

            int maxX = Konfiguracija.SirinaLavirinta - 1;
            int maxY = Konfiguracija.VisinaLavirinta - 1;
            string porukaOpsega = "GREŠKA: Unesite koordinate u formatu: X Y (razdvojeno razmakom, brojevi 0-" + maxX + " za X, 0-" + maxY + " za Y).";

            while (true)
            {
                Console.Clear();
                Console.WriteLine("============================================");
                Console.WriteLine("      KLIJENT (" + id + ") - Novi zahtev");
                Console.WriteLine("============================================");
                Koordinata pocetna = null;
                Koordinata krajnja = null;

                while (true)
                {
                    while (true)
                    {
                        Console.Write("Početna (x y) ili 'kraj': ");
                        string unos = Console.ReadLine()?.Trim();
                        if (unos?.ToLower() == "kraj") return;

                        string[] delovi = unos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (delovi.Length == 2 &&
                            int.TryParse(delovi[0], out int x) && x >= 0 && x <= maxX &&
                            int.TryParse(delovi[1], out int y) && y >= 0 && y <= maxY)
                        {
                            pocetna = new Koordinata(x, y);
                            break;
                        }
                        Console.WriteLine(porukaOpsega);
                    }

                    while (true)
                    {
                        Console.Write("Krajnja (x y): ");
                        string unos = Console.ReadLine()?.Trim();

                        string[] delovi = unos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (delovi.Length == 2 &&
                            int.TryParse(delovi[0], out int x) && x >= 0 && x <= maxX &&
                            int.TryParse(delovi[1], out int y) && y >= 0 && y <= maxY)
                        {
                            krajnja = new Koordinata(x, y);
                            break;
                        }
                        Console.WriteLine(porukaOpsega);
                    }

                    if (pocetna.X == krajnja.X && pocetna.Y == krajnja.Y)
                    {
                        Console.WriteLine("GREŠKA: Početna i krajnja tačka ne mogu biti iste.");
                        Console.WriteLine("Pritisnite Enter za novi unos...");
                        Console.ReadLine();
                        continue;
                    }
                    else
                    {
                        break;
                    }

                }

                // Kreiranje zahteva
                KlijentModel zahtev = new KlijentModel
                {
                    IDKlijenta = id,
                    pocetnaTacka = pocetna,
                    krajnjaTacka = krajnja
                };

                byte[] bufferZahtev = new byte[1024]; //za slanje podataka

                try
                {
                    //slanje zahteva serveru
                    //salje bajtove serveru preko udp
                    using (MemoryStream ms = new MemoryStream())
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        bf.Serialize(ms, zahtev);
                        bufferZahtev = ms.ToArray();
                        int brBajta = clientSocketUDP.SendTo(bufferZahtev, 0, bufferZahtev.Length, SocketFlags.None, serverEP);
                    }

                    byte[] bufferOdgovorServera = new byte[1024];
                    byte[] bufferUpdateServera = new byte[1024];

                    //čekanje odgovora servera

                    if (clientSocketUDP.Poll(4000 * 1000, SelectMode.SelectRead)) //čeka do 4 sekunde da server odgovori
                    {
                        int brBajtaOdg = clientSocketUDP.ReceiveFrom(bufferOdgovorServera, ref posiljaocEP);

                        string odgovorServera = Encoding.UTF8.GetString(bufferOdgovorServera, 0, brBajtaOdg);
                        Console.WriteLine("\n---------------------------------------------------------------------");
                        Console.WriteLine("Odgovor servera:");
                        Console.WriteLine("   " + odgovorServera);
                        Console.WriteLine("---------------------------------------------------------------------");

                        if (!odgovorServera.Contains("Nema") && !odgovorServera.Contains("odbijen"))
                        {
                            Console.WriteLine("Update servera:");
                            //klijent ocekuje update od servera
                            while (true)
                            {
                                int brBajtaUpdate = clientSocketUDP.ReceiveFrom(bufferUpdateServera, ref posiljaocEP);
                                string updateServera = Encoding.UTF8.GetString(bufferUpdateServera, 0, brBajtaUpdate);

                                if (updateServera.Contains("Stigli"))
                                {
                                    Console.WriteLine("   " + updateServera);
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine("   " + updateServera);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n---------------------------------------------------------------------");
                        Console.WriteLine(" Server ne odgovara (nema dostupnih vozila?).");
                        Console.WriteLine("---------------------------------------------------------------------");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Greška prilikom slanja/primanja: " + ex.Message);
                }


                Console.WriteLine("\nPritisni Enter za novi zahtev ili 'kraj' da izađeš...");
                if (Console.ReadLine()?.Trim().ToLower() == "kraj") break;

            }

            Console.WriteLine("Klijent završava sa radom!");
            clientSocketUDP.Close(); // Zatvaramo soket na kraju rada
            Console.ReadKey();
        }
    }
}