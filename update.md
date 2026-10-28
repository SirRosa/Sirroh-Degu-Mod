# Sirroh Degu — update.md

Data: 2026-10-27. Każde wydanie: problem i przyczyna (potwierdzona / hipoteza), zmiany, cel, wynik CI, wynik w Valheim, regresje. Wynik nieznany = NIEPOTWIERDZONE.

## Historia — fakty

- 0.2.5: wcześniej użytkownik potwierdził futro 0–6, oczy 1–6, neutralność i spawn. Następnie zgłosił brak wariantów również na lokalnej bazie. Przyczyna zmiany zachowania NIEZNANA. main = 626488689ac92836243d37a5c25967973e09d16b; tree 41c763abfa3335d792710e95c8f5a5f1c9a6f6a4; oryginalna DLL blob 2038376cde1787ce05ce59941b9c7e9c688a9d23. Main nie nadpisywać.

- 0.2.6–0.2.9: nieudane wydania; 0.2.8 usunęła ważne patche IsSupported / SyncFamiliar i DeguFamiliarRepair; 0.2.9 utraciła warianty mimo kodu adaptera równego 0.2.5. Przyczyna nieudowodniona.

- 0.2.5 DIAGNOSTIC: przebudowano oryginalną DLL z diagnostyką; użytkownik zgłosił brak wariantów. PR #1 wycofany.

- 0.3.0: IsValid alias i poprawki preview. CI PASS; w grze brak futra, oczu, Tresury, bindpose nadal występował. PR #2 wycofany.

- 0.3.1: nowy DeguAppearanceState losuje i zapisuje indeksy coat i eye. Użytkownik podał logi: coat 0–6, eye 1–6, ale material_adapter=False, brak wyświetlania wariantów; po zmianie cyklu życia renderera brak błędu bindpose w tej próbie; 34 bones / 34 bindposes. Pełnej naprawy bindpose nie potwierdzono.

- 0.3.2: próby ponawiania połączenia z materiałami wilka i Tresurą; CI PASS, użytkownik nadal zgłosił brak widocznych futer i oczu. Bez logów startu 0.3.2 nie ma dowodu, która gałąź adaptera zawiodła. PR #4 roboczy.

## Audyt przed 0.3.3 — obserwacje z kodu

1. Samo LOSOWANIE indeksów jest potwierdzone logami z 0.3.1; problemem jest NAKŁADANIE oryginalnych materiałów z MaterialCatalog.Variants[Wolf] i ApplyProperties do widocznego renderera. Przyczyna material_adapter=False w 0.3.1 nieustalona (load order, wersja DLL, odbicie API, wyjątek).

2. Overhaul 0.22.65: 7 materiałów Wilka w MaterialCatalog.Variants[Wolf]; WolfVisuals.ApplyProperties obsługuje oczy i runy. Tresura: ograniczenia do Wolf w ChooseEyeColor, ToggleElementRunes, FamiliarMenuPanel.UpdateDetail, HandleLifecycleRequest.

3. Krytyczny procesowy błąd: zielony CI nie testuje Unity/Valheim, interakcji graczy i synchronizacji sieciowej. Nie uznawać CI za dowód naprawy.

4. 34 kości i 34 bindposes to jedynie walidacja rozmiarów; ustalenie przyczyny Unity warning wymaga testu runtime.

## Wersja 0.3.3 — plan i status

Cel: przywrócić oryginalne 7 wariantów wilczego futra i 6 oczu, pełną zmianę w Tresurze, 16 kolorów imienia, runy od 25 i przełącznik, renderer i podgląd 3D, serwerową autoryzację i zapis ZDO. AI/spawn i geometria bez zmian.

Plan: zabezpieczyć baseline, ustalić faktyczną ścieżkę inicjalizacji materiałów i menu, wprowadzić ograniczone zmiany i logowanie rzeczywistych błędów adaptera, uruchomić CI, zbudować paczkę testową, nie publikować wydania końcowego bez runtime.

Kryteria: dzikie losowe wizualnie, 7/6 przycisków działających i zsynchronizowanych, poziom 25 runy/switch, brak błędów bindpose, neutralność i spawn 3–7, restart i host–klient.

