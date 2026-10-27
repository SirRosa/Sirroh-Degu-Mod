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
