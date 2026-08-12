using System;

namespace ZajednickeKlase
{
    public static class Konfiguracija
    {
        public const int SirinaLavirinta = 25;
        public const int VisinaLavirinta = 25;

        // Verovatnoća (0-1) da se posle generisanja "savršenog" lavirinta dodatno probije poneki zid, radi alternativnih ruta (petlji).
        public const double VerovatnocaDodatnihProlaza = 0.12;

        public const int PauzaKretanjaMs = 800;

        public const double CenaPoPolju = 80.0;

        public const int SemeLavirinta = 42;
    }
}