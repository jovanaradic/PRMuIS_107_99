using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ZajednickeKlase;
using ZajednickeKlase.Modeli;

namespace KlijentWPF
{
    public partial class MainWindow : Window
    {
        private Socket socketUDP;
        private IPEndPoint serverEP;
        private int idKlijenta;

        private Button[,] celije;
        private Koordinata odabranaPocetna;
        private Koordinata odabranaKrajnja;

        private ObservableCollection<string> statusPoruke = new ObservableCollection<string>();
        private bool cekaOdgovor = false;
        private bool primljenPrviOdgovor = false;
        //imitacija poll
        private DispatcherTimer timerCekanja;

        public MainWindow()
        {
            InitializeComponent();
            ListaStatusa.ItemsSource = statusPoruke;
            NapraviMrezu();
        }

        //klikabilna mreza
        private void NapraviMrezu()
        {
            int sirina = Konfiguracija.SirinaLavirinta;
            int visina = Konfiguracija.VisinaLavirinta;

            MrezaCelija.Columns = sirina;
            MrezaCelija.Rows = visina;
            celije = new Button[sirina, visina];

            for (int y = 0; y < visina; y++)
            {
                for (int x = 0; x < sirina; x++)
                {
                    var dugme = new Button
                    {
                        Tag = new Koordinata(x, y),
                        Style = (Style)FindResource("CelijaDugme"),
                        FontSize = 9
                    };
                    dugme.Click += Celija_Click;
                    celije[x, y] = dugme;
                    MrezaCelija.Children.Add(dugme);
                }
            }
        }

        private void BtnPoveziSe_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtIdKlijenta.Text, out idKlijenta) || idKlijenta < 0)
            {
                MessageBox.Show("ID klijenta mora biti pozitivan ceo broj.", "Greska", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socketUDP.Bind(new IPEndPoint(IPAddress.Any, 0));
                serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50001);

                TxtStatusKonekcije.Text = "Prijavljen";
                TxtStatusKonekcije.Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0x6C, 0x3B));
                ZnackaKonekcije.Background = new SolidColorBrush(Color.FromRgb(0xD9, 0xF3, 0xE3));
                BtnPoveziSe.IsEnabled = false;
                TxtIdKlijenta.IsEnabled = false;

                var nitZaPrijem = new Thread(SlusajOdgovoreServera);
                nitZaPrijem.IsBackground = true;
                nitZaPrijem.Start();

                DodajPoruku("Klijent " + idKlijenta + " prijavljen i spreman.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Greska prilikom povezivanja: " + ex.Message, "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Celija_Click(object sender, RoutedEventArgs e)
        {
            var dugme = (Button)sender;
            var koordinata = (Koordinata)dugme.Tag;

            if (odabranaPocetna == null)
            {
                odabranaPocetna = koordinata;
                dugme.Background = new SolidColorBrush(Color.FromRgb(0x8E, 0xE3, 0xAF));
                TxtPolazak.Text = "(" + koordinata.X + ", " + koordinata.Y + ")";
            }
            else if (odabranaKrajnja == null)
            {
                if (koordinata.X == odabranaPocetna.X && koordinata.Y == odabranaPocetna.Y)
                    return; // ne dozvoljavamo istu tacku za polazak i odrediste

                odabranaKrajnja = koordinata;
                dugme.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0x9A, 0x9A));
                TxtOdrediste.Text = "(" + koordinata.X + ", " + koordinata.Y + ")";

                BtnPosaljiZahtev.IsEnabled = !cekaOdgovor;
            }
            // ako su vec oba izabrana, klik na novu celiju se ignorise dok se ne resetuje
        }

        private void BtnResetujOdabir_Click(object sender, RoutedEventArgs e)
        {
            ResetujOdabir();
        }

        private void ResetujOdabir()
        {
            foreach (var dugme in celije)
                dugme.Background = Brushes.White;

            odabranaPocetna = null;
            odabranaKrajnja = null;
            TxtPolazak.Text = "nije izabrano";
            TxtOdrediste.Text = "nije izabrano";
            BtnPosaljiZahtev.IsEnabled = false;
        }

        private void BtnPosaljiZahtev_Click(object sender, RoutedEventArgs e)
        {
            if (odabranaPocetna == null || odabranaKrajnja == null || cekaOdgovor)
                return;

            var zahtev = new KlijentModel
            {
                IDKlijenta = idKlijenta,
                pocetnaTacka = odabranaPocetna,
                krajnjaTacka = odabranaKrajnja
            };

            try
            {
                using (var ms = new MemoryStream())
                {
                    var bf = new BinaryFormatter();
                    bf.Serialize(ms, zahtev);
                    var bafer = ms.ToArray();
                    socketUDP.SendTo(bafer, 0, bafer.Length, SocketFlags.None, serverEP);
                }

                statusPoruke.Clear();
                DodajPoruku("Poslat zahtev: (" + odabranaPocetna.X + "," + odabranaPocetna.Y + ") -> (" + odabranaKrajnja.X + "," + odabranaKrajnja.Y + ")");
                cekaOdgovor = true;
                primljenPrviOdgovor = false;
                BtnPosaljiZahtev.IsEnabled = false;

                timerCekanja = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                timerCekanja.Tick += TimerCekanja_Tick;
                timerCekanja.Start();
            }
            catch (Exception ex)
            {
                DodajPoruku("Greska prilikom slanja: " + ex.Message);
            }
        }

        private void SlusajOdgovoreServera()
        {
            var bafer = new byte[1024];

            while (true)
            {
                EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    int brojBajtova = socketUDP.ReceiveFrom(bafer, ref posiljaocEP);
                    string poruka = Encoding.UTF8.GetString(bafer, 0, brojBajtova);

                    Dispatcher.Invoke(() =>
                    {
                        if (!primljenPrviOdgovor)
                        {
                            primljenPrviOdgovor = true;
                            if (timerCekanja != null)
                                timerCekanja.Stop();
                        }

                        DodajPoruku(poruka);

                        bool zavrsenoIliOdbijeno = poruka.Contains("Stigli")
                            || poruka.Contains("Nema")
                            || poruka.Contains("odbijen");

                        if (zavrsenoIliOdbijeno)
                        {
                            cekaOdgovor = false;
                            ResetujOdabir();
                        }
                    });
                }
                catch (Exception ex)
                {
                    try
                    {
                        Dispatcher.Invoke(() => DodajPoruku("GRESKA U PRIJEMU (" + ex.GetType().Name + "): " + ex.Message));
                    }
                    catch { /* prozor je vec zatvoren, nema gde da se ispise */ }
                    break;
                }
            }
        }

        private void TimerCekanja_Tick(object sender, EventArgs e)
        {
            timerCekanja.Stop();

            if (!primljenPrviOdgovor)
            {
                DodajPoruku("Server ne odgovara (nema dostupnih vozila?).");
                cekaOdgovor = false;
                ResetujOdabir();
            }
        }

        private void DodajPoruku(string poruka)
        {
            statusPoruke.Add(poruka);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (socketUDP != null)
                socketUDP.Close();
        }
    }
}