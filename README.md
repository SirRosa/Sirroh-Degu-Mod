# Sirroh Degu — Valheim (wersja testowa)

Nowe stworzenie `Degu` to kopia fabrycznego prefabu wilka. Wszystkie komponenty (AI, statystyki, animacje, oswajanie, efekty, dźwięki, drop, kolizje) są sklonowane z `Wolf`. Zmieniono skinned mesh oraz wyświetlaną nazwę.

**Degu nie występuje naturalnie.** Mod nie rejestruje wpisów `SpawnSystem`.

Po włączeniu konsoli i `devcommands` wpisz:

```text
spawn Degu
```

## Instalacja

Pobierz `release/SirrohDegu-0.1.0.zip` i skopiuj znajdującą się w nim DLL do `BepInEx/plugins/SirrohDegu/`. Wymagany **BepInExPack Valheim 5.4.2351**. Mod musi znajdować się na kliencie i na hoście/serwerze, gdyż rejestruje sieciowy prefab.

Model pochodzi z `Degu.blend` i zawiera wagi dla 34 kości kompatybilnych nazwami z oryginalnym wilkiem. Żaden skrypt odpowiedzialny za zachowanie wilka nie jest zastępowany.

## Ograniczenia testowe

- Geometrię dopasowano rozmiarem do zakresu wizualnego wilka. Animator, szkielet sceny i collider pozostają wilcze.
- Różna anatomia Degusa i wilka może powodować niedoskonałe zginanie nóg i szyi.
- Materiały wilka pozostają bez zmian; jego tekstura UV może nie pokrywać się z UV Degusa.
- Zachowania w grze, multiplayer i obraz animacji wymagają testów. Sukces kompilacji nie potwierdza poprawnego spawnu.

## Build

GitHub Actions używa dedykowanego serwera Valheim do pobrania aktualnych bibliotek gry, BepInEx do kompilacji `net48`, oraz `Assets/Degu.part1.b64`–`Assets/Degu.part3.b64` z osadzoną geometrią i wagami. Wynikiem jest jedna DLL `SirrohDegu.dll`. Asset `Degu.meshbin` jest zaszyty w DLL, nie wymaga dodatkowych importerów ani Jotunn.

Po udanym buildzie skompilowana DLL, manifest i paczka ZIP trafiają jako commit do `release/` (bez upload-artifact, żeby uniknąć limitu przechowywania artefaktów).
