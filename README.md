# Sirroh Degu — Valheim (wersja testowa)

Nowe stworzenie `Degu` to kopia fabrycznego prefabu wilka. Wszystkie komponenty (AI, statystyki, animacje, oswajanie, efekty, dźwięki, drop, kolizje) są sklonowane z `Wolf`. Zmieniono skinned mesh oraz wyświetlaną nazwę.

**Degu nie występuje naturalnie.** Mod nie rejestruje wpisów `SpawnSystem`.

Po włączeniu konsoli i `devcommands` wpisz:

```text
spawn Degu
```

## Instalacja

**Thunderstore / r2modman:** pobierz `release/SirRosa-SirrohDegu-0.1.4.zip` i wybierz **Settings → Profile → Import local mod** (bez ręcznego rozpakowywania). Archiwum zawiera w katalogu głównym `manifest.json`, `icon.png` (256 × 256), `README.md` oraz `SirrohDegu.dll`.

**Instalacja ręczna:** rozpakuj `release/SirrohDegu-0.1.4.zip` i skopiuj DLL do `BepInEx/plugins/SirrohDegu/`. Wymagany **BepInExPack Valheim 5.4.2351**. Mod musi znajdować się na kliencie i na hoście/serwerze, gdyż rejestruje sieciowy prefab.

Model pochodzi z `Degu.blend` i zawiera wagi dla 34 kości kompatybilnych nazwami z oryginalnym wilkiem. Żaden skrypt odpowiedzialny za zachowanie wilka nie jest zastępowany.

## Ograniczenia testowe

- Geometrię dopasowano rozmiarem do zakresu wizualnego wilka. Animator, szkielet sceny i collider pozostają wilcze.
- Różna anatomia Degusa i wilka może powodować niedoskonałe zginanie nóg i szyi.
- Materiały wilka pozostają bez zmian; jego tekstura UV może nie pokrywać się z UV Degusa.
- Zachowania w grze, multiplayer i obraz animacji wymagają testów. Sukces kompilacji nie potwierdza poprawnego spawnu.

## Build

GitHub Actions używa dedykowanego serwera Valheim do pobrania aktualnych bibliotek gry, BepInEx do kompilacji `net48`, oraz `Assets/Degu.part1.b64`–`Assets/Degu.part3.b64` z osadzoną geometrią i wagami. Wynikiem jest jedna DLL `SirrohDegu.dll`. Asset `Degu.meshbin` jest zaszyty w DLL, nie wymaga dodatkowych importerów ani Jotunn.

Po udanym buildzie skompilowana DLL, manifest i dwie paczki ZIP (w tym pełna paczka Thunderstore) trafiają jako commit do `release/` (bez upload-artifact, aby uniknąć limitu magazynu artefaktów). Każda nowa wersja moda automatycznie otrzymuje ZIP Thunderstore zgodny z `manifest.json`.

## v0.1.4: naprawa rejestracji

Poprawiono dwa błędy z v0.1.0: klonowanie aktywnego wilka powodowało `NullReferenceException` w `ZNetView.Awake`, a mod nie uzupełniał `ZNetScene.m_namedPrefabs`. Teraz prefab powstaje pod nieaktywnym obiektem pomocniczym, z wyłączoną inicjalizacją sieciową na czas kopiowania; rejestracja następuje po `ZNetScene.Awake` zarówno w `m_prefabs`, jak i `m_namedPrefabs`. Gra sprawdza `GetPrefab("Degu")` i zapisuje jednoznaczny wynik w logu.

**Log oczekiwany:** `Degu REGISTERED + VERIFIED`. Po poleceniu `spawn Degu`: `Degu(Clone): ZNetView.Awake executed`. Jeżeli prefab pojawi się, ale jego model będzie niewidoczny lub zdeformowany, konieczne będą osobne testy renderingu i animacji.

## v0.1.4: naprawa deformacji

Oryginalny wilk pozostaje sterownikiem animacji, AI, dźwięku i rozgrywki. Geometria Degusa nie jest już wiązana bezpośrednio z oryginalnymi pozycjami stawów wilka: powstaje drugi, anatomicznie dopasowany szkielet z 34 kośćmi, którego lokalne obroty są odczytywane z animowanego wilka w LateUpdate. Model i jego stawy przechodzą tę samą transformację osi Degu (+Y góra, +Z przód) na osie oryginalnego wilka. Bind poses są liczone względem szkieletu Degusa, nie kości oryginalnego wilka. Waga maksymalnie czterech kości na wierzchołek.

Nadal testowy: sprawdzić zmianę animacji, zginanie ogona i kończyn, multiplayer oraz UV / materiał. Collidery i parametry AI pozostają wilcze, żadnego naturalnego spawnu.

## v0.1.4 — poziom, łapy, materiały

- Oś pionu pochodzi z wektora `Wolf Thigh → Foot` (zamiast potencjalnie odwróconego iloczynu wektorowego). Nogi otrzymują stronę właściwą dla przemapowanych osi.
- Model i nowy rig są przesuwane tak, aby najniższe punkty Degusa i oryginalnej siatki wilka znajdowały się na tym samym poziomie; bez stałej wysokości miednicy.
- Kontrola oraz odbudowa wag tylnych ud, barków i części kończyn. Maksymalnie cztery wagi/wierzchołek.
- Zapis ruchu lokalnych translacji kości oprócz rotacji; obroty przenoszone jako delty względem póz referencyjnych. Diagnostyka ruchu kończyn raz po pierwszych 10 sekundach.
- Nieprzezroczysty, ciepłobrązowy materiał futra z dyskretną proceduralną teksturą oraz osobny materiał oczu. Nie używa tekstur wilka. Oczy nie są przeznaczone do malowania materiałem futra.
- Nadal zachowane AI, oswajanie, kolizje, drop i brak naturalnego spawnu.

**Test w grze jest wymagany:** zweryfikować chód, poziom łap, kierunek spojrzenia i wygląd materiałów. Nowa wersja nie jest jeszcze zweryfikowana wizualnie na kliencie gry.

## v0.1.4 — ponowne zbudowanie wag i przywrócenie animacji

- Wagi są generowane **od zera z oryginalnych Face Sets**, z wydzieleniem lewej i prawej strony, połączeniami stawów, topologicznym wygładzaniem i normalizacją czterech wpływów.
- Usunięto runtime DeguWeightRepair i przepisanie materiałów. SkinnedMeshRenderer zachowuje **oryginalne materiały i tekstury wilka z Valheim**.
- Naprawiono kierunek trójkątów (previous exporter rendered backfaces; to mogło powodować wrażenie przeźroczystości).
- Przywrócono kopiowanie animowanych lokalnych obrotów wilka z v0.1.2, która dawała widoczne animacje. Diagnostyka `Degu ANIM QA` po 10 sekundach.
- Dołączono natywny plik `.blend` z 34 kośćmi, grupami wag i zachowanym oznaczeniem Face Sets, zweryfikowany skryptem Blendera w GitHub Actions. Plik jest w `release/`, nie w paczce instalacyjnej.

**Model wymaga testu w uruchomionej grze.** W Blenderze zweryfikowano niezależnie deformacje kończyn i brak zmian w pozycji spoczynkowej. Ostateczny chód i wygląd tekstur są zależne od animacji i UV Valheim.
