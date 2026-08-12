"""
Analiza podataka Taksi Dispecerskog Sistema.
Cita Podaci/poredjenje_algoritama.csv i pravi 4 grafikona.
Pokretanje: python analiza_podataka.py
"""

import os
import pandas as pd
import matplotlib.pyplot as plt

PUTANJA_PODACI = "Podaci"
PUTANJA_POREDJENJE = os.path.join(PUTANJA_PODACI, "poredjenje_algoritama.csv")
PUTANJA_VOZNJE = os.path.join(PUTANJA_PODACI, "voznje.csv")


def ucitaj_podatke():
    if not os.path.exists(PUTANJA_POREDJENJE):
        raise FileNotFoundError(f"Nije pronadjen fajl: {PUTANJA_POREDJENJE}")

    poredjenje = pd.read_csv(PUTANJA_POREDJENJE, sep=";", encoding="utf-8-sig")

    voznje = None
    if os.path.exists(PUTANJA_VOZNJE):
        voznje = pd.read_csv(PUTANJA_VOZNJE, sep=";", encoding="utf-8-sig")

    return poredjenje, voznje


def analiza_1_efikasnost_algoritama(df):
    print("\n1) POREDJENJE ALGORITAMA PO EFIKASNOSTI PRETRAGE")

    rezime = df.groupby("Algoritam").agg(
        ProsecnoVremeMs=("VremeMs", "mean"),
        MedijanaVremeMs=("VremeMs", "median"),
        ProsecnoPosecenihCvorova=("PosecenihCvorova", "mean"),
        BrojMerenja=("Algoritam", "count"),
    ).round(4)
    print(rezime)

    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(11, 4.5))
    rezime["ProsecnoVremeMs"].plot(kind="bar", ax=ax1, color="#377eb8")
    ax1.set_title("Prosecno vreme izvrsavanja (ms)")
    ax1.set_ylabel("ms")

    rezime["ProsecnoPosecenihCvorova"].plot(kind="bar", ax=ax2, color="#4daf4a")
    ax2.set_title("Prosecan broj posecenih cvorova")
    ax2.set_ylabel("cvorova")

    plt.tight_layout()
    plt.savefig("grafik_1_algoritmi.png", dpi=120)
    plt.close()
    print("-> Sacuvano: grafik_1_algoritmi.png")


def analiza_2_velicina_lavirinta(df):
    print("\n2) UTICAJ VELICINE LAVIRINTA NA PERFORMANSE ALGORITAMA")

    df = df.copy()
    df["VelicinaLavirinta"] = df["SirinaLavirinta"].astype(str) + "x" + df["VisinaLavirinta"].astype(str)
    redosled = (
        df[["SirinaLavirinta", "VelicinaLavirinta"]]
        .drop_duplicates()
        .sort_values("SirinaLavirinta")["VelicinaLavirinta"]
        .tolist()
    )

    rezime = df.groupby(["VelicinaLavirinta", "Algoritam"]).agg(
        ProsecnoVremeMs=("VremeMs", "mean"),
        MedijanaVremeMs=("VremeMs", "median"),
        ProsecnoPosecenihCvorova=("PosecenihCvorova", "mean"),
    ).round(4)
    print(rezime.reindex(redosled, level="VelicinaLavirinta"))

    if df["VelicinaLavirinta"].nunique() < 2:
        return

    pivot = df.pivot_table(index="VelicinaLavirinta", columns="Algoritam", values="VremeMs", aggfunc="mean")
    pivot = pivot.reindex(redosled)
    pivot.plot(kind="line", marker="o", figsize=(8, 5))
    plt.title("Prosecno vreme izvrsavanja po velicini lavirinta")
    plt.ylabel("VremeMs")
    plt.tight_layout()
    plt.savefig("grafik_2_velicina.png", dpi=120)
    plt.close()
    print("-> Sacuvano: grafik_2_velicina.png")


def analiza_3_opterecenje_sistema(df):
    print("\n3) UTICAJ BROJA AKTIVNIH VOZILA (OPTERECENJE SISTEMA)")

    rezime = df.groupby(["BrojAktivnihVozila", "Algoritam"]).agg(
        ProsecnoVremeMs=("VremeMs", "mean"),
        ProsecnoPosecenihCvorova=("PosecenihCvorova", "mean"),
    ).round(4)
    print(rezime)

    if df["BrojAktivnihVozila"].nunique() < 2:
        return

    pivot = df.pivot_table(index="BrojAktivnihVozila", columns="Algoritam", values="VremeMs", aggfunc="mean")
    pivot.plot(kind="line", marker="o", figsize=(8, 5))
    plt.title("Prosecno vreme izvrsavanja po broju aktivnih vozila")
    plt.xlabel("Broj aktivnih vozila")
    plt.ylabel("VremeMs")
    plt.tight_layout()
    plt.savefig("grafik_3_opterecenje.png", dpi=120)
    plt.close()
    print("-> Sacuvano: grafik_3_opterecenje.png")


def analiza_4_ukrstena(df):
    print("\n4) UKRSTENA ANALIZA (SirinaLavirinta x BrojAktivnihVozila x Algoritam)")

    pivot = df.pivot_table(
        index=["SirinaLavirinta", "BrojAktivnihVozila"],
        columns="Algoritam",
        values="VremeMs",
        aggfunc="median",
    ).round(4)
    print(pivot)

    if df["SirinaLavirinta"].nunique() < 2 and df["BrojAktivnihVozila"].nunique() < 2:
        return

    algoritmi = sorted(df["Algoritam"].unique())
    fig, axes = plt.subplots(1, len(algoritmi), figsize=(5 * len(algoritmi), 4.5))
    if len(algoritmi) == 1:
        axes = [axes]

    for ax, algoritam in zip(axes, algoritmi):
        podaci = df[df["Algoritam"] == algoritam].pivot_table(
            index="SirinaLavirinta", columns="BrojAktivnihVozila", values="VremeMs", aggfunc="median"
        )
        im = ax.imshow(podaci.values, cmap="YlOrRd", aspect="auto")
        ax.set_xticks(range(len(podaci.columns)))
        ax.set_xticklabels(podaci.columns)
        ax.set_yticks(range(len(podaci.index)))
        ax.set_yticklabels(podaci.index)
        ax.set_xlabel("Broj aktivnih vozila")
        ax.set_ylabel("Sirina lavirinta")
        ax.set_title(algoritam)

        for i in range(podaci.shape[0]):
            for j in range(podaci.shape[1]):
                vrednost = podaci.values[i, j]
                if not pd.isna(vrednost):
                    ax.text(j, i, f"{vrednost:.2f}", ha="center", va="center", fontsize=9)

        fig.colorbar(im, ax=ax, label="Medijana VremeMs")

    plt.suptitle("Ukrstena analiza: velicina lavirinta x broj vozila x algoritam")
    plt.tight_layout()
    plt.savefig("grafik_4_ukrstena_heatmap.png", dpi=120)
    plt.close()
    print("-> Sacuvano: grafik_4_ukrstena_heatmap.png")


def main():
    poredjenje, voznje = ucitaj_podatke()

    analiza_1_efikasnost_algoritama(poredjenje)
    analiza_2_velicina_lavirinta(poredjenje)
    analiza_3_opterecenje_sistema(poredjenje)
    analiza_4_ukrstena(poredjenje)


if __name__ == "__main__":
    main()