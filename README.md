# Sirroh Degu — Valheim (wersja testowa)

Nowe stworzenie `Degu` to kopia fabrycznego prefabu wilka. Wszystkie komponenty (AI, statystyki, animacje, oswajanie, efekty, dźwięki, drop, kolizje) są sklonowane z `Wolf`. Zmieniono skinned mesh oraz wyświetlaną nazwę.

**Degu nie występuje naturalnie.** Mod nie rejestruje wpisów `SpawnSystem`.

Po włączeniu konsoli i `devcommands` wpisz:

```text
spawn Degu
```

## Instalacja

**Thunderstore / r2modman:** pobierz `release/SirRosa-SirrohDegu-0.1.8.zip` i wybierz **Settings → Profile → Import local mod** (bez ręcznego rozpakowywania). Archiwum zawiera w katalogu głównym `manifest.json`, `icon.png` (256 × 256), `README.md` oraz `SirrohDegu.dll`.

**Instalacja ręczna:** rozpakuj `release/SirrohDegu-0.1.8.zip` i skopiuj DLL do `BepInEx/plugins/SirrohDegu/`. Wymagany **BepInExPack Valheim 5.4.2351**. Mod musi znajdować się na kliencie i na hoście/serwerze, gdyż rejestruje sieciowy prefab.

Model pochodzi z `Degu.blend` i zawiera wagi dla 34 kości kompatybilnych nazwami z oryginalnym wilkiem. Żaden skrypt odpowiedzialny za zachowanie wilka nie jest zastępowany.

## Ograniczenia testowe

- Geometrię dopasowano rozmiarem do zakresu wizualnego wilka. Animator, szkielet sceny i collider pozostają wilcze.
- Różna anatomia Degusa i wilka może powodować niedoskonałe zginanie nóg i szyi.
- Materiały wilka pozostają bez zmian; jego tekstura UV może nie pokrywać się z UV Degusa.
- Zachowania w grze, multiplayer i obraz animacji wymagają testów. Sukces kompilacji nie potwierdza poprawnego spawnu.

## Build

GitHub Actions używa dedykowanego serwera Valheim do pobrania aktualnych bibliotek gry, BepInEx do kompilacji `net48`, oraz `Assets/Degu.part1.b64`–`Assets/Degu.part3.b64` z osadzoną geometrią i wagami. Wynikiem jest jedna DLL `SirrohDegu.dll`. Asset `Degu.meshbin` jest zaszyty w DLL, nie wymaga dodatkowych importerów ani Jotunn.

Po udanym buildzie skompilowana DLL, manifest i dwie paczki ZIP (w tym pełna paczka Thunderstore) trafiają jako commit do `release/` (bez upload-artifact, aby uniknąć limitu magazynu artefaktów). Każda nowa wersja moda automatycznie otrzymuje ZIP Thunderstore zgodny z `manifest.json`.

## v0.1.7: naprawa rejestracji

Poprawiono dwa błędy z v0.1.0: klonowanie aktywnego wilka powodowało `NullReferenceException` w `ZNetView.Awake`, a mod nie uzupełniał `ZNetScene.m_namedPrefabs`. Teraz prefab powstaje pod nieaktywnym obiektem pomocniczym, z wyłączoną inicjalizacją sieciową na czas kopiowania; rejestracja następuje po `ZNetScene.Awake` zarówno w `m_prefabs`, jak i `m_namedPrefabs`. Gra sprawdza `GetPrefab("Degu")` i zapisuje jednoznaczny wynik w logu.

**Log oczekiwany:** `Degu REGISTERED + VERIFIED`. Po poleceniu `spawn Degu`: `Degu(Clone): ZNetView.Awake executed`. Jeżeli prefab pojawi się, ale jego model będzie niewidoczny lub zdeformowany, konieczne będą osobne testy renderingu i animacji.

## v0.1.7: naprawa deformacji

Oryginalny wilk pozostaje sterownikiem animacji, AI, dźwięku i rozgrywki. Geometria Degusa nie jest już wiązana bezpośrednio z oryginalnymi pozycjami stawów wilka: powstaje drugi, anatomicznie dopasowany szkielet z 34 kośćmi, którego lokalne obroty są odczytywane z animowanego wilka w LateUpdate. Model i jego stawy przechodzą tę samą transformację osi Degu (+Y góra, +Z przód) na osie oryginalnego wilka. Bind poses są liczone względem szkieletu Degusa, nie kości oryginalnego wilka. Waga maksymalnie czterech kości na wierzchołek.

Nadal testowy: sprawdzić zmianę animacji, zginanie ogona i kończyn, multiplayer oraz UV / materiał. Collidery i parametry AI pozostają wilcze, żadnego naturalnego spawnu.

## v0.1.7 — poziom, łapy, materiały

