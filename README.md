# Sirroh Degu — Valheim (wersja testowa)

Nowe stworzenie `Degu` to kopia fabrycznego prefabu wilka. Wszystkie komponenty (AI, statystyki, animacje, oswajanie, efekty, dźwięki, drop, kolizje) są sklonowane z `Wolf`. Zmieniono skinned mesh oraz wyświetlaną nazwę.

**Koszatniczka występuje naturalnie na Równinach**, w grupach 3–7; nadal możesz użyć `spawn Degu`.

Po włączeniu konsoli i `devcommands` wpisz:

```text
spawn Degu
```

## Instalacja

**Thunderstore / r2modman:** pobierz `release/SirRosa-SirrohDegu-0.2.1.zip` i wybierz **Settings → Profile → Import local mod** (bez ręcznego rozpakowywania). Archiwum zawiera w katalogu głównym `manifest.json`, `icon.png` (256 × 256), `README.md` oraz `SirrohDegu.dll`.

**Instalacja ręczna:** rozpakuj `release/SirrohDegu-0.2.1.zip` i skopiuj DLL do `BepInEx/plugins/SirrohDegu/`. Wymagany **BepInExPack Valheim 5.4.2351**. Mod musi znajdować się na kliencie i na hoście/serwerze, gdyż rejestruje sieciowy prefab.

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

## v0.1.9 — model po ręcznej korekcie w Blenderze
- Na podstawie dostarczonego `Degu_Wolf_Reweighted_v0.1.8.blend` z trzema nowymi namalowanymi Face Sets, przygotowano nowy model do instalacji. Wykryte etykiety `24` (8 trójkątów, pierwsze wnętrze ucha), `25` (18 trójkątów, nos) oraz `26` (8 trójkątów, drugie wnętrze ucha).
- UV tych Face Sets odnoszą się wyłącznie do właściwego wycinka oryginalnego atlasu wilka 256×256: żółty dla wewnętrznych płaszczyzn uszu, zielony dla nosa. Obie małżowiny otrzymują lustrzane współrzędne. Nie tworzymy żadnych nowych materiałów w grze.
- Współdzielone wierzchołki wzdłuż brzegów nowych Face Sets są duplikowane tylko w tych 34 trójkątach; zapobiega to przypadkowemu przemalowywaniu zewnętrznej części ucha i reszty pyska. Przybywają **32 wierzchołki (1477 → 1509)**. Każda kopia zachowuje pozycję i istniejące wagi.
- Korekty kości wykonywane przez użytkownika po lewej stronie zostały sprawdzone i lustrzanie odtworzone dla wszystkich 8 par kości przednich i tylnych kończyn w pliku kontrolnym `.blend`. Zapis pozycji spoczynkowych w kodzie moda odpowiada symetrycznie dopasowanemu rigowi.
- Żadnych zmian w AI wilka, systemie animacji, oswajaniu, dźwiękach, kolizji i naturalnym spawnie (wyłącznie `spawn Degu`).
- Testy CI obejmują ścisłą liczbę trójkątów trzech regionów, zachowanie wag i indeksów, pełny test deformacji w Blenderze i standardową kompilację pakietu Thunderstore.
- Do oceny działania w Valheim konieczny jest ponowny test gry. Plik źródłowy użytkownika i testowa paczka nie są tym samym co zweryfikowana animacja runtime.

## v0.1.10 — znikanie Degusa podczas biegu

Wersja naprawcza koncentruje się wyłącznie na zachowaniu podczas animacji sprintu/galopu. Geometria, Face Sets, UV, wagi i punkty kości z v0.1.9 pozostają niezmienione.

- Degu używa nowego `SirrohDegu_StableVisual` (`SkinnedMeshRenderer`) osadzonego bezpośrednio pod korzeniem obiektu stworzenia; nie dziedziczy już skali i widoczności obiektu oryginalnego renderera wilka. Oryginalny renderer jest wyłączany dopiero po ukończeniu konfiguracji skórki. Oryginalny Animator i jego 34 kości pozostają aktywne jako jedyne źródło animacji.
- `Animator.cullingMode = AlwaysAnimate` na sklonowanym stworzeniu utrzymuje animowane kości wilka, mimo wyłączenia źródłowego SkinnedMeshRenderera.
- Ruch root `Pelvis` jest ograniczony do 0,24 m od bind pose, tylko jeśli oryginalny Animator zgłosi ekstremalne przesunięcie. Typowe animacje idle/chód nie są zmieniane, a obroty pozostałych 33 kości pozostają kopiowane jak wcześniej.
- Zakres renderowania skinned mesha ma dodatkowy margines, aby gwałtowne wymachy podczas galopu nie powodowały omyłkowego ukrywania mesha.
- Jednorazowe ostrzeżenia diagnostyczne `Degu sprint diagnostic` zapisują zaobserwowaną zmianę skali oryginalnego wilka lub przesunięcie miednicy. Bez spamu każdą klatkę.

**Weryfikacja:** CI sprawdza kompilację i deformacje w Blenderze; bieg w Valheim należy sprawdzić w grze. Jeśli błąd pozostanie, diagnozę opieramy na zarejestrowanym ostrzeżeniu sprintu, zamiast zmieniać wagi lub kształt modelu.

## v0.1.11 — naprawa rozciągnięć tekstury przy uszach i podeszwach

