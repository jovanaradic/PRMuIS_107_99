using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using ZajednickeKlase;
using ZajednickeKlase.AlgoritmiPretrage;
using ZajednickeKlase.Enumeracije;
using ZajednickeKlase.Evidencija;
using ZajednickeKlase.Lavirint;
using ZajednickeKlase.Modeli;

namespace ServerWPF
{
    // Pomocna klasa samo za prikaz u DataGrid-u (spaja X/Y u citljivu "Lokaciju"
    // i formatira brojeve) - ZadatakModel se prikazuje direktno, njemu ovo ne treba.
    public class VoziloRed
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public string Lokacija { get; set; }
        public string Km { get; set; }
        public string Zarada { get; set; }
    }

    // Mapira tekst statusa (vozila ili zadatka) u boju za "pill" oznaku u tabelama.
    public class StatusBojaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? "";
            switch (status)
            {
                case "NaPutu":
                    return new SolidColorBrush(Color.FromRgb(0xE0, 0x8E, 0x2B));
                case "UVoznji":
                    return new SolidColorBrush(Color.FromRgb(0x1E, 0xA1, 0x5A));
                case "Aktivan":
                    return new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xED));
                case "Zavrsen":
                    return new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0xA0));
                default: // Slobodno i eventualno nepoznat status
                    return new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xED));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class MainWindow : Window
    {
        private const int VelicinaCelije = 40;

        private Lavirint lavirint;
        private ObservableCollection<string> logPoruke = new ObservableCollection<string>();

        // Markeri na canvasu - drzimo ih po ID-u da bismo ih azurirali umesto ponovnog crtanja
        private Dictionary<int, Border> vozilaMarkeri = new Dictionary<int, Border>();
        private Dictionary<int, Border> klijentMarkeri = new Dictionary<int, Border>();

        public MainWindow()
        {
            InitializeComponent();
            ListaLoga.ItemsSource = logPoruke;

            lavirint = GeneratorLavirinta.Generisi(
                Konfiguracija.SirinaLavirinta,
                Konfiguracija.VisinaLavirinta,
                Konfiguracija.VerovatnocaDodatnihProlaza,
                Konfiguracija.SemeLavirinta);

            NacrtajMapu();

            var nitServera = new Thread(RadiServer);
            nitServera.IsBackground = true;
            nitServera.Start();
        }

        // Crta zidove lavirinta JEDNOM, na osnovu stvarnih podataka koje server jedini ima
        // (za razliku od Klijenta, koji ne zna raspored zidova).
        private void NacrtajMapu()
        {
            int mrezaSirina = 2 * lavirint.Sirina + 1;
            int mrezaVisina = 2 * lavirint.Visina + 1;

            MapaLavirinta.Width = lavirint.Sirina * VelicinaCelije;
            MapaLavirinta.Height = lavirint.Visina * VelicinaCelije;

            var olovka = new SolidColorBrush(Color.FromRgb(0x33, 0x38, 0x4A));

            // horizontalni zidovi
            for (int py = 0; py <= lavirint.Visina; py++)
            {
                for (int px = 0; px < lavirint.Sirina; px++)
                {
                    if (lavirint.Zidovi[2 * px + 1, 2 * py])
                    {
                        var linija = new Line
                        {
                            X1 = px * VelicinaCelije,
                            Y1 = py * VelicinaCelije,
                            X2 = (px + 1) * VelicinaCelije,
                            Y2 = py * VelicinaCelije,
                            Stroke = olovka,
                            StrokeThickness = 2
                        };
                        MapaLavirinta.Children.Add(linija);
                    }
                }
            }

            // vertikalni zidovi
            for (int py = 0; py < lavirint.Visina; py++)
            {
                for (int px = 0; px <= lavirint.Sirina; px++)
                {
                    if (lavirint.Zidovi[2 * px, 2 * py + 1])
                    {
                        var linija = new Line
                        {
                            X1 = px * VelicinaCelije,
                            Y1 = py * VelicinaCelije,
                            X2 = px * VelicinaCelije,
                            Y2 = (py + 1) * VelicinaCelije,
                            Stroke = olovka,
                            StrokeThickness = 2
                        };
                        MapaLavirinta.Children.Add(linija);
                    }
                }
            }
        }

        private Border NapraviMarker(string tekst, Color boja)
        {
            var marker = new Border
            {
                Width = VelicinaCelije - 4,
                Height = VelicinaCelije - 4,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(boja),
                Child = new TextBlock
                {
                    Text = tekst,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            return marker;
        }

        private void PostaviNaCeliju(Border marker, int x, int y)
        {
            Canvas.SetLeft(marker, x * VelicinaCelije + 2);
            Canvas.SetTop(marker, y * VelicinaCelije + 2);
        }

        // Osvezava tabele, markere na mapi i naslov - poziva se SAMO na UI niti
        // (Dispatcher.Invoke iz pozadinske niti, ili direktno ako smo vec na UI niti).
        private void OsveziPrikaz(Dictionary<int, TaksiVoziloModel> vozila, Dictionary<int, ZadatakModel> zadaci)
        {
            TabelaVozila.ItemsSource = vozila.Values.OrderBy(v => v.Id).Select(v => new VoziloRed
            {
                Id = v.Id,
                Status = v.Status.ToString(),
                Lokacija = "(" + v.koordinataX + ", " + v.koordinataY + ")",
                Km = v.Km.ToString("0.0"),
                Zarada = v.Zarada.ToString("0.00") + " RSD"
            }).ToList();

            TabelaZadataka.ItemsSource = zadaci.Values.OrderBy(z => z.ID).ToList();

            // markeri vozila - dodaj/azuriraj/ukloni
            var ziveIdVozila = new HashSet<int>(vozila.Keys);
            foreach (var stariId in vozilaMarkeri.Keys.Where(id => !ziveIdVozila.Contains(id)).ToList())
            {
                MapaLavirinta.Children.Remove(vozilaMarkeri[stariId]);
                vozilaMarkeri.Remove(stariId);
            }

            foreach (var v in vozila.Values)
            {
                Color boja;
                switch (v.Status)
                {
                    case StatusVozila.NaPutu: boja = Color.FromRgb(0xE0, 0x8E, 0x2B); break;
                    case StatusVozila.UVoznji: boja = Color.FromRgb(0x1E, 0xA1, 0x5A); break;
                    default: boja = Color.FromRgb(0x2F, 0x6F, 0xED); break;
                }

                if (!vozilaMarkeri.ContainsKey(v.Id))
                {
                    var marker = NapraviMarker("V" + v.Id, boja);
                    vozilaMarkeri[v.Id] = marker;
                    MapaLavirinta.Children.Add(marker);
                }
                else
                {
                    vozilaMarkeri[v.Id].Background = new SolidColorBrush(boja);
                }

                if (lavirint.UGranicama(v.koordinataX, v.koordinataY))
                    PostaviNaCeliju(vozilaMarkeri[v.Id], v.koordinataX, v.koordinataY);
            }

            // markeri klijenata koji cekaju (samo dok vozilo nije "UVoznji" - vec pokupilo)
            var aktivniZadaciKojiCekaju = zadaci.Values
                .Where(z => z.StatusZadatka == StatusZadatka.Aktivan
                    && vozila.ContainsKey(z.IDVozila)
                    && vozila[z.IDVozila].Status != StatusVozila.UVoznji)
                .ToList();

            var ziveIdZadataka = new HashSet<int>(aktivniZadaciKojiCekaju.Select(z => z.ID));
            foreach (var stariId in klijentMarkeri.Keys.Where(id => !ziveIdZadataka.Contains(id)).ToList())
            {
                MapaLavirinta.Children.Remove(klijentMarkeri[stariId]);
                klijentMarkeri.Remove(stariId);
            }

            foreach (var z in aktivniZadaciKojiCekaju)
            {
                if (!klijentMarkeri.ContainsKey(z.ID))
                {
                    var marker = NapraviMarker("K" + z.IDKlijenta, Color.FromRgb(0xD6, 0x45, 0x45));
                    klijentMarkeri[z.ID] = marker;
                    MapaLavirinta.Children.Add(marker);
                }

                if (lavirint.UGranicama(z.pozicijaKlijenta.X, z.pozicijaKlijenta.Y))
                    PostaviNaCeliju(klijentMarkeri[z.ID], z.pozicijaKlijenta.X, z.pozicijaKlijenta.Y);
            }
        }

        private void DodajLog(string poruka)
        {
            logPoruke.Insert(0, poruka);
            if (logPoruke.Count > 300)
                logPoruke.RemoveAt(logPoruke.Count - 1);
        }

        // --- Mrezna logika (pozadinska nit) ---
        private void RadiServer()
        {
            Socket serverSocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEPTCP = new IPEndPoint(IPAddress.Any, 50000);
            serverSocketTCP.Bind(serverEPTCP);
            serverSocketTCP.Blocking = false;
            serverSocketTCP.Listen(10);

            Socket serverSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEPUDP = new IPEndPoint(IPAddress.Any, 50001);
            serverSocketUDP.Bind(serverEPUDP);
            serverSocketUDP.Blocking = false;

            byte[] bufferVozilo = new byte[16384];
            byte[] bufferKlijent = new byte[1024];

            var aktivnaVozila = new Dictionary<int, TaksiVoziloModel>();
            var socketPoIdVozila = new Dictionary<int, Socket>();
            var zadaci = new Dictionary<int, ZadatakModel>();
            int idZadatkaBrojac = 0;
            var brojacKorakaPoZadatku = new Dictionary<int, int>();
            var EPPoIDKlijenta = new Dictionary<int, EndPoint>();
            var VoziloKlijentID = new Dictionary<int, int>();

            var socketsVozila = new List<Socket>();

            try
            {
                while (true)
                {
                    var checkRead = new List<Socket> { serverSocketTCP, serverSocketUDP };
                    checkRead.AddRange(socketsVozila);

                    Socket.Select(checkRead, null, null, 1000);

                    foreach (Socket socket in checkRead)
                    {
                        if (socket == serverSocketTCP)
                        {
                            try
                            {
                                Socket vozilo = serverSocketTCP.Accept();
                                vozilo.Blocking = false;
                                socketsVozila.Add(vozilo);
                            }
                            catch (SocketException ex)
                            {
                                Dispatcher.Invoke(() => DodajLog("Greška prilikom prihvatanja konekcije vozila: " + ex.Message));
                            }
                        }
                        else if (socket == serverSocketUDP)
                        {
                            try
                            {
                                EndPoint klijentEPUDP = new IPEndPoint(IPAddress.Any, 0);
                                KlijentModel zahtev1;
                                int primljenihBajtovaKlijent1 = serverSocketUDP.ReceiveFrom(bufferKlijent, ref klijentEPUDP);
                                using (var ms = new MemoryStream(bufferKlijent, 0, primljenihBajtovaKlijent1))
                                {
                                    var bf = new BinaryFormatter();
                                    zahtev1 = bf.Deserialize(ms) as KlijentModel;
                                }

                                bool klijentImaZadatak = zadaci.Values.Any(z => z.IDKlijenta == zahtev1.IDKlijenta && z.StatusZadatka == StatusZadatka.Aktivan);
                                if (klijentImaZadatak)
                                {
                                    var bp = Encoding.UTF8.GetBytes("Zahtev odbijen: Klijent sa ID " + zahtev1.IDKlijenta + " vec ima aktivnu voznju.");
                                    serverSocketUDP.SendTo(bp, klijentEPUDP);
                                    continue;
                                }

                                if (!lavirint.UGranicama(zahtev1.pocetnaTacka.X, zahtev1.pocetnaTacka.Y) ||
                                    !lavirint.UGranicama(zahtev1.krajnjaTacka.X, zahtev1.krajnjaTacka.Y))
                                {
                                    var bp = Encoding.UTF8.GetBytes("Zahtev odbijen: koordinate van granica lavirinta.");
                                    serverSocketUDP.SendTo(bp, klijentEPUDP);
                                    continue;
                                }

                                TaksiVoziloModel najbolji = NadjiNajblizeVozilo(lavirint, aktivnaVozila, zahtev1.pocetnaTacka);
                                if (najbolji == null)
                                {
                                    var bp = Encoding.UTF8.GetBytes("Nema dostupnih vozila u ovom trenutku");
                                    serverSocketUDP.SendTo(bp, klijentEPUDP);
                                    continue;
                                }

                                Koordinata pozicijaVozila = new Koordinata(najbolji.koordinataX, najbolji.koordinataY);

                                List<RezultatPretrage> rezultatiDoKlijenta;
                                RezultatPretrage optimalanDoKlijenta = PronalazacPuta.NadjiOptimalnuPutanju(lavirint, pozicijaVozila, zahtev1.pocetnaTacka, out rezultatiDoKlijenta);

                                List<RezultatPretrage> rezultatiDoOdredista;
                                RezultatPretrage optimalanDoOdredista = PronalazacPuta.NadjiOptimalnuPutanju(lavirint, zahtev1.pocetnaTacka, zahtev1.krajnjaTacka, out rezultatiDoOdredista);

                                if (optimalanDoKlijenta == null || optimalanDoOdredista == null)
                                {
                                    var bp = Encoding.UTF8.GetBytes("Zahtev odbijen: nije moguce pronaci putanju kroz lavirint.");
                                    serverSocketUDP.SendTo(bp, klijentEPUDP);
                                    continue;
                                }

                                int brojAktivnihZadatakaSad = zadaci.Values.Count(z => z.StatusZadatka == StatusZadatka.Aktivan);
                                EvidencijaPodataka.SacuvajPoredjenjeAlgoritama(idZadatkaBrojac, "Vozilo->Klijent", rezultatiDoKlijenta, optimalanDoKlijenta.Algoritam,
                                    Konfiguracija.SirinaLavirinta, Konfiguracija.VisinaLavirinta, aktivnaVozila.Count, brojAktivnihZadatakaSad);
                                EvidencijaPodataka.SacuvajPoredjenjeAlgoritama(idZadatkaBrojac, "Klijent->Odrediste", rezultatiDoOdredista, optimalanDoOdredista.Algoritam,
                                    Konfiguracija.SirinaLavirinta, Konfiguracija.VisinaLavirinta, aktivnaVozila.Count, brojAktivnihZadatakaSad);

                                var zadatak = new ZadatakModel
                                {
                                    ID = idZadatkaBrojac,
                                    pozicijaKlijenta = zahtev1.pocetnaTacka,
                                    zeljenaPozicija = zahtev1.krajnjaTacka,
                                    IDKlijenta = zahtev1.IDKlijenta,
                                    IDVozila = najbolji.Id,
                                    PredjenaRazdaljina = optimalanDoOdredista.DuzinaPuta,
                                    PutanjaDoKlijenta = optimalanDoKlijenta.Putanja,
                                    PutanjaDoOdredista = optimalanDoOdredista.Putanja,
                                    IzabraniAlgoritam = optimalanDoKlijenta.Algoritam + " / " + optimalanDoOdredista.Algoritam
                                };

                                zadaci[idZadatkaBrojac] = zadatak;
                                zadatak.StatusZadatka = StatusZadatka.Aktivan;

                                using (var ms = new MemoryStream())
                                {
                                    var bf = new BinaryFormatter();
                                    bf.Serialize(ms, zadatak);
                                    var bufferZadatak = ms.ToArray();
                                    try
                                    {
                                        Socket voziloSocket = socketPoIdVozila[najbolji.Id];
                                        var voziloEPTCP = voziloSocket.RemoteEndPoint as IPEndPoint;
                                        voziloSocket.SendTo(bufferZadatak, 0, bufferZadatak.Length, SocketFlags.None, voziloEPTCP);
                                    }
                                    catch (Exception ex)
                                    {
                                        Dispatcher.Invoke(() => DodajLog("Greška pri slanju zadatka vozilu: " + ex.Message));
                                    }
                                }

                                double vreme = optimalanDoKlijenta.DuzinaPuta * (Konfiguracija.PauzaKretanjaMs / 1000.0);
                                var bufferOdg = Encoding.UTF8.GetBytes("Vozilo " + najbolji.Id + " dolazi za priblizno " + vreme.ToString("0.0") + " sekundi!");
                                serverSocketUDP.SendTo(bufferOdg, klijentEPUDP);

                                brojacKorakaPoZadatku[najbolji.Id] = 0;
                                EPPoIDKlijenta[zahtev1.IDKlijenta] = klijentEPUDP;
                                VoziloKlijentID[najbolji.Id] = zahtev1.IDKlijenta;
                                idZadatkaBrojac++;

                                Dispatcher.Invoke(() =>
                                {
                                    DodajLog("Zadatak #" + zadatak.ID + ": vozilo " + najbolji.Id + " -> klijent " + zadatak.IDKlijenta + " (" + zadatak.IzabraniAlgoritam + ")");
                                    OsveziPrikaz(aktivnaVozila, zadaci);
                                });
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => DodajLog("Greška prilikom obrade zahteva klijenta: " + ex.Message));
                            }
                        }

                        if (socketsVozila.Contains(socket))
                        {
                            try
                            {
                                int primljeniBajtoviVozilo = socket.Receive(bufferVozilo);
                                if (primljeniBajtoviVozilo == 0)
                                {
                                    var parVozila = socketPoIdVozila.FirstOrDefault(par => par.Value == socket);
                                    bool pronadjen = !parVozila.Equals(default(KeyValuePair<int, Socket>));
                                    int idVozilaZaBrisanje = pronadjen ? parVozila.Key : -1;

                                    if (pronadjen)
                                    {
                                        aktivnaVozila.Remove(idVozilaZaBrisanje);
                                        socketPoIdVozila.Remove(idVozilaZaBrisanje);
                                    }
                                    socketsVozila.Remove(socket);
                                    socket.Close();

                                    Dispatcher.Invoke(() => { DodajLog("Vozilo " + idVozilaZaBrisanje + " se diskonektovalo."); OsveziPrikaz(aktivnaVozila, zadaci); });
                                    continue;
                                }

                                using (var ms = new MemoryStream(bufferVozilo, 0, primljeniBajtoviVozilo))
                                {
                                    var bf = new BinaryFormatter();
                                    object obj = bf.Deserialize(ms);

                                    if (obj is TaksiVoziloModel)
                                    {
                                        var vozilo = (TaksiVoziloModel)obj;
                                        if (aktivnaVozila.ContainsKey(vozilo.Id))
                                        {
                                            var postojeci = aktivnaVozila[vozilo.Id];
                                            postojeci.koordinataX = vozilo.koordinataX;
                                            postojeci.koordinataY = vozilo.koordinataY;
                                            postojeci.Status = vozilo.Status;

                                            var zadatak = zadaci.Values.FirstOrDefault(z => z.IDVozila == postojeci.Id && z.StatusZadatka == StatusZadatka.Aktivan);
                                            if (zadatak != null)
                                            {
                                                if (postojeci.Status == StatusVozila.NaPutu)
                                                {
                                                    brojacKorakaPoZadatku[postojeci.Id]++;
                                                    int indeksTrenutneCelije = zadatak.PutanjaDoKlijenta.FindIndex(k => k.X == postojeci.koordinataX && k.Y == postojeci.koordinataY);
                                                    int preostaliKoraci = indeksTrenutneCelije >= 0 ? zadatak.PutanjaDoKlijenta.Count - 1 - indeksTrenutneCelije : 0;

                                                    if (brojacKorakaPoZadatku[postojeci.Id] % 4 == 0 && preostaliKoraci > 2)
                                                    {
                                                        int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                        EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];
                                                        double vrijeme = preostaliKoraci * (Konfiguracija.PauzaKretanjaMs / 1000.0);
                                                        var bp = Encoding.UTF8.GetBytes("Vozilo se priblizava... Dolazi na odrediste za " + vrijeme.ToString("0.0") + " sekundi!");
                                                        serverSocketUDP.SendTo(bp, klijentEPUDP);
                                                    }
                                                }

                                                if (zadatak.pozicijaKlijenta.X == postojeci.koordinataX && zadatak.pozicijaKlijenta.Y == postojeci.koordinataY && postojeci.Status != StatusVozila.UVoznji)
                                                {
                                                    int idKlijenta = VoziloKlijentID[postojeci.Id];
                                                    EndPoint klijentEPUDP = EPPoIDKlijenta[idKlijenta];
                                                    var bp = Encoding.UTF8.GetBytes("Vozilo se trenutno nalazi na vasoj poziciji!");
                                                    serverSocketUDP.SendTo(bp, klijentEPUDP);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            aktivnaVozila[vozilo.Id] = vozilo;
                                            socketPoIdVozila[vozilo.Id] = socket;
                                            Dispatcher.Invoke(() => DodajLog("Vozilo " + vozilo.Id + " povezano."));
                                        }

                                        Dispatcher.Invoke(() => OsveziPrikaz(aktivnaVozila, zadaci));
                                    }
                                    else if (obj is StatusVoznje)
                                    {
                                        var status = (StatusVoznje)obj;
                                        if (aktivnaVozila.ContainsKey(status.IdVozila))
                                        {
                                            var v = aktivnaVozila[status.IdVozila];
                                            v.Km += status.Km;
                                            v.Zarada += status.CenaVoznje;
                                            v.BrojMusterija++;

                                            string algoritamZadatka = null;
                                            int idZadatkaZavrsenog = -1;
                                            foreach (var z in zadaci.Values)
                                            {
                                                if (z.IDKlijenta == status.IdKlijenta && z.IDVozila == status.IdVozila && z.StatusZadatka == StatusZadatka.Aktivan)
                                                {
                                                    z.StatusZadatka = StatusZadatka.Zavrsen;
                                                    algoritamZadatka = z.IzabraniAlgoritam;
                                                    idZadatkaZavrsenog = z.ID;
                                                    break;
                                                }
                                            }

                                            EvidencijaPodataka.SacuvajZavrsenuVoznju(new EvidencijaVoznje
                                            {
                                                Vreme = DateTime.Now,
                                                IdZadatka = idZadatkaZavrsenog,
                                                IdKlijenta = status.IdKlijenta,
                                                IdVozila = status.IdVozila,
                                                Algoritam = algoritamZadatka,
                                                PredjenaRazdaljina = status.Km,
                                                CenaVoznje = status.CenaVoznje
                                            });

                                            EndPoint klijentEPUDP = EPPoIDKlijenta[status.IdKlijenta];
                                            var bp = Encoding.UTF8.GetBytes("Stigli ste na odrediste! Voznja je placena " + status.CenaVoznje + " RSD!");
                                            serverSocketUDP.SendTo(bp, klijentEPUDP);

                                            Dispatcher.Invoke(() =>
                                            {
                                                DodajLog("Zadatak #" + idZadatkaZavrsenog + " završen - naplaćeno " + status.CenaVoznje.ToString("0.00") + " RSD.");
                                                OsveziPrikaz(aktivnaVozila, zadaci);
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                var parVozila = socketPoIdVozila.FirstOrDefault(par => par.Value == socket);
                                bool pronadjen = !parVozila.Equals(default(KeyValuePair<int, Socket>));
                                int idZaBrisanje = pronadjen ? parVozila.Key : -1;

                                if (pronadjen)
                                {
                                    socketPoIdVozila.Remove(idZaBrisanje);
                                    aktivnaVozila.Remove(idZaBrisanje);
                                }
                                socketsVozila.Remove(socket);
                                socket.Close();

                                Dispatcher.Invoke(() =>
                                {
                                    DodajLog("Vozilo se isključilo (greška: " + ex.Message + ")");
                                    OsveziPrikaz(aktivnaVozila, zadaci);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DodajLog("Server je prestao sa radom: " + ex.Message);
                    TxtStatusServera.Text = "Zaustavljen";
                });
            }
        }

        private TaksiVoziloModel NadjiNajblizeVozilo(Lavirint lav, Dictionary<int, TaksiVoziloModel> vozila, Koordinata klijent)
        {
            TaksiVoziloModel najblizi = null;
            int minDuzinaPuta = int.MaxValue;

            foreach (var vozilo in vozila.Values)
            {
                if (vozilo.Status == StatusVozila.Slobodno)
                {
                    var pozicijaVozila = new Koordinata(vozilo.koordinataX, vozilo.koordinataY);
                    var rezultat = BFS.Pretrazi(lav, pozicijaVozila, klijent);

                    if (rezultat.Pronadjeno && rezultat.DuzinaPuta < minDuzinaPuta)
                    {
                        minDuzinaPuta = rezultat.DuzinaPuta;
                        najblizi = vozilo;
                    }
                }
            }

            return najblizi;
        }
    }
}