import pandas as pd

# Data från undersökningen strukturerad i en tabell
data = {
    "Id": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23],
    "Potential": [5, 9, 8, 8, 8, 7, 10, 7, 8, 8, 8, 7, 7, 8, 9, 7, 7, 8, 8, 8, 7, 8, 8],
    "Intresseväckande": [5, 10, 7, 7, 9, 7, 10, 8, 5, 4, 8, 8, 8, 9, 9, 8, 6, 8, 8, 7, 7, 7, 7],
    "Föredrar_simuleringar": ["Nej", "Ja", "Ja", "Ja", "Ja", "Ja", "Ja", "Ja", "Ja", "Ja", "Nej", "Ja", "Nej", "Ja", "Ja", "Ja", "Nej", "Ja", "Nej", "Nej", "Nej", "Nej", "Ja"],
}

df = pd.DataFrame(data)

# Beräkna snittvärden för respektive numerisk fråga
average_potential = df["Potential"].mean()
average_interest = df["Intresseväckande"].mean()
prefer_simulations_percentage = df["Föredrar_simuleringar"].value_counts(normalize=True)["Ja"] * 100

print(average_potential, average_interest, prefer_simulations_percentage)
#     7.74 / 10          7.48 / 10         65.2%