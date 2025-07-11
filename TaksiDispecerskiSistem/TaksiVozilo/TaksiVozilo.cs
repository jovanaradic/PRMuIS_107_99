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

namespace TaksiVozilo
{
    internal class TaksiVozilo
    {
        static void Main(string[] args)
        {
            Console.Title = "VOZILO";
            Console.Write("ID vozila: ");
            int id = int.Parse(Console.ReadLine() ?? "0");

            Random r = new Random();
            Koordinata lokacija = new Koordinata(r.Next(0, 20), r.Next(0, 20));

            Socket clientSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50000);
            clientSocketTCP.Connect(serverEP);

            TaksiVoziloModel vozilo = new TaksiVoziloModel
            {
                koordinataX = lokacija.X,
                koordinataY = lokacija.Y,
                Status = StatusVozila.Slobodno
            };

            byte[] bufferStatusVoznje = new byte[1024];

            //slanje statusa voznje serveru
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
                        BinaryFormatter bf = new BinaryFormatter();
                        zadatak = bf.Deserialize(ms) as ZadatakModel;

                    }
                    if (zadatak != null)
                    {

                        Console.WriteLine($"Nova voznja → klijent {zadatak.IDKlijenta}, {zadatak.PredjenaRazdaljina:F1} km");
                        vozilo.Status = StatusVozila.NaPutu;

                        // SIMULACIJA KRETANJA -> u simulaciji saljem vozilo

                        vozilo.Status = StatusVozila.UVoznji;

                        // SIMULACIJA KRETANJA


                        StatusVoznje status = new StatusVoznje
                        {
                            IdKlijenta = zadatak.IDKlijenta,
                            IdVozila = id,
                            Km = zadatak.PredjenaRazdaljina,
                            CenaVoznje = zadatak.PredjenaRazdaljina * 0.8
                        };


                        //saljem status StatusVoznje -> ridefinisher

                        vozilo.Status = StatusVozila.Slobodno;

                        //saljem vozilo
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

            Console.WriteLine("Klijent zavrsava sa radom");
            Console.ReadKey();
            clientSocketTCP.Close();

        }
    }
}