Stan: audyt wykonany, implementacja 0.3.3 w toku; brak potwierdzenia runtime; nie modyfikować Overhaul ani stabilnego main.

## Zasada kolejnych wpisów

Każdy kolejny commit zmiany: dopisać do update.md datę/wersję, zmodyfikowane pliki, cel, ryzyko, wynik CI, wynik testu gry (PASS/FAIL/NIEPRZETESTOWANE), konkretne regresje.

## 0.3.3 — konkretne zmiany (kandydat; stan Valheim NIEPOTWIERDZONE)
- **Przyczyna / diagnoza:** log 0.3.1 dowiódł poprawnych losowych indeksów ZDO i material_adapter=False. Dla 0.3.2 brak kompletnych logów po starcie, więc faktyczna awaria katalogu Wolf na komputerze użytkownika pozostaje NIEPOTWIERDZONA. Niezależny Fur Randomizer może mieć 7 materiałów w innej DLL; Overhaul w takim trybie celowo nie ładuje własnego bundle. Kod 0.3.2 czytał wyłącznie Overhaul.Variants.
- **Zmienione pliki:** src/DeguAppearanceService.cs (dwustopniowe wykrywanie MaterialCatalog z Overhaul oraz standalone sirroh.furrandomizer; potwierdzenie wszystkich siedmiu prawdziwych Wolf Material.Main, użycie natywnego Assign oraz WolfVisuals.ApplyProperties, nieulotne ponawianie źródła i oczu, diagnostyka materiału/albedo/eyeAtlas), src/DeguFamiliarRepair.cs (sprawdzenie dokładnie Player.m_nview i zapis RPC), manifest.json/src/DeguPlugin.cs/README.md/CI/test kontraktów (0.3.3), update.md.
- **Zamierzony wynik:** rzeczywiste 7 materiałów wilka i 6 oczu na żywym Degu, aktualizacja po zmianie ZDO i menu Tresury, emissive od poziomu 25, brak regresji modelu/AI/spawnu.
- **Ograniczenia:** nie przeprowadzono klientowego testu Valheim. Żaden punkt gameplay nie jest oznaczony PASS; logi modelu 0.3.1 pokazały 34/34 i brak bindpose w tamtym uruchomieniu. Nie modyfikowano DeguWorld, DeguMesh, DeguRigDriver, materiałów Overhaul, animacji, UV.
- **Weryfikacja:** GitHub Actions #40555758498 na commicie ff7396b0dde7aa04cea9a61e52620f6e809946db: **SUCCESS** (kompilacja .NET Framework net48 i statyczne kontrakty), Blender QA PASS (34 kości, 34 grupy, 2580 ścian), raport skinning QA PASS; Thunderstore ZIP: PASS, 90 319 bajtów, SHA-256 06e8b205d01bc76cf85456b688688acb8b7ce6d54035f978fd18efa413bd1cce. **Valheim runtime: NIEPRZETESTOWANE**. Wartość runtime: NIEPOTWIERDZONE. W logu pierwszego spawnu oczekiwać 'Wolf coats READY' i 'DEGU_APPEARANCE: APPLIED ... material=... atlas=... eyeAtlas=...'; w Tresurze 'DEGU_TRESURA: ... ACTIVE', 'sent native request', 'saved appearance'.

## Kontrola sensowności zmian 0.3.3
- TAK: ZDO losuje różne coat/eye (dowód z logów 0.3.1); nowy kod wykorzystuje te indeksy, nie tworzy ponownie losowania.
- TAK: kod Overhaul 0.22.65 ma MaterialCatalog.Variants[Wolf], prywatne Assign i WolfVisuals.ApplyProperties; kandydat sprawdza rzeczywiste 7 materiałów i wykorzystuje je na Degu, a w wariancie z osobnym Fur Randomizerem sprawdza też jego katalog.
- TAK: logi teraz jawnie raportują brak katalogu, wybrany materiał, albedo, eyeAtlas i shader. Wcześniejsze 0.3.1 miało jedynie material_adapter=False.
- TAK: niezmienione AI, spawn, model, rigging, oryginalne skrypty losowania i Overhaul. Zmiana jednego źródła ZDO ma uniknąć konfliktu odziedziczonych Wolf SkinAssignment na Degu.
- **NIEPOTWIERDZONE:** czy faktyczna instalacja użytkownika zawiera oczekiwany Overhaul/Fur Randomizer oraz czy Degu wygląda prawidłowo, menu Tresury i sieć działają. Nie wolno deklarować „100%” ani „naprawione” przed tymi testami.

