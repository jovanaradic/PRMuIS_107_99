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
using System.Threading;

namespace TaksiVozilo
{
    internal class TaksiVozilo
    {
        static void Main(string[] args)
        {
            Console.Title = "VOZILO";
            Console.Write("ID vozila: ");
            int id;
            while (true)
            {
               
                string input = Console.ReadLine();
                if (int.TryParse(input, out id) && id >= 0)
                    break;
                Console.WriteLine("GREŠKA: ID mora biti pozitivan ceo broj. Pokušajte ponovo.");
            }
            //vozilo dobija nasumicnu pocetnu poziciju na mapi
            Random r = new Random();
            Koordinata lokacija = new Koordinata(r.Next(0, 19), r.Next(0, 19));

            Socket clientSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50000);

            try
            {
                clientSocketTCP.Connect(serverEP);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"GRESKA: nije moguce povezati se na server: {ex}");
                Console.WriteLine("\nPritisnite Enter za izlaz...");
                Console.ReadLine();
                return;
            }

            TaksiVoziloModel vozilo = new TaksiVoziloModel
            {
                Id = id,
                koordinataX = lokacija.X,
                koordinataY = lokacija.Y,
                Status = StatusVozila.Slobodno
            };

            byte[] bufferStatusVoznje = new byte[1024];

            //slanje vozila serveru - zahtev za konekciju
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, vozilo);
                bufferStatusVoznje = ms.ToArray();
                int brBajta = clientSocketTCP.SendTo(bufferStatusVoznje, 0, bufferStatusVoznje.Length, SocketFlags.None, serverEP);
            }

            Console.Clear();
            Console.WriteLine($"VOZILO {id} povezano i spremno.\n");

            while (true)
            {
                try
                {
                    byte[] bufferZadatak = new byte[1024];
                    int velicinaZadatka = clientSocketTCP.Receive(bufferZadatak);

                    ZadatakModel zadatak = null;

                    using (MemoryStream ms = new MemoryStream(bufferZadatak, 0, velicinaZadatka))
                    {
                        //prima zadatak od servera
                        BinaryFormatter bf = new BinaryFormatter();
                        zadatak = bf.Deserialize(ms) as ZadatakModel;

                    }
                    if (zadatak != null)
                    {

                        Console.WriteLine($"Nova voznja → klijent {zadatak.IDKlijenta}, {zadatak.PredjenaRazdaljina:F1} km");

                        vozilo.Status = StatusVozila.NaPutu;
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);
                        SimulirajKretanje(vozilo, zadatak.pozicijaKlijenta, clientSocketTCP, serverEP);

                        vozilo.Status = StatusVozila.UVoznji;
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);
                        SimulirajKretanje(vozilo, zadatak.zeljenaPozicija, clientSocketTCP, serverEP);

                        StatusVoznje status = new StatusVoznje
                        {
                            IdKlijenta = zadatak.IDKlijenta,
                            IdVozila = id,
                            Km = zadatak.PredjenaRazdaljina,
                            CenaVoznje = zadatak.PredjenaRazdaljina * 80
                        };
                        //salje se status voznje pri zavrsetku
                        byte[] buffer = new byte[1024];

                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            bf.Serialize(ms, status);
                            buffer = ms.ToArray();
                            clientSocketTCP.Send(buffer);
                        }

                        vozilo.Status = StatusVozila.Slobodno;
                        //kako bi se u serveru promijenilo stanje na slobodno
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);
                    }
                    else
                    {
                        Console.WriteLine("Veza sa serverom prekinuta!");
                        break;
                    }
                }

                catch (SocketException ex)
                {
                    Console.WriteLine($"Doslo je do greske tokom slanja:\n{ex}");
                }
            }

            Console.WriteLine("Vozilo zavrsava sa radom");
            Console.ReadKey();
            clientSocketTCP.Close();
        }

        private static void SimulirajKretanje(TaksiVoziloModel vozilo, Koordinata cilj, Socket socket, EndPoint serverEP)
        {
            while (vozilo.koordinataX != cilj.X || vozilo.koordinataY != cilj.Y)
            {
                if (vozilo.koordinataX < cilj.X)
                    vozilo.koordinataX++;
                else if (vozilo.koordinataX > cilj.X)
                    vozilo.koordinataX--;

                if (vozilo.koordinataY < cilj.Y)
                    vozilo.koordinataY++;
                else if (vozilo.koordinataY > cilj.Y)
                    vozilo.koordinataY--;

                PosaljiVozilo(socket, serverEP, vozilo);
                Thread.Sleep(800); // pauza da se kretanje vidi (0.8 sekunde po koraku)
            }
        }

        private static void PosaljiVozilo(Socket socket, EndPoint serverEP, TaksiVoziloModel vozilo)
        {
            try
            {
                byte[] buffer = new byte[1024];
                using (MemoryStream ms = new MemoryStream())
                {
                    B693
                        inaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, vozilo);
                    buffer = ms.ToArray();
                    socket.Send(buffer);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Doslo je do greske prilikom slanja: {ex}");
            }
        }
    }
}
