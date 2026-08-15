# Click Assistant

Desktopový autoclicker pro Windows (.NET 8 / Avalonia UI). Uživatel si na obrazovce nadefinuje sekvenci bodů
(kliknutí myší, případně stisky kláves), appka je pak automaticky přehrává ve zvoleném pořadí a intervalu -
volitelně s "humanizovaným" pohybem myši (zakřivené dráhy, náhodná odchylka polohy a časování), aby akce
nepůsobila jako robotická.

## Hlavní funkce

- **Editor sekvence** - klikací body i klávesové kroky, pořadí sekvenční/náhodné, vlastní pořadí přetažením,
  opakování N-krát nebo donekonečna, základní interval s náhodným jitterem.
- **Humanizace pohybu** - zakřivené (Bézierovy) dráhy myši mezi body, náhodná odchylka cílové pozice, náhodné
  trvání pohybu, šance na "přestřelení" cíle - vše volitelné a nastavitelné.
- **Globální klávesové zkratky** - Start a Stop jsou dvě nezávislé zkratky, fungují i mimo okno appky.
- **Mapa monitorů** - vizuální editor rozložení všech připojených monitorů; body i monitory lze přetahovat
  myší, monitory nejde přetáhnout přes sebe ani mimo plochu mapy (kolize se řeší automatickým odsunutím
  ostatních monitorů nebo přichycením na nejbližší volnou pozici).
- **Živý náhled bodu** - u vybraného bodu lze zobrazit reálný screenshot okolí kliku, který se sám
  aktualizuje, když se změní to, co je pod ním.
- **Detekce změny rozlišení/monitorů** - při spuštění i za běhu appka porovná uložený snapshot obrazovky s
  aktuálním stavem a při neshodě nabídne přepočet souřadnic profilu.
- **Lokalizace** - kompletní UI v 39 jazycích (viz `src/ClickAssistant.App/Localization/Strings`), přepínání
  za běhu bez restartu.
- **Světlý/tmavý/automatický motiv.**

## Požadavky

- Windows (zachytávání obrazovky i simulace vstupu jsou navázané na Windows API).
- Pro build ze zdrojáků [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Spuštění ze zdrojáků

```
dotnet run --project src/ClickAssistant.App
```

## Testy

Automatizované testy pokrývají jen `ClickAssistant.Core` (čistou doménovou logiku bez UI/OS závislostí):

```
dotnet test tests/ClickAssistant.Core.Tests
```

## Sestavení samostatného .exe

```
./publish-exe.ps1
```

(nebo ekvivalentní `dotnet publish` příkaz uvnitř skriptu) vytvoří self-contained single-file
`publish/ClickAssistant.App.exe`, který ke spuštění nepotřebuje nainstalovaný .NET.

## Kde appka ukládá data

Profily a nastavení se ukládají mimo repozitář, do `%AppData%/ClickAssistant/` (profily jednotlivě jako JSON
v `profiles/`, nastavení appky v `settings.json`).

## Struktura projektu a architektura

- `src/ClickAssistant.Core` - čistá doménová logika (engine, modely, port rozhraní), bez závislostí na
  Windows/UI.
- `src/ClickAssistant.Infrastructure` - implementace portů nad SharpHook (globální vstup) a GDI (screenshoty).
- `src/ClickAssistant.App` - Avalonia UI (MVVM).
- `tests/ClickAssistant.Core.Tests` - xUnit testy nad `Core`.

Podrobný popis architektury, klíčových tříd a "gotchas" (věcí, které nejsou vidět na první pohled) je v
[`CLAUDE.md`](CLAUDE.md). `technicalExplanation.md` je navíc česky psaný výukový materiál k C#/.NET pro
někoho, kdo umí PHP.