## 0.3.4 — powrót funkcji menu, ochrona wpisów, brak mutacji wilków
- **Potwierdzone z Overhaul 0.22.65:** natywne `TamingSkill.OnAnswer` (nie `OnAdoptReply`) przyjmuje odpowiedź adopcyjną. `TamingSkill.OnStatus` usuwa z listy chowańca, jeżeli brak tożsamości gatunku i serwer nie potwierdza życia. `Capture` bywa pominięty po adopcji, gdy model nie jest załadowany (`ZNetScene.FindInstance(id)==null`).
- **Zmiany:** `src/DeguFamiliarRepair.cs`: hydracja brakującej identyfikacji z już istniejącego, należącego do aktualnego gracza ZDO Degu przed oczyszczeniem osieroconych wpisów; poprawny hook odpowiedzi `OnAnswer`; odzyskanie brakującego wpisu przy otwartym menu tylko z widocznego, żywego, oswojonego i należącego do gracza Degu, przy zachowaniu natywnego limitu slotów, bez tworzenia ZDO i bez edycji innych gatunków. `src/DeguAppearanceService.cs`: wyeliminowano zmianę `shader` oryginalnego współdzielonego materiału wilka w fallbacku. `tools/verify_overhaul_adapter.py`: kontrakty metod, własności i niemutowania shaderów. Manifest, README, workflow: 0.3.4.
- **Cel:** nie gubić Degu w Tresurze przy opóźnionej rejestracji, przywrócić bezpiecznie chowańce, które wciąż mają właściwego właściciela ZDO; nie zmieniać żadnego wspólnego materiału wilka. Futer/oczu nie oznaczono jako działające na podstawie samego CI; nadal bez klientowego dowodu.
- **Status:** kompilacja i testy CI W TRAKCIE; Valheim runtime NIEPRZETESTOWANE. 0.3.3 użytkownik potwierdził: brak wizualnych wariantów futra/oczu i Degu niewidoczne w menu. Poprzedni wynik CI 0.3.3 nie był dowodem skuteczności.

### 0.3.4 — aktualizacja audytu po implementacji
- **Wykryta regresja procesu:** pierwsza kompilacja 0.3.4 zakończyła się błędem CS0023/CS1061: bezpośrednie odwołanie do ZRoutedRpc.GetServerPeerID nie istnieje w używanych referencjach serwera. Zastąpiono je bezpiecznym odczytem refleksyjnym z odrzuceniem niepotwierdzonego sendera (brak pomijania autoryzacji).
- **Dalsze zmiany:** rozpoznawanie katalogu Wolf przez Variant[7] i przez nazwane wpisy standalone (ustalona kolejność 7 skór); częściowe udostępnianie rzeczywiście istniejących materiałów, jasne logowanie brakujących indeksów zamiast blokady wszystkich skórek; kontrola co 3 s, czy inny mod nie nadpisał materiału Degu, i przywrócenie natywnego wybranego wariantu; ograniczenie powtarzania tych samych ostrzeżeń. Nie dotykano modelu, UV, kości, AI, spawnu ani Overhaul.
- **Weryfikacja merytoryczna:** natywny kod Overhaul 0.22.65 faktycznie ma TamingSkill.OnAnswer i TamingSkill.OnStatus; oryginalny MaterialCatalog ma Variant[7] w kolejności Default, Albino, Black, DarkBrown, Grey, Blood, Midnight; WolfVisuals ma ApplyProperties. Naprawa identyfikacji chowańca operuje tylko istniejącym ZDO, kontroluje właściciela i limit native Slots().
- **Wyniki:** 0.3.4 po ostatnich zmianach CI = W TRAKCIE; zachowanie w kliencie Valheim (futro 7/7, oczy 6/6, Tresura, poziom 25, multiplayer, podgląd) = NIEPRZETESTOWANE. Ostatni raport użytkownika dotyczył nieskuteczności 0.3.3. Nie wolno dopisywać runtime PASS na podstawie CI.
- **Historia PR:** PR #3 (0.3.1), #4 (0.3.2), #5 (0.3.3) zamknięte jako FAIL IN VALHEIM, PR #6 (0.3.4) otwarty jako niezweryfikowany kandydat.

