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
            //vozilo dobija nasumičnu pocetnu poziciju na mapi - unutar granica lavirinta
            Random r = new Random();
            Koordinata lokacija = new Koordinata(r.Next(0, Konfiguracija.SirinaLavirinta), r.Next(0, Konfiguracija.VisinaLavirinta));

            Socket clientSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50000);

            try
            {
                clientSocketTCP.Connect(serverEP);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GREŠKA: nije moguce povezati se na server: " + ex);
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
            Console.WriteLine("VOZILO " + id + " povezano i spremno.\n");

            while (true)
            {
                try
                {
                    byte[] bufferZadatak = new byte[16384];
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

                        Console.WriteLine("Nova voznja -> klijent " + zadatak.IDKlijenta + ", " + zadatak.PredjenaRazdaljina.ToString("F1") + " polja (ruta: " + zadatak.IzabraniAlgoritam + ")");

                        vozilo.Status = StatusVozila.NaPutu;
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);
                        PratiPutanju(vozilo, zadatak.PutanjaDoKlijenta, clientSocketTCP, serverEP);

                        vozilo.Status = StatusVozila.UVoznji;
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);
                        PratiPutanju(vozilo, zadatak.PutanjaDoOdredista, clientSocketTCP, serverEP);

                        vozilo.Status = StatusVozila.Slobodno;
                        PosaljiVozilo(clientSocketTCP, serverEP, vozilo);

                        Thread.Sleep(100);

                        StatusVoznje status = new StatusVoznje
                        {
                            IdKlijenta = zadatak.IDKlijenta,
                            IdVozila = id,
                            Km = zadatak.PredjenaRazdaljina,
                            CenaVoznje = zadatak.PredjenaRazdaljina * Konfiguracija.CenaPoPolju
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
                    }
                    else
                    {
                        Console.WriteLine("Veza sa serverom prekinuta!");
                        break;
                    }
                }

                catch (SocketException ex)
                {
                    Console.WriteLine("Doslo je do greske tokom slanja:\n" + ex);
                    break;
                }

            }

            Console.WriteLine("Vozilo zavrsava sa radom");
            Console.ReadKey();
            clientSocketTCP.Close();
        }

        // vozilo prolazi kroz listu koordinata (putanju) koju je dispecer izracunao
        private static void PratiPutanju(TaksiVoziloModel vozilo, List<Koordinata> putanja, Socket socket, EndPoint serverEP)
        {
            if (putanja == null || putanja.Count == 0)
                return;

            int pocetniIndeks = 0;
            if (putanja[0].X == vozilo.koordinataX && putanja[0].Y == vozilo.koordinataY)
                pocetniIndeks = 1;

            for (int i = pocetniIndeks; i < putanja.Count; i++)
            {
                vozilo.koordinataX = putanja[i].X;
                vozilo.koordinataY = putanja[i].Y;
                PosaljiVozilo(socket, serverEP, vozilo);
                Thread.Sleep(Konfiguracija.PauzaKretanjaMs);
            }
        }

        private static void PosaljiVozilo(Socket socket, EndPoint serverEP, TaksiVoziloModel vozilo)
        {
            try
            {
                byte[] buffer = new byte[1024];
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, vozilo);
                    buffer = ms.ToArray();
                    socket.Send(buffer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Došlo je do greske prilikom slanja: " + ex);
            }
        }
    }
}