- Oś pionu pochodzi z wektora `Wolf Thigh → Foot` (zamiast potencjalnie odwróconego iloczynu wektorowego). Nogi otrzymują stronę właściwą dla przemapowanych osi.
- Model i nowy rig są przesuwane tak, aby najniższe punkty Degusa i oryginalnej siatki wilka znajdowały się na tym samym poziomie; bez stałej wysokości miednicy.
- Kontrola oraz odbudowa wag tylnych ud, barków i części kończyn. Maksymalnie cztery wagi/wierzchołek.
- Zapis ruchu lokalnych translacji kości oprócz rotacji; obroty przenoszone jako delty względem póz referencyjnych. Diagnostyka ruchu kończyn raz po pierwszych 10 sekundach.
- Nieprzezroczysty, ciepłobrązowy materiał futra z dyskretną proceduralną teksturą oraz osobny materiał oczu. Nie używa tekstur wilka. Oczy nie są przeznaczone do malowania materiałem futra.
- Nadal zachowane AI, oswajanie, kolizje, drop i brak naturalnego spawnu.

**Test w grze jest wymagany:** zweryfikować chód, poziom łap, kierunek spojrzenia i wygląd materiałów. Nowa wersja nie jest jeszcze zweryfikowana wizualnie na kliencie gry.

## v0.1.7 — ponowne zbudowanie wag i przywrócenie animacji

- Wagi są generowane **od zera z oryginalnych Face Sets**, z wydzieleniem lewej i prawej strony, połączeniami stawów, topologicznym wygładzaniem i normalizacją czterech wpływów.
- Usunięto runtime DeguWeightRepair i przepisanie materiałów. SkinnedMeshRenderer zachowuje **oryginalne materiały i tekstury wilka z Valheim**.
- Naprawiono kierunek trójkątów (previous exporter rendered backfaces; to mogło powodować wrażenie przeźroczystości).
- Przywrócono kopiowanie animowanych lokalnych obrotów wilka z v0.1.2, która dawała widoczne animacje. Diagnostyka `Degu ANIM QA` po 10 sekundach.
- Dołączono natywny plik `.blend` z 34 kośćmi, grupami wag i zachowanym oznaczeniem Face Sets, zweryfikowany skryptem Blendera w GitHub Actions. Plik jest w `release/`, nie w paczce instalacyjnej.

**Model wymaga testu w uruchomionej grze.** W Blenderze zweryfikowano niezależnie deformacje kończyn i brak zmian w pozycji spoczynkowej. Ostateczny chód i wygląd tekstur są zależne od animacji i UV Valheim.

## v0.1.7 — poprawa spamu konsoli i oryginalnego atlasu wilka

- Wyeliminowano `NullReferenceException` z `DeguRigDriver.LateUpdate` po utworzeniu nowego obiektu. Częściowo serializowany cache diagnostyki nie był odtwarzany na instancjach, co powodowało wyjątek co klatkę. Dodano bezpieczną inicjalizację w `Awake` i kontrole brakujących kości.
- Zidentyfikowano oryginalny materiał `wolfMat` i jego shader `Custom/Creature` (kanał UV0 próbkowany przez `_MainTex`, `_BumpMap` oraz `_EmissionMap`). Mod **nie tworzy żadnych materiałów ani tekstur w grze**. Zachowuje wszystkie oryginalne materiały wilka bez ich modyfikacji.
- Siatka Degusa ma niekompatybilne UV: 359 wierzchołków miało wartości (0,0), a siatki oczu obejmowały duże fragmenty atlasu. Wprowadzono mapowanie UV według Face Sets na strefy atlasu `Wolf Pixel1`: uszy, pysk, tułów, kończyny, ogon oraz dwa oddzielne oczodoły.
- Atlas źródłowy używany jest **tylko podczas kompilacji** do audytu UV i przeglądu w Blenderze. W DLL są zapisane wyłącznie współrzędne UV i skinning; gra pobiera teksturę ze swojej oryginalnej instalacji Valheim.
- Plik `.blend`, raport UV i obraz z nałożonymi punktami UV w katalogu `release/` umożliwiają sprawdzenie wyniku przed testem.

Nie zmieniano sztucznie wag z wersji 0.1.4. Mapowanie atlasu jest eksperymentalne i wymaga wizualnej kontroli w grze. Komenda: `spawn Degu`, brak spawnu naturalnego.

## v0.1.7 — obrys UV od użytkownika, kontakt łap