### 0.3.4 — wynik pełnej kompilacji i artefaktu
- **GitHub Actions:** run 40562099212, commit ec377799225cbde9cf48fec8e76bb49af4e84955: **SUCCESS** po poprzednich nieudanych/cancelled iteracjach. PASS: kompilacja .NET Framework net48, kontrakty kodu/AI/spawnu, Blender QA i skinning QA.
- **Paczka Thunderstore RC:** SirRosa-SirrohDegu-0.3.4.zip z tego runu (92 173 B), w ZIP: SirrohDegu.dll (129 536 B), manifest.json (0.3.4), README.md i icon.png. Test integralności ZIP: PASS; SHA-256 `a54e256eaecd166818a662bd5b31e83df1033c75b2ad811dfb69f240bc04eaa5`.
- **Blender QA:** 34 kości, 34 grupy wierzchołków, 2580 ścian, ocena rest-pose PASS; skinning QA PASS. To NIE dowodzi braku ostrzeżenia bindpose w Unity.
- **Valheim runtime:** NIEPRZETESTOWANE na tym buildzie. Futro (7/7), oczy (6/6), menu Tresury, zapis/odtwarzanie chowańców, emisja od 25 poziomu, przełącznik, neutralne AI, spawn Równiny 3–7, podgląd 3D i multi = **NIEPOTWIERDZONE**. Nie ogłaszać wydania produkcyjnego.
- **Wniosek:** kod odpowiada na potwierdzone błędy metod Overhaul (`OnAnswer`, utrata `Prefab` podczas statusu), pominięty fallback materiałów i konflikty shaderów, ale bez uruchomienia gry nie udowodniono ich rozwiązania w praktyce. PR #6 pozostaje draft, `main` bez modyfikacji.

## 0.4.0 — pełna przebudowa modułów, zamiast kolejnych patchy starego adaptera

**Stan wyjściowy (test gry, 2026-10-27, 21:05):** w 0.3.4 Degu spawnuje się w grupach 3–7, renderer ma 34 kości i 34 bindposes, lecz `material_adapter=False`. Właściciel ZDO losuje `coat=0..6` i `eye=1..6`, ale żaden kolor nie trafia na model. Degu nie pojawia się w Tresurze. Log wypisuje masowo niepotrzebne linie `DEGU_BINDPOSE_CHECK`. W przekazanym fragmencie nie ma samego ostrzeżenia bindpose. To są fakty, nie domniemania.

**Architektoniczne błędy 0.3.4 potwierdzone z kodu:**
1. Trzy wersjonowane, zależne od refleksji adaptery (`DeguAppearanceService`, `DeguFamiliarRepair`, `DeguPreviewDiagnostics`) mogą wyłączać WSZYSTKIE funkcje, gdy brakuje choć jednego symbolu lub nie zgadza się ścisły numer wersji Overhaul. `CanRender=false` blokuje nakładanie materiałów mimo poprawnego ZDO. Sam log nie dowodzi, który dokładnie symbol zawiódł.
2. Brak niezależnego pobierania materiałów z załadowanych AssetBundle; Overhaul 0.22.65 przy osobnym Fur Randomizerze nie musi wypełnić własnego katalogu.
3. Degu w Tresurze zależał od all-or-nothing `Install()`, a odzyskiwanie wpisu mogło działać dopiero, kiedy sam wpis był już niewidoczny.
4. Debug w `ZNetView.Awake` i `DeguBindposeTrace` generował ścianę wpisów podczas masowego spawnowania, bez nowych danych.