Oryginalny `wolfMat` ma shader `Custom/Creature` i korzysta z jednej atlasowej mapy `_MainTex` oraz map normalnych i emisji o tych samych UV. Osobne piksele wnętrza uszu, nosów i podeszw **nie są domyślną dodatkową warstwą malowaną na futrze**; są osobnymi wyspami w tej samej teksturze UV. `_StyleTex` nie jest używany (w plikach zasobów oryginalnego wilka `_UseStyles=0`).

Wersja usuwa główną przyczynę degeneracji: wcześniejsze reguły przypisywały UV podeszew i wnętrz uszu indywidualnym wierzchołkom. Trójkąty łączące wierzchołek z atlasu futra z wierzchołkiem atlasu podeszwy interpolowały po dalekich częściach obrazka, tworząc brudne, rozciągnięte tekstury.

- Podstawowy mapping `retarget_wolf_uv_profile.py` nakłada futro na wszystkie trójkąty ciała oraz pozostawia działające UV oczu i pyska; nie przypisuje już osobnej wyspy ucha/podeszwy pojedynczym wierzchołkom.
- Wyłącznie wybrane przez użytkownika kompletne Face Sets `24` i `26` otrzymują żółte wnętrze ucha; `25` — nos. Zewnętrzna strona ucha pozostaje futrem.
- Podeszwy są wyznaczane na podstawie całych trójkątów skierowanych mocno ku ziemi (`normal.y < -0.80`, `y < 0.07`), w obszarze stóp. Obie strony używają tej samej jasnoniebieskiej wyspy atlasu.
- Skrypt duplikuje wierzchołki tylko na granicach wysp UV, zachowując oryginalne pozycje i wagi. Nie ma już trójkątów, które interpolują teksturę pomiędzy odległym fragmentem atlasu futra i specjalnym obszarem.
- Nie zmienia się geometria postaci, układ kości, wagi, animacje, pozycja podczas biegu, oryginalny `wolfMat` ani emisja oczu. Nowa tekstura nie jest generowana i nie dochodzi do nakładania dodatkowego materiału.

Uwagi: ostro odcięta różnica koloru na granicy poprawnie rozdzielonych UV jest naturalna dla pojedynczej atlasowej tekstury i nie stanowi deformacji geometrii. Ewentualne artefakty mipmap / bilinear przy małych wyspach mogą wymagać osobnej analizy w grze. Test w Valheim pozostaje konieczny.

## v0.2.0 — Równiny, polska nazwa i Sirroh Overhaul

- Wyświetlana nazwa w języku polskim: **Koszatniczka**. Identyfikator prefaba pozostaje `Degu`, więc polecenie `spawn Degu` nadal działa. W pozostałych językach nazwa to `Degu`.
- Wpis do oryginalnych spawnerów `SpawnSystem`: tylko `Heightmap.Biome.Plains`, po 3–7 sztuk na grupę, maksymalnie 21 w aktywnych strefach, interval 250 s, próba 35%, w dzień i w nocy. Rejestracja odbywa się raz na listę, bez własnego cyklicznego tworzenia obiektów.
- Koszatniczka należy do frakcji `PlainsMonsters`, aby natywne stworzenia tej frakcji nie uznawały jej za wroga. Dzika Koszatniczka nigdy nie wybiera celu przez `BaseAI.FindEnemy`; wyłączono także polowanie na gracza i ataki na budynki. Oswojone Koszatniczki zachowują oryginalną logikę chowańca.
- Kompatybilność z **Sirroh Overhaul 0.22.66+**: prefab `Degu` korzysta z katalogu wilczych umaszczeń (Default, Albino, Black, DarkBrown, Grey, Blood, Midnight), z wyborem sześciu kolorów oczu oraz istniejącą emisją run żywiołów przy odpowiednim poziomie chowańca. Całość jest opcjonalna — bez Sirroh Overhaul stosowane są zwykłe materiały wilka.
- Wersja Sirroh Overhaul 0.22.66 dodaje rozpoznawanie Degu w systemie FurRandomizer i menu tresury; nie ma duplikowania AssetBundle ani osobnej DLL z mechanikami.

**Testy w grze:** przyłączanie do frakcji Równin, zachowanie neutralne wobec innych potworów, naturalny spawn w grupach i synchronizacja umaszczenia/oczu/run w multiplayer. Sama kompilacja nie zastępuje testu zachowania.


## v0.2.1 — opcjonalny adapter Sirroh Overhaul 0.22.65

W jednej DLL `SirrohDegu.dll` umieszczono opcjonalny adapter, który nie modyfikuje repozytorium ani plików Sirroh Overhaul. Adapter aktywuje się wyłącznie po wykryciu dokładnie wersji 0.22.65. Bez Overhaul Degu nadal działa samodzielnie.

- Losuje 1 z 7 oryginalnych wariantów sierści wilka oraz 1 z 6 kolorów oczu na właścicielu ZDO; zapis i sieć zachowują oryginalne klucze Overhaul.
- Rozszerza wybór umaszczenia i koloru oczu oswojonego Degu w menu Tresury.
- Obsługuje runy żywiołów od poziomu chowańca 25, włącznie z przełącznikiem, stanem zapisanym i podglądem 3D.
- Materiały nadpisywane są jedynie na `SirrohDegu_StableVisual`, z odrębnym drugim submeshem dla oczu. Wilki i inne prefabrykaty nie są zmieniane.
- W przypadku błędu inicjalizacji Harmony adapter jest wyłączany; Degu nadal działa bez kompatybilności.

Kompatybilność runtime, synchronizację host/klient i widoczność materiałów należy sprawdzić w grze. Kompilacja i weryfikatory statyczne nie stanowią takiego testu.
