using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ZajednickeKlase;
using ZajednickeKlase.Enumeracije;
using ZajednickeKlase.Modeli;

namespace TaksiVoziloWPF
{
    public partial class MainWindow : Window
    {
        private Socket socketTCP;
        private IPEndPoint serverEP;
        private TaksiVoziloModel vozilo;

        private ObservableCollection<string> logKretanja = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            ListaKretanja.ItemsSource = logKretanja;
        }

        private void BtnPoveziSe_Click(object sender, RoutedEventArgs e)
        {
            int idVozila;
            if (!int.TryParse(TxtIdVozila.Text, out idVozila) || idVozila < 0)
            {
                MessageBox.Show("ID vozila mora biti pozitivan ceo broj.", "Greska", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                socketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverEP = new IPEndPoint(IPAddress.Loopback, 50000);
                socketTCP.Connect(serverEP);

                var r = new Random();
                vozilo = new TaksiVoziloModel
                {
                    Id = idVozila,
                    koordinataX = r.Next(0, Konfiguracija.SirinaLavirinta),
                    koordinataY = r.Next(0, Konfiguracija.VisinaLavirinta),
                    Status = StatusVozila.Slobodno
                };

                PosaljiVozilo();

                TxtStatusKonekcije.Text = "Povezano";
                TxtStatusKonekcije.Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0x6C, 0x3B));
                ZnackaKonekcije.Background = new SolidColorBrush(Color.FromRgb(0xD9, 0xF3, 0xE3));
                BtnPoveziSe.IsEnabled = false;
                TxtIdVozila.IsEnabled = false;

                AzurirajMetrike();

                var nitRada = new Thread(RadiVozilo);
                nitRada.IsBackground = true;
                nitRada.Start();

                DodajPoruku("Vozilo " + idVozila + " povezano i spremno.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Greska prilikom povezivanja: " + ex.Message, "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Radi u pozadinskoj niti: prima zadatke od servera i izvrsava ih (kretanje kroz
        // putanju, slanje statusa)
        private void RadiVozilo()
        {
            while (true)
            {
                ZadatakModel zadatak;
                try
                {
                    var bafer = new byte[16384];
                    int velicina = socketTCP.Receive(bafer);
                    if (velicina == 0)
                        break;

                    using (var ms = new MemoryStream(bafer, 0, velicina))
                    {
                        var bf = new BinaryFormatter();
                        zadatak = bf.Deserialize(ms) as ZadatakModel;
                    }
                }
                catch (Exception)
                {
                    break;
                }

                if (zadatak == null) continue;

                Dispatcher.Invoke(() => DodajPoruku("Nova vožnja -> klijent " + zadatak.IDKlijenta + ", " + zadatak.PredjenaRazdaljina.ToString("F1") + " polja (ruta: " + zadatak.IzabraniAlgoritam + ")"));

                vozilo.Status = StatusVozila.NaPutu;
                PosaljiVozilo();
                Dispatcher.Invoke(AzurirajMetrike);

                PratiPutanju(zadatak.PutanjaDoKlijenta);

                vozilo.Status = StatusVozila.UVoznji;
                PosaljiVozilo();
                Dispatcher.Invoke(AzurirajMetrike);

                PratiPutanju(zadatak.PutanjaDoOdredista);

                vozilo.Status = StatusVozila.Slobodno;
                PosaljiVozilo();

                Thread.Sleep(100); // razmak pre sledece poruke - izbegava spajanje TCP paketa

                var status = new StatusVoznje
                {
                    IdKlijenta = zadatak.IDKlijenta,
                    IdVozila = vozilo.Id,
                    Km = zadatak.PredjenaRazdaljina,
                    CenaVoznje = zadatak.PredjenaRazdaljina * Konfiguracija.CenaPoPolju
                };
                PosaljiStatusVoznje(status);

                vozilo.Km += status.Km;
                vozilo.Zarada += status.CenaVoznje;
                vozilo.BrojMusterija++;

                Dispatcher.Invoke(() =>
                {
                    AzurirajMetrike();
                    DodajPoruku("Vožnja završena i naplaćena " + status.CenaVoznje.ToString("0.00") + " RSD.");
                });
            }
        }

        private void PratiPutanju(System.Collections.Generic.List<Koordinata> putanja)
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
                PosaljiVozilo();
                Dispatcher.Invoke(AzurirajMetrike);
                Thread.Sleep(Konfiguracija.PauzaKretanjaMs);
            }
        }

        private void PosaljiVozilo()
        {
            using (var ms = new MemoryStream())
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, vozilo);
                var bafer = ms.ToArray();
                socketTCP.Send(bafer);
            }
        }

        private void PosaljiStatusVoznje(StatusVoznje status)
        {
            using (var ms = new MemoryStream())
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, status);
                var bafer = ms.ToArray();
                socketTCP.Send(bafer);
            }
        }

        // Azurira sve prikazane metrike i ispisuje trenutnu poziciju u log - poziva se
        // uvek na UI niti (ili direktno, ili preko Dispatcher.Invoke iz pozadinske niti).
        private void AzurirajMetrike()
        {
            TxtPozicija.Text = "(" + vozilo.koordinataX + ", " + vozilo.koordinataY + ")";
            TxtPredjeno.Text = vozilo.Km.ToString("0.0");
            TxtZarada.Text = vozilo.Zarada.ToString("0") + " RSD";
            TxtMusterije.Text = vozilo.BrojMusterija.ToString();
            TxtStatusVozila.Text = vozilo.Status.ToString();
        }

        private void DodajPoruku(string poruka)
        {
            logKretanja.Insert(0, poruka);
            if (logKretanja.Count > 200)
                logKretanja.RemoveAt(logKretanja.Count - 1); // ogranicavamo duzinu loga, brisemo najstarije
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (socketTCP != null)
                socketTCP.Close();
        }
    }
}