**Wykonane (kod):**
- `src/DeguWolfPalette.cs` (NOWY): odczyt oryginalnych siedmiu wariantów Wilka z katalogu Overhaul LUB istniejącego załadowanego AssetBundle, bez sztywnego gate wersji; 7 współdzielonych kopii materiałów **tylko dla Degu**, z oryginalną paletą i shaderem Valheim; oczy i żywioły przez natywne `WolfVisuals.ApplyProperties`. Jeden właściciel ZDO losuje kolor futra i oczu, a klienci go odtwarzają.
- `src/DeguFamiliarBridge.cs` (NOWY): niezależne patche każdej funkcji Tresury, zamiast globalnego wyłączenia modułu; natywny RPC z kontrolą właściciela/ZDO i roster; zachowanie wpisów istniejących oswojonych Degu; sterowanie futrem/oczami/kolorem imienia/runami.
- `src/DeguPreviewBridge.cs` (NOWY): odłączony podgląd 3D bez instancjonowania Character/AI, pełna mapa 34 kości PRZED przypisaniem mesha.
- `SirrohDegu.csproj`: stare `OverhaulCompatibility.cs`, `DeguAppearanceService.cs`, `DeguFamiliarRepair.cs`, `DeguPreviewDiagnostics.cs`, `DeguBindposeTrace.cs` wykluczone z kompilacji (pozostały wyłącznie jako historia na GitHub). Żadna stara Harmony integracja nie może działać w nowej DLL.
- `src/DeguPlugin.cs`: wywołuje wyłącznie nowe systemy. Usunięto niepotrzebne globalne patchowanie `ZNetView.Awake` i spamujące śledzenie bindpose. Model/AI/spawn/rig/tekstury pozostają jak w 0.3.4.
- `manifest.json`, README, CI: wydanie próbne 0.4.0. `tools/verify_overhaul_adapter.py`: nowy test izolacji modułów i kontraktów, nie poświadczający runtime.

**Cel:** najpierw prawdziwe warianty Wilka na Degu i przywrócona lista Tresury; następnie działające 7/6 wyborów, runy od poziomu 25, poprawny preview, brak bindpose, neutralne AI i spawn 3–7.

**Wyniki testów:** kompilacja/QA 0.4.0 = DO WERYFIKACJI, wynik Valheim runtime = NIEPRZETESTOWANE. Nie wolno oznaczać żadnej wymaganej funkcji jako PASS tylko na podstawie CI. Nie modyfikować Overhaul ani oryginalnego main.

### 0.4.0 — wynik kompilacji (2026-10-27)
- **Przebiegi CI:** pierwszy run #40577042694 FAIL w błędnym asercie statycznym sprawdzającym tekst komentarza. Kolejna kompilacja #40577428870 FAIL — brak bezpośredniej referencji do UnityEngine.AssetBundleModule. Poprawiono test i dołączono oryginalną bibliotekę Unity AssetBundleModule z Valheim do csproj + GitHub Actions. Run #40578088376 dla commita `aa6d0d6abdf231af7713f6382eb9a4c4ba45cb51` **SUCCESS**: testy kontraktów modułów, kompilacja net48, Blender QA oraz skinning QA.
- **Weryfikacja paczki:** `SirRosa-SirrohDegu-0.4.0.zip` 85030 bajtów, SHA-256 `ce93476596e9097503dbca2d3e981f5f9b0d96db7ab98c1899fcb14d9a5cfbb9`, prawidłowy ZIP i manifest 0.4.0; SirrohDegu.dll 108544 bajty. Kontrola ciągów nazw modułów w DLL: nowe `DeguWolfPalette`, `DeguFamiliarBridge`, `DeguPreviewBridge` są obecne; `DeguAppearanceService`, `DeguFamiliarRepair`, `DeguBindposeTrace` nie występują. **Nie jest to dowód funkcjonalnego uruchomienia w grze.**
- **Wynik Valheim:** NIEPRZETESTOWANE. Nie oznaczać futra, oczu, Tresury, emissive, podglądu ani synchronizacji jako naprawionych tylko na podstawie kompilacji. Dopiero test graficzny/wielu graczy pozwoli na PASS lub FAIL.
- **Bezpieczeństwo zmian:** `main` i oryginalne źródła DeguWorld, DeguMesh, DeguRigDriver, assety użytkownika i Sirroh Overhaul nie były modyfikowane. PR #6 (nieudana wersja 0.3.4) zamknięty, PR #7 pozostaje draft.

