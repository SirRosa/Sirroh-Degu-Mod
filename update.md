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