- Model jest rzutowany na **jedną stronę tekstury wilka 256×256**, dokładnie według czerwonego obrysu użytkownika: pysk po lewej stronie, przednie łapy na lewym dole, tylne po prawej, ogon u góry z prawej. **Lewa i prawa strona Degusa używają wspólnego obszaru** UV. Nos wyznacza zielony punkt (65,136); lewe i prawe oko nie współdzielą atlasu i używają zaznaczonych punktów (39,67) oraz (184,97).
- Pozostałe punkty UV poza konturem są przenoszone do najbliższej bezpiecznej części tej samej sylwetki, bez przypadkowego próbkowania innych atlasowych przedmiotów.
- W runtime nie tworzymy ani nie zastępujemy materiału i tekstur wilka. Oryginalny `wolfMat` pozostaje bez zmian. Materiał w pliku Blender służy **wyłącznie do podglądu** UV i ma podpięty oryginalny atlas Valheim.
- Geometria, wagi, kości i indeksy trójkątów z poprzedniej wersji pozostają niezmienione. Wyniki UV i symetrii są sprawdzane w CI, a obraz `Degu_Wolf_UV_Overlay.png` pokazuje projekcję na teksturę.
- Poprzednia pozycja spoczynkowa wynikała z ustawienia łap według rozmiaru granic siatki wilka, która zawiera również margines animacji. Nowe pozycjonowanie bazuje na punkcie podparcia fizycznego stworzenia. Przy każdej aktualizacji animacji cztery osobne końcówki łap otrzymują niewielką korektę przy kontakcie z podłożem (bez raycastów), maksymalnie 0,22 m. To przybliżenie geometryczne; właściwy wynik wymagający weryfikacji w Valheim.
- Plik Blender ma 34 oryginalnie nazwane kości, wszystkie Vertex Groups, Face Sets, nowy atlas UV i testy deformacji.

**Test:** sprawdź pozycję tylnych łap w idle, przednich podczas chodu, odbicie obu stron futra oraz oczy i nos. Konfiguracja wciąż testowa.

## v0.1.7 — poprawki na podstawie kolorowej mapy użytkownika

- Oczy: punkty POMARAŃCZOWE (39.5,67.5) i (183.5,96.5) zastępują poprzednie strefy; zaokrąglone centra zgodnie z 256×256 szkicem.
- Wnętrze uszu: ŻÓŁTY obszar atlasu, środek (99.2,97.3). Rozdzielenie wnętrza i zewnętrznego futra po orientacji powierzchni i wysokości.
- Podeszwy łap: JASNONIEBIESKI obszar atlasu, środek (157.9,101). Dobór powierzchni patrzących ku ziemi.
- Oczy mają własny drugi submesh wykonany z przeciwstawnych kierunków trójkątów. Eliminuje zanikanie gałek oczu przy backface culling bez dwustronnego materiału czy zmiany całej geometrii.
- Oczy mają osobny `MaterialPropertyBlock` z emissive, bez zmiany ani kopiowania oryginalnego `wolfMat` (materiał ten jest przypisany do obu submeshy; zewnętrzny pozostaje bez zmian). Wymaga wsparcia właściwości `_EmissionColor` przez `Custom/Creature`.
- Stałe pochylenie modelu względem przodu skorygowane domyślnie o 15° w dół; **ta sama rotacja działa na geometrię i stawy**, przed wyznaczeniem bind poses. Kąt można zmienić w `BepInEx/config/sirroh.valheim.degu.cfg` (`PitchCorrectionDegrees`) po uruchomieniu moda i ponownym wejściu do świata.
- Pozycjonowanie modelu i korekta kontaktów łap z v0.1.6 zachowane; test gry pozostaje konieczny.

Build sprawdza liczbę wierzchołków wnętrz uszu i spodów łap przypisanych do specjalnych obszarów. Blender pokazuje wynik na prawdziwej teksturze wilka, ale w grze nadal jest ładowana wyłącznie tekstura Valheim.

## v0.1.8 — poprawka regresji v0.1.7

- Przywrócono naturalny poziom modelu Degusa: źródłowy mesh ma front i tył na podobnej wysokości, dlatego usunięto sztuczne 15° pochylenia. Opcja `PitchCorrectionDegrees` z poprzedniej wersji jest całkowicie ignorowana nawet w istniejącym pliku konfiguracji.
- Usunięto nienaturalną korektę w `DeguRigDriver.LateUpdate`, która podnosiła końcowe kości wszystkich łap do punktu bazowego prefaba. Powodowało to chowanie łap wewnątrz brzucha. Teraz żadne kości łap nie są ręcznie podnoszone. Wysokość początkowa jest obliczana z faktycznych wierzchołków łap o wadze co najmniej 0,25, nie z najniższego punktu ogona czy korpusu.
- Żółta strefa oryginalnej tekstury wilka jest nakładana tylko na mały przedni/środkowy obszar wnętrza małżowiny. Tył, obrzeże i powierzchnia zewnętrzna ucha zachowują teksturę futra. Audyt CI wymaga, aby ten obszar stanowił poniżej 30% wierzchołków uszu.
- Emisja oczu obniżona z `_EmissionColor=(1.6,1.9,2.35)` do `(.12,.16,.21)` i z `_EmissionIntensity=2` do `.20`. Shader i materiał oryginalnego wilka zachowane.

Nie zmieniono algorytmu wag ani transferu oryginalnych animacji wilka. QA w Blenderze sprawdza deformacje, natomiast faktyczna postawa, kontakt z podłożem i wygląd uszu/emisji wymagają weryfikacji w Valheim.