## 0.4.1 — 2026-10-28 — porządki w logach i wyższy pitch Degu

**Punkt startowy:** użytkownik potwierdził, że 0.4.0 „zasadniczo wszystko w końcu działa”. Ta wersja jest **działającą podstawą**; nie zmieniać ani nie nadpisywać jej. W zgłoszeniu widać rzeczywiste `DEGU_TRESURA: server saved op=10 / 18`, brak ostrzeżeń bindpose, lecz spam `Degu ANIM QA` (po jednym wpisie od każdego Degu) oraz `Degu sprint diagnostic: vanilla Wolf renderer localScale` (99.17/98.52/99.86 zamiast 100; **niewidoczny renderer wilka**, Degu ma oddzielną skalę 150). To ostatnie nie świadczy o awarii i nie wymaga przestawiania animacji/skali. Wpisy QOL są spoza Degu.

**Zrobione / po co:**
- `src/DeguRigDriver.cs`: usunięto wyłącznie samodzielne próbkowanie i jednorazowe raportowanie ruchu kończyn po 10 sekundach oraz pomiary zmian skali ukrytego wilka. Zachowano **wszystkie** 34 kopie rotacji, retarget miednicy i ograniczenie jej przemieszczenia do 0,24 m z ostrzeżeniem tylko w razie rzeczywistego przekroczenia progu. **Cel:** brak dziesiątek komunikatów, brak zmiany zachowania animacji.
- `src/DeguFamiliarBridge.cs`: `server saved op=10/18` zmieniono z Info na Debug (tylko sukces; błędy/odmowy nadal widoczne). **Cel:** brak wpisu przy każdym kliknięciu w Tresurze.
- NOWY `src/DeguAudioPitch.cs`: na **wyłącznie prefabie Degu** izoluje oryginalne listy `EffectList` przez kopiowanie ich wpisów, a używane prefaby dźwiękowe `ZSFX` klonuje pod nieaktywnym rodzicem, rejestruje pod oddzielnymi nazwami w `ZNetScene` i zmienia `m_minPitch/m_maxPitch` na prywatnych kopiach. Zachowane oryginalne klipy, głośności, efekty, oryginalne prefaby i wilki. Obsługuje także `ZSFX` bezpośrednio osadzone w klonie Degu. **Cel:** wyższy, bardziej piszczący głos tylko Degu (nie globalne przestrojenie wilków).
- `src/DeguPlugin.cs`: podłącza audio do istniejącego/nowego prefabu; `[Audio] VoicePitchMultiplier` domyślnie 1.45, dopuszczalne 1.00–2.00, modyfikacja wymaga restartu gry. Ponowna rejestracja po zmianie świata nie mnoży pitch drugi raz. `manifest.json`, README i workflow: 0.4.1.
- `tools/verify_overhaul_adapter.py`: testuje zachowanie oryginalnych funkcji riggingu i stałe skróty kodu z 0.4.0 oraz izolowanie SFX na prefabie Degu. **Nie zmieniono** DeguMesh, DeguWorld, DeguWolfPalette, DeguPreviewBridge, modelu, tekstur i Overhaul.

**Uzasadnienie:** źródła dźwięku `ZSFX` odtwarzają zmienne `m_minPitch/m_maxPitch`; modyfikowanie ich na oryginalnym prefabie wilka zmieniłoby również wilki. Skopiowanie prefabu i przepisanie własnej `EffectList` Degu izoluje skutki. Logi animacji i niewielkie różnice skal są wyłącznie diagnostyczne. 

**Weryfikacja:** GitHub Actions #40676955037 na commicie `a82c1bd6bfa8b6ed292e92fbd6adf3e283c96c47` = **SUCCESS** (testy regresji, kompilacja .NET net48, Blender QA, skinning QA, pakiet Thunderstore). Paczka testowa `SirRosa-SirrohDegu-0.4.1.zip` = 86 501 bajtów, SHA-256 `29a59e5d9977e4b968700c582115e7042ac2ead98c2f72a7cde52e9fa395e228`, zawiera DLL 110 592 bajtów, manifest 0.4.1, README, ikonę. ZIP integralność PASS, nowy `DeguAudioPitch` obecny w DLL. Runtime Valheim (w tym odsłuch) = **NIEPRZETESTOWANE**. Test statyczny nie zastępuje odsłuchu gry.

## 0.4.2 — 2026-10-28 — odrębne zwłoki Degu (DeguRagdoll)

**Zgłoszenie:** po śmierci żywego Degu powstaje martwy wilk. Przyczyna stwierdzona w prefabach Valheim: Degu powstał przez klon Wolf i przejął `Character.m_deathEffects` z wpisem `Wolf_Ragdoll` (oryginalny ragdoll ma `Ragdoll`, `ZNetView`, łączone rigidbody/collidery i skinned renderer). Nie był zarejestrowany żaden własny prefab zwłok.

**Zmiany / zamiar:**
- Nowy `src/DeguCorpse.cs`: budowanie osobnego **sieciowego DeguRagdoll** przez sklonowanie *Wolf_Ragdoll* pod nieaktywnym holderem. Zamiana wyłącznie wizualna przez sprawdzony `DeguMesh.ReplaceMesh` z 34 kośćmi i istniejącymi oryginalnymi materiałami wilka; stare widoczne rendery wilka wyłączone, ale system natywnych joints, rigidbody, colliderów, `Ragdoll` i obsługa zrzutów przedmiotów pozostają oryginalne. Nie tworzono prefabów zwłok z `Character/MonsterAI`.
- Izolowana kopia `EffectList` i konkretnego `EffectData` **tylko Degu**, wpis `Wolf_Ragdoll` przełączony na `DeguRagdoll`; efekty śmierci wszystkich zwykłych wilków i inne efekty Degu pozostają nietknięte. Po nieudanej walidacji prefab zostaje bez przepięcia, zamiast uszkadzać śmierć.
- `OnDeath` Degu: ograniczony do właściciela ZDO snapshot pozycji, indeksu futra 0–6, oczu 1–6, run i poziomu. Właściciel DeguRagdoll przejmuje najbliższy świeży snapshot i zapisuje stan raz we własnym ZDO; każdy klient nakłada barwy przez **ten sam** działający `DeguWolfPalette`. Brak dopisywania nowej losowości ani ingerencji w Tresurę.
- `src/DeguPlugin.cs`: tylko rejestracja prefabu śmierci w istniejącej ścieżce rejestrowania Degu, zarówno przy prefabie już obecnym jak i nowo zbudowanym, oraz odłączenie statycznej listy snapshotów przy wyłączeniu moda.
- `manifest.json`, `README.md`, `.github/workflows/build.yml`, `tools/verify_overhaul_adapter.py`: wersja 0.4.2; testy zgodności sieciowej, niezmienności starego modelu i izolacji efektów. Bez zmian `DeguMesh.cs`, `DeguWorld.cs`, `DeguWolfPalette.cs`, `DeguRigDriver.cs`, `DeguAudioPitch.cs`, `DeguFamiliarBridge.cs`, `DeguPreviewBridge.cs` i Overhaul.

**Ryzyka i kryteria:** potwierdzić w rzeczywistym Valheim, czy prefab `Wolf_Ragdoll` na tej wersji gry ma pełne 34 kości i czy zwłoki fizycznie się przewracają, synchronizują multiplayer, zachowują barwy i poprawnie oddają łupy; równolegle sprawdzić normalnego wilka. Dopiero runtime potwierdza te wymagania. CI/build NIEPRZETESTOWANE na etapie utworzenia wpisu; Valheim NIEPRZETESTOWANE. Zachować 0.4.1 jako bazę.

### 0.4.2 — wynik kompilacji i stan testów
- **GitHub Actions:** run [40678593781](https://github.com/SirRosa/Sirroh-Degu-Mod/actions/runs/40678593781), commit `704f8ff1ca7abcfcb984062a29467e6ef649992f`, wynik **SUCCESS**. PASS: statyczne kontrakty DeguRagdoll, kompilacja net48 przeciwko rzeczywistym bibliotekom Valheim, Blender QA (34 kości, 34 vertex groups, 2580 ścian, rest-pose error 6.365778852490731e-08) i skinning QA. Testy nie uruchamiają gry ani fizyki Unity.
- **Gotowa paczka RC:** `SirRosa-SirrohDegu-0.4.2.zip` 88 797 bajtów, SHA-256 `4d4da43789c7ec90f68903405a69c159796028c4a4064df3e4dc8dfa10ead10c`. ZIP integralność PASS, manifest 0.4.2, DLL 116 224 bajty zawiera nowy moduł DeguCorpse; oryginalny model, AI, spawn, Tresura i audio niezmienione.
- **Valheim runtime: NIEPRZETESTOWANE.** Sprawdzić: śmierć dzikiej oraz oswojonej koszatniczki → **model Degu, nie wilka**, naturalny upadek ragdolla, brak błędów bindpose, dotychczasowe łupy i czas zanikania, zgodny kolor futra/oczu (także runy 25), ponowne wejście i host–klient. Oddzielnie uśmiercić wilka i potwierdzić jego niezmienione zwłoki. Ewentualne niezgodności fizyki trzeba naprawić przed mergem, bez zmiany żywego Degu.

## 0.4.3 — 2026-10-28 — zwłoki Degu zapadają się pod ziemię
- **Raport z gry:** 0.4.2 poprawnie tworzy zwłoki Degu, ale ich model / ragdoll zapada pod teren. Nie zgłoszono innych regresji. Wcześniejsze testy 0.4.0/0.4.1 potwierdziły funkcje żywych Degu; traktować je jako nienaruszalne.
- **Audyt kodu:** 0.4.2 klonował `Wolf_Ragdoll`, dodawał mniejszą siatkę Degu, ale nie dopasowywał fizycznych colliderów, które nadal odpowiadały gabarytom wilka. **Hipoteza:** geometria kontaktu fizycznego lub tunneling przy początkowym upadku jest przyczyną zapadania. Bez logów pozycji ciała i fizyki z klienta **nie jest to jeszcze udowodniona przyczyna**.
- **Zmiany:** nowy `src/DeguCorpseGroundSupport.cs`, instalowany wyłącznie na nowym `DeguRagdoll` przez `DeguCorpse.Register`: mały podtrzymujący SphereCollider **promień 0,19 m w świecie** w okolicy tułowia, związany z istniejącym natywnym Rigidbody w obrębie miednicy/kręgosłupa. Zachowana dotychczasowa fizyka Ragdoll; nie tworzono Rigidbody, nie zmieniano masy, jointów, dropów ani czasu zanikania. CCD `ContinuousDynamic` przeciwko przenikaniu przez ziemię i `Physics.IgnoreCollision` wyłącznie z innymi colliderami tego samego ragdolla. Zero teleportowania lub stałego przesuwania zwłok.
- **Bez zmian:** `DeguMesh`, `DeguRigDriver`, `DeguWorld`, `DeguWolfPalette`, `DeguFamiliarBridge`, `DeguAudioPitch`, `DeguPreviewBridge`, prefab żywego Degu, Overhaul i wszystkie oryginalne prefabry wilka. `DeguCorpse.cs` zmieniony wyłącznie przez wywołanie `DeguCorpseGroundSupport.Configure(candidate)`.
- **Cel testu:** zabicie kilku dzikich i oswojonych Degu na trawie, zboczu i wybudowanej podłodze; ragdoll pozostaje na powierzchni i poprawnie się przewraca, nie przesuwa nagle, zachowuje materiały, łupy i 2 s naturalnego rozpadu. Sprawdzić zwykłego wilka. Wynik CI i Valheim: NIEPOTWIERDZONY przed wykonaniem.

- **Bezpieczeństwo przy niezgodnym prefabie:** gdy na danej wersji Valheim nie da się dobrać natywnego Rigidbody do podparcia, DeguRagdoll wciąż pozostaje zarejestrowany i odtwarza standardowe zwłoki/łupy; kod loguje jeden komunikat o braku nowego podparcia, nie wraca do martwego wilka. Test gry pozostaje wymagany.

- **Kolejność fizyki:** `DeguCorpseGroundSupport` ma `DefaultExecutionOrder(10000)`, dzięki czemu jego `Start` nakłada CCD i ignorowanie kolizji między własnymi colliderami po `Ragdoll.Start`, gdy pierwotne Rigidbody są już skonfigurowane.
