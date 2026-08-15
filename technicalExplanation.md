# AutoClicker – C# lekce a technická dokumentace pro PHP vývojáře

Tento dokument má dvě propojené role najednou:

1. **Kurz jazyka C#/.NET pro PHP vývojáře** – Část A. Neučí C# obecně "od nuly", ale přesně ty koncepty,
   které v této appce skutečně potkáš, vysvětlené vždy přes srovnání s PHP a s reálným úryvkem kódu z repa.
2. **Detailní referenční popis architektury a všech hlavních tříd** – Část B. Jak appka funguje, kde co je,
   proč je to takhle navržené, a kam se dívat, když něco nefunguje.

Doporučený postup čtení: Část A projdi jednou od začátku (je to lekce, buduje se koncept na koncept), Část B pak
používej spíš jako referenci – vracej se k jednotlivým kapitolám podle toho, kterou třídu zrovna zkoumáš.

Appka samotná: desktopový autoclicker. Uživatel nadefinuje sekvenci bodů na obrazovce (kliknutím), appka je pak
automaticky proklikává v nastaveném pořadí a intervalu, volitelně s "humanizovaným" (nedokonalým, lidsky
vypadajícím) pohybem myši, spouští se globální klávesovou zkratkou i mimo okno appky, a umí zobrazit reálný
screenshot okolí bodu, aby uživatel viděl, kam přesně klikne. Okno je plně responzivní (jde libovolně
zvětšovat/zmenšovat, viz kapitola B.13) a engine má zabudovanou obranu proti poškozeným/upraveným datům profilu
(viz kapitola B.15).

---
---

# ČÁST A – C# jako jazyk (kurz pro PHP vývojáře)

## A.0 Mentální mapa: čím se C#/.NET liší od PHP

Než půjdeme do detailů, pár zásadních rozdílů, které ovlivňují úplně všechno ostatní:

| | PHP | C# / .NET |
|---|---|---|
| **Spouštění** | Interpretovaný za běhu (nebo s opcache) | Kompilovaný do IL bajtkódu, ten pak JIT zkompilovaný do strojového kódu při startu |
| **Typování** | Dynamické (i s type hints je to hlavně run-time kontrola) | Statické – typy se kontrolují **při kompilaci**. Špatný typ = chyba buildu, appka se ani nespustí |
| **Autoload tříd** | Composer autoload (PSR-4, mapování namespace→cesta) | Není potřeba – celý projekt se zkompiluje najednou do jedné `.dll`/`.exe`, kompilátor vidí všechny třídy |
| **Balíčky** | Composer (`composer.json`, `vendor/`) | NuGet (`.csproj` `<PackageReference>`, balíčky v `~/.nuget/packages`) |
| **Vstupní bod** | `index.php` (nebo cokoliv, co web server nasměruje) | Metoda `Main` ve třídě s `[STAThread]`/bez, viz `Program.cs` (kapitola B.3) |
| **Životní cyklus** | Request-response, proces umírá po každém requestu (sdílený stav jen přes DB/cache/session) | Appka běží jako **jeden dlouho žijící proces** od spuštění po zavření – proměnné v paměti žijí, dokud je někdo nedrží |
| **Konfigurace typů** | `declare(strict_types=1)` je opt-in | Statické typování je vždy zapnuté, není co vypínat |

Poslední řádek je důležitý pro pochopení "pročto tak je": v PHP je běžné, že request přijde, appka se stateless
"probudí", udělá jednu věc a umře. V .NET desktopové appce **appka žije v paměti hodiny** – proto se tu tolik řeší
věci jako "na kterém vlákně běžím", "kdo drží referenci na tenhle objekt", "kdy se uvolní paměť" – v PHP webu tyhle
otázky prostě nikdy nevyvstanou, protože proces stejně za pár desítek milisekund skončí.

---

## A.1 Syntaxe a typy

### Proměnné a typy

```csharp
int count = 5;                  // explicitní typ
var name = "AutoClicker";        // 'var' = odvození typu při kompilaci (NENÍ to totéž co PHP dynamický typ!)
string status = "Nečinný";
```

`var` vypadá jako PHP proměnná bez typu, ale je to jen **zkratka pro zápis** – kompilátor za tebe typ odvodí
(`name` je pořád `string`, natvrdo, navždy) a od té chvíle se s ním chová úplně stejně staticky jako s explicitním
typem. `var name = "x"; name = 5;` je chyba kompilace, přesně jako `string name = "x"; name = 5;`.

### String interpolace

```csharp
var text = $"Krok {i + 1}";              // C# – $ prefix, {} uvnitř
```
```php
$text = "Krok " . ($i + 1);               // PHP bez interpolace výrazů
$text = "Krok {$arr['i']}";               // PHP interpolace jen pro proměnné/přístupy
```
V C# `$"..."` umí interpolovat **libovolný výraz** včetně volání metod, ne jen proměnné – blíž к JS template
literals (`` `Krok ${i+1}` ``) než k PHP `"..."`.

### Typy hodnotové vs. referenční

Tohle v PHP nemá přímou obdobu a je to důležité pro pochopení `record struct` v kapitole A.4:
- **Referenční typ** (`class`) – proměnná drží odkaz na objekt v paměti. Přiřazení `b = a` znamená "obě proměnné
  teď ukazují na stejný objekt" (podobné PHP objektům, které se taky předávají "podle handle").
  Skoro každá třída v appce (`ClickSequenceExecutor`, `MainWindowViewModel`, ...) je `class`.
- **Hodnotový typ** (`struct`) – proměnná drží přímo data, ne odkaz. Přiřazení `b = a` **zkopíruje hodnotu**.
  V appce to je `ScreenPoint` (`readonly record struct ScreenPoint(int X, int Y)`,
  `src/AutoClicker.Core/Models/ScreenPoint.cs`) a `MonitorBounds` – malé, často vytvářené dvojice čísel, kde
  kopírování je levnější než alokace objektu na haldě. Tohle PHP vůbec neřeší (skalární typy PHP se sice chovají
  hodnotově, ale nemáš možnost si vytvořit vlastní "malý objekt, co se chová jako číslo").

---

## A.2 Třídy a rozhraní

```csharp
public sealed class ClickSequenceExecutor
{
    private readonly IInputSimulator _simulator;

    public ClickSequenceExecutor(IInputSimulator simulator)
    {
        _simulator = simulator;
    }
}
```
Vs. PHP:
```php
final class ClickSequenceExecutor
{
    private readonly InputSimulatorInterface $simulator;

    public function __construct(InputSimulatorInterface $simulator)
    {
        $this->simulator = $simulator;
    }
}
```
Skoro 1:1 mapování slovo za slovem: `sealed` = `final` (nejde dědit), `private readonly` existuje v PHP 8.1+ taky
(`readonly` vlastnost). Rozdíly, na které narazíš:
- **Konstruktor bez `function`**: jméno třídy = konstruktor, žádné `__construct`.
- **`interface`** funguje stejně jako v PHP – `IInputSimulator` (`src/AutoClicker.Core/Engine/IInputSimulator.cs`)
  je čistě signatura metod, žádná implementace. Konvence v C# je prefix `I` (`IInputSimulator`,
  `IProfileRepository`), v PHP bys spíš viděl suffix `Interface`.
- **Žádné `implements` klíčové slovo** – dědičnost i implementace rozhraní se píšou stejně, za dvojtečkou:
  `class SharpHookInputSimulator : IInputSimulator` (jedna třída = jeden `class` k dědění + libovolně mnoho
  `interface` k implementaci, oddělené čárkou).
- **`sealed`** nemá v PHP přesnou obdobu mimo `final` – ale platí i pro celé třídy (`final class` v PHP je to samé).

### Přístupové modifikátory
`public`, `private`, `protected` fungují stejně jako PHP. Navíc C# má `internal` (viditelné jen uvnitř stejného
projektu/`.dll` – nemá PHP obdobu, nejblíž je "neexportovaný ze package" v jiných jazycích) – v AutoClickeru se
moc nepoužívá, projekty spolu komunikují přes `public` rozhraní.

---

## A.3 Vlastnosti (properties) – nejsou to fieldy, i když to tak vypadá

```csharp
public bool IsRunning => _cts is { IsCancellationRequested: false };
```
(`ClickSequenceExecutor.cs`, řádek 24). Tohle **vypadá** jako veřejná proměnná, ale je to **vlastnost** (property)
s `get` tělem napsaným jako výraz (`=>` = "expression-bodied member", zkrácený zápis pro `{ get { return ...; } }`).
Nejbližší PHP analogie je magická metoda:
```php
public function isRunning(): bool
{
    return $this->cts !== null && !$this->cts->isCancellationRequested();
}
```
...jenže v C# se to **volá jako pole** (`executor.IsRunning`, ne `executor.IsRunning()`), ne jako metoda. To je
zásadní rozdíl oproti PHP, kde `public bool $isRunning` a `public function isRunning()` jsou dvě naprosto odlišné
věci s odlišnou syntaxí volání. V C# `public bool IsRunning { get; set; }` (plnohodnotná vlastnost s uloženou
hodnotou) a `public bool IsRunning => vypočti();` (vlastnost počítaná za běhu) se volají **úplně stejně** –
`obj.IsRunning` – volající nepozná (a nemusí vědět) rozdíl.

Vlastnosti se čtou/zapisují takhle:
```csharp
public string Name { get; init; } = "Nový profil";   // get + init: nastavitelné jen při vytváření objektu
public string Name { get; set; }                       // get + set: nastavitelné kdykoliv
public bool IsRunning => ...;                           // jen get, žádné uložené pole, počítá se pokaždé znovu
```
`init` je jako `readonly` vlastnost v PHP 8.1, ale jde nastavit i přes object initializer syntax (`new Foo { Name
= "x" }`), ne jen v konstruktoru – uvidíš to všude u modelů (kapitola B.4).

---

## A.4 Records a immutabilita – `with` výraz

```csharp
public sealed record ClickProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Nový profil";
    public List<ClickPoint> Points { get; init; } = new();
}
```
`record` je speciální druh třídy (od C# 9) navržený pro **immutable datové objekty s hodnotovou rovností**.
Nejbližší PHP obdoba je "value object" vzor, který si v PHP musíš postavit ručně (readonly vlastnosti +
vlastní `equals()`), tady to dělá jazyk sám:
- Jakmile je objekt vytvořený, `init` vlastnosti už nejde měnit (na rozdíl od `set`).
- Dva recordy se stejnými hodnotami všech vlastností jsou `==` (na rozdíl od `class`, kde `==` porovnává identitu
  objektu v paměti, ne obsah – to je jako PHP `==` vs `===` u objektů, jen v C# je to `record` vs `class`, ne
  operátor).

Neměnitelnost by byla k ničemu, kdyby se pak nedalo pohodlně vytvořit "upravená kopie". Na to slouží `with`:
```csharp
return profile with { Points = rescaledPoints, CapturedScreenSnapshot = to };
```
(`ProfileRescaler.cs`) – vytvoří **novou instanci** `ClickProfile` se dvěma změněnými vlastnostmi, všechno
ostatní zkopíruje z originálu. Originál (`profile`) zůstává netknutý. V PHP bys tohle psal ručně:
```php
$new = clone $profile;
$new->points = $rescaledPoints;
$new->capturedScreenSnapshot = $to;
```
...jenže `clone` v PHP je defaultně **shallow copy** (vnořené objekty/pole se nekopírují, jen odkazy) a musíš si
hlídat `__clone()` magickou metodu ručně, pokud chceš hlubší kopii. `with` v C# generuje bezpečnou kopii
automaticky podle definice recordu.

Appka `with` používá všude, kde se "upravuje" profil za běhu (viz `MainWindowViewModel.OnResolutionChangedDuringRun`),
aby se zabránilo nechtěným vedlejším efektům ze sdílené reference.

---

## A.5 Nullable reference types a `null`-operátory

C# projekt v tomhle repu má zapnuté `<Nullable>enable</Nullable>` (viz kterýkoliv `.csproj`) – to znamená, že
**typ sám o sobě říká, jestli může být `null`**:
```csharp
private ClickProfile? _lastStartedProfile;   // '?' = smí být null
private readonly IInputSimulator _simulator;  // bez '?' = kompilátor předpokládá, že NIKDY není null
```
V PHP `?ClickProfile $x` (nullable type hint) dělá to samé, ale je to opt-in a kontroluje se jen za běhu. V C# s
nullable reference types kompilátor **staticky hlídá**, že se `_simulator` nikde nepoužije jako by mohl být
`null` (bez `?`), a naopak tě nutí `_lastStartedProfile` před použitím zkontrolovat. Je to nejbližší věc k PHP
statické analýze (PHPStan/Psalm), akorát vestavěná přímo do kompilátoru.

Operátory, na které narazíš doslova na každém druhém řádku:

| Operátor | Význam | PHP obdoba |
|---|---|---|
| `x?.Y` | Zavolej `.Y` jen pokud `x` není `null`, jinak vrať `null` | `$x?->y` (PHP 8.0+, stejná syntaxe!) |
| `x ?? y` | Pokud `x` je `null`, použij `y` | `$x ?? $y` (identické) |
| `x ??= y` | Pokud `x` je `null`, přiřaď mu `y` | `$x ??= $y` (identické, PHP 7.4+) |
| `x is { Prop: value }` | Pattern matching s kontrolou null i hodnoty najednou | nemá přímou obdobu |

Příklad z appky:
```csharp
CapturedScreenSnapshot ??= _screenInfoProvider.GetCurrentSnapshot();
```
(`ProfileEditorViewModel.AddPoint`) – "pokud ještě nemáme zachycený snapshot obrazovky, zachyť ho teď (jen
poprvé)". Přesně čitelné jako PHP ekvivalent.

```csharp
public bool IsRunning => _cts is { IsCancellationRequested: false };
```
`is { IsCancellationRequested: false }` znamená "`_cts` není `null` **a zároveň** jeho vlastnost
`IsCancellationRequested` je `false`" – jeden výraz nahrazuje `$this->cts !== null &&
!$this->cts->isCancellationRequested()`.

---

## A.6 Kolekce a generika

```csharp
List<ClickPoint> points = new List<ClickPoint>();     // = new() (typ se odvodí z levé strany)
Dictionary<Guid, ClickPoint> byId = points.ToDictionary(p => p.Id);
IReadOnlyList<HookKeyCode> modifiers = Array.Empty<HookKeyCode>();
```
`List<T>`, `Dictionary<TKey, TValue>` jsou **generické typy** – `<T>` je jako PHP by mohlo být, kdyby PHP pole
byla typovaná. Nejbližší PHP obdoba je obyčejné `array` (asociativní i indexované), ale s velkým rozdílem:
`List<ClickPoint>` **garantuje při kompilaci**, že v něm nikdy nebude nic jiného než `ClickPoint` – žádné
`array_push($list, "omylem string")`, které v PHP projde a spadne až za běhu (nebo nespadne vůbec a jen ti to
rozbije logiku o pár řádků dál).

`IReadOnlyList<T>` je rozhraní pro "jen ke čtení" verzi seznamu – appka ho používá v parametrech metod (např.
`IPointOrderStrategy.GetOrder(IReadOnlyList<ClickPoint> points, ...)`), aby bylo jasné "tahle metoda seznam bodů
jen čte, nebude ho měnit". PHP nemá typovanou obdobu immutable pole – nejblíž je konvence "nepředávej referenci"
nebo `readonly` vlastnost pole (PHP 8.1, ale pořád mutable obsah).

---

## A.7 LINQ – funkcionální práce s kolekcemi

LINQ (Language Integrated Query) jsou metody na kolekcích, přímá obdoba PHP `array_*` funkcí, ale jako **metody
volané řetězově** místo volných funkcí:

| C# LINQ | PHP obdoba |
|---|---|
| `points.Select(p => p.Name)` | `array_map(fn($p) => $p->name, $points)` |
| `points.Where(p => p.ClickCount > 1)` | `array_filter($points, fn($p) => $p->clickCount > 1)` |
| `points.OrderBy(p => p.Id)` | `usort($points, fn($a,$b) => $a->id <=> $b->id)` |
| `points.FirstOrDefault(p => p.Id == id)` | jeden prvek z `array_filter(...)[0] ?? null` |
| `points.Count(p => ...)` | `count(array_filter(...))` |
| `points.ToList()` / `.ToDictionary(...)` | přetypování na pole / asociativní pole |
| `points.Sum(p => p.ClickCount)` | `array_sum(array_map(...))` |

Příklad přímo z appky (`ProfileRescaler.cs`):
```csharp
var rescaledPoints = profile.Points
    .Select(p => p with { Location = RescalePoint(p.Location, from, to) })
    .ToList();
```
Čti zprava doleva/shora dolů jako pipeline: "vezmi `profile.Points`, na každý bod aplikuj přepočet pozice (vytvoř
kopii s `with`), a výsledek slij zpátky do `List<T>`". `p => p with { ... }` je **lambda výraz** (viz A.8) –
anonymní funkce zapsaná inline.

`Select`/`Where`/atd. jsou tzv. **lazy** (línivé) – nevykonají se hned, ale až se přes výsledek iteruje (např.
`foreach` nebo zavoláním `.ToList()`). To je koncepčně podobné PHP generátorům (`yield`), ne obyčejným polím.

---

## A.8 Delegáti, `Action`/`Func`, eventy

### Lambda výrazy a delegáti
```csharp
Action<ScreenPoint> callback = point => Console.WriteLine(point.X);
Func<ClickPoint, bool> predicate = p => p.ClickCount > 1;
```
`Action<T>` = funkce, co nic nevrací (bere `T`, vrací `void`). `Func<T, TResult>` = funkce, co něco vrací
(poslední typový parametr je vždy návratový typ). Nejbližší PHP obdoba je `callable`/`Closure`:
```php
$callback = function (ScreenPoint $point) { echo $point->x; };  // nebo fn($point) => ...
```
V C# jsou `Action`/`Func` **typované** – kompilátor přesně ví, kolik parametrů a jakého typu funkce bere/vrací,
takže špatný počet argumentů = chyba buildu, ne runtime `TypeError`.

### Eventy – vestavěný pub/sub do jazyka

```csharp
public event EventHandler<EngineStatusEventArgs>? StatusChanged;
...
StatusChanged?.Invoke(this, new EngineStatusEventArgs(status));   // "vystřelení" eventu
```
(`ClickSequenceExecutor.cs`). `event` je jazyková konstrukce pro **observer pattern**, který bys v PHP musel
postavit ručně (vlastní `EventEmitter`/Symfony `EventDispatcher`/pole callbacků + `addListener()` metoda). V C#
to jazyk dává zdarma:
```csharp
executor.StatusChanged += (sender, e) => { /* reaguj na změnu stavu */ };   // přihlášení k odběru (+=)
executor.StatusChanged -= handler;                                          // odhlášení (-=)
```
`?.Invoke(...)` = "pokud má event alespoň jednoho odběratele (není `null`), zavolej všechny" – kombinace
null-conditional operátoru (A.5) s vystřelením eventu, protože event bez odběratelů je `null`.

Appka to používá k tomu, aby `ClickSequenceExecutor` (běžící na worker vlákně, viz A.9 a kapitola B.5) mohl
informovat `MainWindowViewModel` o změně stavu, aniž by o něm cokoliv věděl – engine nemá žádnou referenci
zpátky na ViewModel, jen "vystřelí" event do vzduchu a kdo poslouchá, zareaguje. To je přesně Dependency
Inversion aplikovaný na komunikaci, ne jen na implementace (kapitola B.1).

---

## A.9 `async`/`await` – nejdůležitější a nejcizejší koncept pro PHP vývojáře

PHP až do fiber (8.1+) prakticky nemá nativní konkurenci v jednom procesu – požadavky se paralelizují **mezi
procesy/workery** (PHP-FPM, Swoole), ne uvnitř jednoho. C#/.NET desktopová appka běží jako **jeden proces s
mnoha vlákny** a `async`/`await` je nástroj, jak psát kód, který **čeká** na něco pomalého (I/O, časovač), aniž
by zablokoval vlákno, které čeká.

Nejbližší mentální model, pokud znáš JavaScript: `async`/`await` v C# je **skoro identické** s JS
`async`/`await` a `Promise` – `Task` v C# ≈ `Promise` v JS.

```csharp
public async Task<IReadOnlyList<ClickProfile>> LoadAllAsync()
{
    var profiles = new List<ClickProfile>();
    foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
    {
        await using var stream = File.OpenRead(file);
        var profile = await JsonSerializer.DeserializeAsync<ClickProfile>(stream, SerializerOptions);
        if (profile is not null) profiles.Add(profile);
    }
    return profiles;
}
```
(`JsonProfileRepository.cs`). Čti to takhle:
- `async Task<T>` v hlavičce metody = "tahle metoda je asynchronní a nakonec vrátí `T`" (jako `async function
  foo(): Promise<T>` v JS/TS).
- `await` před voláním = "počkej, až tohle dokončí, ale **neblokuj vlákno** – uvolni ho pro jinou práci, dokud
  operace neskončí, pak pokračuj přesně odsud".
- Volající (`MainWindowViewModel.InitializeAsync`) musí sám být `async` a `await`-ovat volání – "asynchronnost
  se táhne nahoru celým řetězcem volání" (stejné pravidlo jako v JS: `async` funkce voláš přes `await` z jiné
  `async` funkce).

### Kde to je jinak než JS: `Task.Run` a vlákna z thread poolu

```csharp
_ = Task.Run(() => RunLoopAsync(profile, token), token);
```
(`ClickSequenceExecutor.StartAsync`). JavaScript je jednovláknový (event loop), takže `async`/`await` tam nikdy
neřeší "na kterém vlákně běžím". C# je **vícevláknové** – `Task.Run(...)` spustí danou práci na **vlákně z thread
poolu** (skupina vláken spravovaná runtime, aby se nemusela pořád vytvářet nová OS vlákna). To je důvod, proč
appka tolik řeší `Dispatcher.UIThread.Post(...)` (viz kapitola B.5/B.11) – kód spuštěný přes `Task.Run` běží na
**jiném vlákně** než UI, a UI framework (Avalonia) nedovolí měnit vlastnosti navázané na obrazovku z jiného
vlákna než toho, které UI vykresluje.

`_ = Task.Run(...)` – podtržítko je "zahoď návratovou hodnotu, vím, že na ni nečekám, není to chyba" (jinak
kompilátor varuje). Tomu se říká **fire-and-forget** a je to výjimka z pravidla "vždy `await`uj Task" – detailní
vysvětlení proč přesně tady je potřeba, viz kapitola B.5.1.

### `CancellationToken` – kooperativní rušení

.NET nemá `pcntl_kill`/"zabij vlákno silou". Místo toho `CancellationToken`:
```csharp
public void Stop() => _cts?.Cancel();
...
while (!token.IsCancellationRequested) { ... token.ThrowIfCancellationRequested(); ... }
```
`Cancel()` jen nastaví příznak "je požadováno zrušení" – běžící kód si ho musí sám pravidelně kontrolovat
(`ThrowIfCancellationRequested()` vyhodí výjimku, pokud je příznak nastavený) a **spolupracovat** na vlastním
ukončení. Není to preemptivní zabití, je to zdvořilá žádost, kterou kód musí sám respektovat. Detaily v
kapitole B.5.2.

---

## A.10 Pattern matching

C# `switch` umí mnohem víc než PHP `match`/`switch`:
```csharp
private static MouseButton ToSharpHook(MouseButtonType button) => button switch
{
    MouseButtonType.Left => MouseButton.Button1,
    MouseButtonType.Right => MouseButton.Button2,
    MouseButtonType.Middle => MouseButton.Button3,
    _ => throw new ArgumentOutOfRangeException(nameof(button))
};
```
Tohle je **switch výraz** (ne příkaz) – rovnou vrací hodnotu, žádné `break`, žádné padání skrz (fall-through).
Nejblíž PHP `match`:
```php
$result = match ($button) {
    MouseButtonType::Left => MouseButton::Button1,
    MouseButtonType::Right => MouseButton::Button2,
    MouseButtonType::Middle => MouseButton::Button3,
    default => throw new \OutOfRangeException(),
};
```
Skoro identická syntaxe i sémantika (PHP `match` byl ostatně inspirovaný podobnými jazyky). `_` v C# = `default`
v PHP `match`.

C# navíc umí matchovat **strukturu a typ**, ne jen hodnotu – to PHP `match` neumí vůbec:
```csharp
if (_screenInfoProvider is null || profile.CapturedScreenSnapshot is not { } captured) return false;
```
`is not { } captured` = "pokud `CapturedScreenSnapshot` **není** non-null hodnota (a pokud je, ulož ji do nové
proměnné `captured`)". Jeden výraz nahrazuje `null`-check i přiřazení do proměnné najednou.

---

## A.11 Atributy `[...]` a source generátory

```csharp
[ObservableProperty]
private string _statusText = "Nečinný";
```
`[ObservableProperty]` je **atribut** – metadata připíchnutá k poli/metodě/třídě. PHP 8.0+ má identickou
syntaxi i koncept: `#[Attribute]`. Rozdíl je v tom, co se s atributem děje:

- V PHP se atributy typicky čtou **za běhu přes reflection** (např. Symfony routing `#[Route(...)]` – framework
  za běhu prochází třídy a reaguje na atributy).
- V AutoClickeru (knihovna **CommunityToolkit.Mvvm**) se `[ObservableProperty]` zpracovává **při kompilaci**
  pomocí tzv. **source generátoru** – nástroj, který za tebe vygeneruje další C# kód ještě před buildem. Z
  privátního pole `_statusText` vygenerátor vyrobí veřejnou vlastnost:
  ```csharp
  public string StatusText
  {
      get => _statusText;
      set { _statusText = value; OnPropertyChanged(); }  // + vystřelení PropertyChanged eventu
  }
  ```
  Tenhle vygenerovaný kód **skutečně existuje** (najdeš ho v `obj/` složce po buildu jako `.g.cs` soubor), jen
  ho nepíšeš ručně. Je to podobné, jako by ti Composer/PHP nástroj při `composer dump-autoload` vygeneroval
  hotové gettery/settery ze seznamu polí – jenže to PHP nedělá, tohle je čistě .NET věc.

Podobně `[RelayCommand]`:
```csharp
[RelayCommand]
private async Task StartAsync() { ... }
```
vygeneruje veřejnou vlastnost `StartCommand` typu `ICommand`, kterou UI naváže na tlačítko
(`Command="{Binding StartCommand}"`). Víc v kapitole B.2.

**Proč si toho všímat:** Když v kódu appky vidíš `StatusText` nebo `StartCommand` použité, ale v souboru najdeš
jen `_statusText`/metodu `StartAsync()`, nehledej `StatusText` doslovně v textu souboru – je vygenerovaná.

---

## A.12 Enumy

```csharp
public enum RepeatMode
{
    Once,
    FixedCount,
    Infinite
}
```
Skoro identické s PHP 8.1+ enums (`enum RepeatMode { case Once; case FixedCount; case Infinite; }`). Rozdíl: C#
enum je pod kapotou celé číslo (`Once` = 0, `FixedCount` = 1, ...) pokud si výslovně neřekneš jinak, zatímco PHP
"pure enum" nemá skalární hodnotu vůbec (musel bys použít "backed enum" `enum RepeatMode: int`). Appka ukládá
enumy do JSONu jako čitelný text (`"Infinite"`, ne `2`) díky `JsonStringEnumConverter` (kapitola B.10) – bez
téhle konfigurace by `System.Text.Json` defaultně serializoval čísla, což by byl ekvivalent PHP backed enum s
`int` hodnotami místo `string`.

---

## A.13 `namespace`/`using` vs. PHP `namespace`/`use`

```csharp
namespace AutoClicker.Core.Engine;   // C# 10+ "file-scoped" namespace – platí pro celý zbytek souboru

using AutoClicker.Core.Models;        // import jiného namespace
```
Prakticky identické s PHP:
```php
namespace AutoClicker\Core\Engine;
use AutoClicker\Core\Models\ClickPoint;
```
Zásadní rozdíl: **C# nepotřebuje autoload**. V PHP `use` řeší i to, odkud se soubor s třídou vůbec načte
(Composer PSR-4 mapování namespace → cesta k souboru). V C# se celý projekt kompiluje najednou – `using` jen
zpřístupní kratší jména typů (`ClickPoint` místo `AutoClicker.Core.Models.ClickPoint`), nijak neřeší "odkud se
to nahraje", protože všechno je už dávno v jedné zkompilované `.dll`.

---

## A.14 Výjimky – `try`/`catch`/`finally`

Prakticky identické s PHP:
```csharp
try
{
    ...
}
catch (OperationCanceledException)      // catch (\Throwable $e) v PHP – tady chytáme konkrétní typ
{
    // očekávané při Stop()
}
finally
{
    RaiseStatus(EngineStatus.Stopped);   // vždy se provede, i po zachycené výjimce, i po 'return'
}
```
(`ClickSequenceExecutor.RunLoopAsync`). Jediný rozdíl od PHP syntaxe: v C# **musíš** typ výjimky uvést (nejde
`catch { ... }` bez typu – nejblíž je `catch (Exception) { }`, obdoba PHP `catch (\Throwable $e)`). Filtrování
podle konkrétního typu (`catch (JsonException)` v `JsonProfileRepository.LoadAllAsync`, viz kapitola B.15) je v
obou jazycích stejné – chytí se jen ten typ a jeho podtřídy, jiné výjimky proletí dál.

---

## A.15 Extension methods (jen k rozpoznání, appka je nedefinuje vlastní)

```csharp
profile.Points.Select(p => p.Name)   // .Select() není metoda na List<T>!
```
`Select`, `Where`, `OrderBy` atd. (LINQ, kapitola A.7) nejsou metody definované na `List<T>` – jsou to
**extension methods**, C# mechanismus, jak "přidat" metodu k cizímu typu, aniž bys ho upravoval. Nejbližší PHP
obdoba neexistuje přímo (PHP nemá jak "přidat metodu" k `array`), nejblíž je asi trait, ale ten musí být do
třídy explicitně `use`-nutý, kdežto extension metoda funguje automaticky, jen musíš mít `using
System.Linq;` nahoře v souboru. Stačí to vědět, že když voláš metodu na typu, který ji "neměl by mít", je to
pravděpodobně extension method (skoro vždy z `System.Linq`).

---

## A.16 Rychlá referenční tabulka PHP ↔ C#

| Co potřebuješ | PHP | C# |
|---|---|---|
| Balíčky | Composer, `composer.json` | NuGet, `.csproj` `<PackageReference>` |
| Autoload | PSR-4 přes Composer | Není potřeba (celý projekt = jedna kompilace) |
| Immutable objekt | `readonly` vlastnosti (8.1+), ruční `clone` | `record` + `with` výraz |
| Null-safety | `?Type`, `?->`, `??`, PHPStan/Psalm (opt-in) | `Type?`, `?.`, `??`, vestavěné do kompilátoru |
| `array_map`/`array_filter`/`usort` | Volné funkce | `.Select()`/`.Where()`/`.OrderBy()` (LINQ) |
| Anonymní funkce | `fn($x) => ...`, `function($x) { ... }` | `x => ...` (lambda) |
| Observer/eventy | Ruční implementace / Symfony EventDispatcher | `event`, `+=`/`-=` vestavěné do jazyka |
| Async I/O | Fibers (8.1+), ReactPHP, Swoole | `async`/`await`, `Task` (vestavěné, mainstream) |
| Atributy metadat | `#[Attribute]`, čtou se přes reflection za běhu | `[Attribute]`, často zpracované **při kompilaci** (source generátory) |
| Enum | `enum X { case A; }` (8.1+) | `enum X { A, B }` (odjakživa, pod kapotou `int`) |
| Testovací framework | PHPUnit | xUnit |
| Mock knihovna | Mockery/Prophecy | NSubstitute |
| Testovací DB/temp | `setUp()`/`tearDown()` | konstruktor testovací třídy + `IDisposable.Dispose()` |
| DI kontejner | Symfony/Laravel service container | V týhle appce žádný – ruční "composition root" (kapitola B.1) |

---
---

# ČÁST B – Architektura a hlavní třídy AutoClickeru

## B.1 Architektura – 4 projekty a proč jsou oddělené

Řešení (`AutoClicker.sln`, obdoba `composer.json` + workspace na úrovni celého repa) obsahuje čtyři C# projekty
(`.csproj` = obdoba `composer.json` pro jeden balíček/modul):

```
src/AutoClicker.Core           – čistá doména a business logika, ŽÁDNÉ závislosti na Windows/Avalonii
src/AutoClicker.Infrastructure – konkrétní implementace (SharpHook, GDI, JSON soubory na disku)
src/AutoClicker.App            – Avalonia UI (ViewModels + Views), skládá vše dohromady
tests/AutoClicker.Core.Tests   – xUnit testy (obdoba PHPUnit), 82 testů
```

Závislosti (`ProjectReference` v `.csproj`, obdoba `require` v `composer.json`) jdou jen jedním směrem:

```
AutoClicker.App  ──▶  AutoClicker.Infrastructure  ──▶  AutoClicker.Core
       └──────────────────────▶  AutoClicker.Core
```

`AutoClicker.Core.csproj` nemá žádný `PackageReference` – je to čisté C#, žádný framework. To je záměr: Core
definuje **rozhraní** (viz A.2) jako `IInputSimulator`, `IGlobalInputListener`, `IScreenCaptureProvider`,
`IScreenInfoProvider`, `IProfileRepository` a k nim algoritmy, které fungují čistě na datech (Bézier křivka,
jitter, řazení bodů). Nic z toho neví, že běží na Windows, ani že existuje SharpHook knihovna.

`AutoClicker.Infrastructure` je vrstva, která tato rozhraní **implementuje** konkrétními technologiemi:
- `SharpHookGlobalListener` / `SharpHookInputSimulator` – knihovna SharpHook (nízkoúrovňové OS hooky na klávesnici/myši)
- `WindowsScreenCaptureProvider` – `System.Drawing` (GDI, jen Windows)
- `JsonProfileRepository` – ukládání profilů jako JSON soubory na disk

Tohle je klasický **Dependency Inversion** / port-adapter pattern – v PHP světě podobné tomu, jak Symfony/Laravel
definují `interface` pro `MailerInterface` a pak mají `SmtpMailer`, `SesMailer` jako implementace. Rozdíl je, že
tady **není žádný DI kontejner** (žádný Laravel `app()->make()` nebo Symfony service container) – `.csproj` sice
referencuje `Microsoft.Extensions.DependencyInjection`, ale nikde se nepoužívá. Místo toho se všechny objekty ručně
sestaví (`new ...`) na jednom místě – v konstruktoru `MainWindow`
(`src/AutoClicker.App/MainWindow.axaml.cs`, řádky 20–47). Tomu se říká
**composition root** – jediné místo v celé appce, kde se rozhoduje "která konkrétní implementace se použije".
Pokud budeš hledat, odkud se bere např. `SharpHookInputSimulator` nebo `WindowsScreenCaptureProvider`, hledej
právě tady, ne v nějakém `Startup.cs` nebo konfiguračním XML.

`AutoClicker.App` je UI vrstva (Avalonia = cross-platformní GUI framework, takové Electron/Qt pro .NET). Obsahuje
`ViewModels/` (logika stavu obrazovky) a `Views/` (`.axaml` soubory = deklarativní XML popis UI, obdoba
Blade/Twig šablon, jen v XML místo v `{{ }}` syntaxi).

**Proč takhle rozdělené?** Hlavní důvod je testovatelnost a přenositelnost: `AutoClicker.Core.Tests` testuje
engine (`ClickSequenceExecutor`) přes mock `IInputSimulator` (knihovna NSubstitute, obdoba PHP Mockery/Prophecy) –
testy vůbec nepotřebují Windows, skutečnou myš ani Avalonia okno. Kdyby chtěl někdo appku portovat na
Linux/macOS, stačí nahradit jen `Infrastructure` vrstvu (a `WindowsScreenCaptureProvider`, který je explicitně
Windows-only, viz kapitola B.9).

---

## B.2 MVVM pattern – jak funguje UI

Avalonia používá **MVVM** (Model-View-ViewModel). Zjednodušená analogie na web:

| MVVM | Web/PHP analogie |
|---|---|
| **View** (`.axaml`) | Blade/Twig šablona – deklarativně popisuje, co se zobrazí a na co je napojené |
| **ViewModel** (`.cs` s `ObservableObject`) | Něco mezi Controllerem a "reaktivním" JS stavem (Vue/Alpine `data()`) – drží stav obrazovky a příkazy |
| **Model** (`AutoClicker.Core.Models`) | Doménové objekty/DTO, jako Eloquent modely bez ORM chování |
| **Binding** (`{Binding X}`) | Obousměrné propojení: `<input>` v šabloně + JS listener, co drží hodnotu v syncu – ale řeší to framework, ne ty ručně |

### B.2.1 Jak vypadá jeden `.axaml` soubor

Např. `src/AutoClicker.App/MainWindow.axaml` je XML soubor popisující okno.
Ke každému `.axaml` existuje "code-behind" soubor `.axaml.cs` (`MainWindow.axaml.cs`) – to je **částečná třída**
(`partial class`, C# umožňuje rozdělit jednu třídu do víc souborů, viz A.2). Jedna polovina
(`InitializeComponent()`) se vygeneruje automaticky ze XML při buildu, druhá polovina je ruční C# kód. To je
koncepčně podobné, jako kdyby Blade šablona měla svůj vlastní PHP soubor s metodami volanými přímo z `<script>` –
jen tady je to build-time propojené a typované.

Klíčový řádek v každém `.axaml`:
```xml
x:DataType="vm:MainWindowViewModel"
```
Tohle říká Avalonii "tenhle View je napojený na tenhle ViewModel typ" a umožňuje to **compiled bindings** –
binding výrazy jako `{Binding StatusText}` se ověřují už při kompilaci (typo v názvu vlastnosti = chyba buildu,
ne tichá chyba za běhu jako v běžném Twig `{{ $typo }}`).

### B.2.2 ViewModel – `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`

Všechny ViewModely (`MainWindowViewModel`, `ProfileEditorViewModel`, `SequenceStepViewModel`,
`MapMonitorRectViewModel`) dědí z `ObservableObject` (knihovna **CommunityToolkit.Mvvm**, viz
`AutoClicker.App.csproj`, `PackageReference Include="CommunityToolkit.Mvvm"`). Mechanismus `[ObservableProperty]`
a `[RelayCommand]` je vysvětlený jazykově v kapitole A.11 (atributy + source generátory) – tady jen konkrétní
použití:

```csharp
[ObservableProperty]
private string _statusText = "Nečinný";
```
(`MainWindowViewModel.cs`, řádek 34) vygeneruje veřejnou vlastnost `StatusText`, která při změně automaticky
vystřelí `PropertyChanged` event. Avalonia binding na to naslouchá a přemaluje UI – v PHP web světě je to
reaktivita Livewire/Vue/Alpine ("změň proměnnou, DOM se sám updatuje"), jen tady to dělá C# atribut + generátor
kódu při kompilaci, žádný JS runtime.

```csharp
[RelayCommand]
private async Task StartAsync() { ... }
```
`[RelayCommand]` (viz `MainWindowViewModel.cs` řádek 127 aj.) vygeneruje veřejnou vlastnost `StartCommand` typu
`ICommand`, na kterou se v XAML napojuje `Command="{Binding StartCommand}"` (viz `MainWindow.axaml` řádek 35).
To je obdoba routy/akce v controlleru navázané na tlačítko – jen bez HTTP requestu, přímé volání metody v paměti.

**Na co si dát pozor:** Když v kódu vidíš `Steps.Count` nebo `StatusText`, ale v souboru je definované jen
`_steps`/`_statusText`, nehledej `StatusText` – je to generovaná vlastnost, kterou nenajdeš plným textem v
souboru (žádný `.g.cs` soubor obvykle neuvidíš v editoru, generuje se do `obj/` složky při buildu). Pokud build
hlásí chybu na řádku s `[ObservableProperty]`, může jít o kolizi jména (např. veřejná vlastnost `Name` už
existuje jinde).

### B.2.3 DI kontejner – proč tu chybí a jak se to řeší

Na rozdíl od Laravel/Symfony service containeru, tahle appka **nemá** registr služeb ani `Program.cs` s
`services.AddSingleton<...>()`. Composition root je přímo v `MainWindow` konstruktoru:

```csharp
var executor = new ClickSequenceExecutor(new SharpHookInputSimulator(), screenInfoProvider: screenInfoProvider);
var viewModel = new MainWindowViewModel(
    new JsonProfileRepository(), executor, _globalListener, screenInfoProvider, new WindowsScreenCaptureProvider())
```
(`MainWindow.axaml.cs`, řádky 27–37). `MainWindowViewModel` pak sám ručně vytváří `ProfileEditorViewModel` a
předává mu potřebné závislosti (`MainWindowViewModel.cs`, řádek 57). To je "manuální DI" – funguje to, protože
appka má jen jeden hlavní graf objektů. Pokud budeš přidávat novou službu, budeš ji ručně `new`-ovat a protahovat
konstruktory přesně tady.

---

## B.3 Start appky – tok od spuštění po zobrazení okna

1. `Program.cs` – vstupní bod (`Main`, obdoba `index.php`, ale kompilovaný do `.exe`). Sestaví `AppBuilder` a
   spustí desktop lifecycle.
2. `App.axaml.cs` (`OnFrameworkInitializationCompleted`) – vytvoří `new MainWindow()` a nastaví jako hlavní okno.
3. `MainWindow` konstruktor – sestaví composition root (viz B.1), nastaví `DataContext = viewModel` (to je to,
   na co se všechny `{Binding ...}` v `MainWindow.axaml` odkazují), založí tray ikonu, spustí globální
   listener (`_globalListener.Start()`), a při události `Opened` (okno se poprvé zobrazí) zavolá
   `await viewModel.InitializeAsync()` – asynchronně načte profily z disku.
4. `MainWindowViewModel.InitializeAsync()` (`MainWindowViewModel.cs`, řádek 75) načte profily přes
   `IProfileRepository.LoadAllAsync()`, naplní `Profiles` kolekci a buď vybere první profil, nebo založí nový.

**Pozor na zavírání okna:** `MainWindow` přepisuje chování zavřením křížkem – `OnWindowClosing` (řádky 87–92)
zruší zavření (`e.Cancel = true`) a jen okno schová (`Hide()`), pokud `_allowClose` není nastavené. Appka tedy
"běží na pozadí" v tray ikoně a skutečně se ukončí jen přes položku "Konec" v tray menu (`_allowClose = true;
Close();`, řádky 63–65). Pokud appka "nejde zavřít", tohle je důvod – není to bug, je to záměr (autoclicker má
běžet na pozadí).

---

## B.4 Doménové modely (`AutoClicker.Core/Models`)

Skoro všechny modely jsou `record` (viz A.4) – `ClickProfile`, `ClickPoint`, `TimingConfig`, `HumanizationConfig`,
`HotkeyConfig`, `ScreenSnapshot`. Immutabilita se využívá přes vzor `with` (`ProfileRescaler.cs`, A.4).

Klíčové modely:
- **`ClickPoint`** (`ClickPoint.cs`) – jeden bod: pozice (`ScreenPoint`), tlačítko myši, počet kliků,
  volitelný vlastní delay po kliknutí (`DelayAfterMsOverride`, `null` = použij globální interval z profilu).
- **`ClickProfile`** (`ClickProfile.cs`) – celý profil: seznam bodů, pořadí (`OrderMode` + volitelný
  `CustomOrder` seznam ID), `TimingConfig`, `HumanizationConfig`, start/stop hotkey, a `CapturedScreenSnapshot`
  (rozlišení obrazovky v době, kdy byly body nadefinované – klíčové pro detekci změny rozlišení, kapitola B.8).
- **`ScreenPoint`** – `readonly record struct` (hodnotový typ, viz A.1 – malá immutable dvojice X/Y,
  efektivnější než `record class` pro časté vytváření v cyklu).
- **`HookKeyCode`** (`HookKeyCode.cs`) – **záměrně vlastní enum** nezávislý na SharpHook knihovně. Komentář v
  kódu to vysvětluje: "aby Core nemusel záviset na SharpHooku". Mapování na skutečné `SharpHook.Data.KeyCode` se
  děje až v `Infrastructure` (`SharpHookGlobalListener.ToSharpHook`). Tohle je přesně ten port/adapter princip
  z kapitoly B.1 aplikovaný na jeden konkrétní enum.

---

## B.5 Klikací engine – `ClickSequenceExecutor`

Soubor: `src/AutoClicker.Core/Engine/ClickSequenceExecutor.cs`. Toto je srdce appky – smyčka, která prochází
body profilu a kliká. Jazykové základy (`async`/`await`, `Task`, `CancellationToken`) jsou vysvětlené v A.9 –
tady konkrétní aplikace na tuhle třídu.

### B.5.1 Proč je `StartAsync` "fire-and-forget"

```csharp
public Task StartAsync(ClickProfile profile)
{
    if (IsRunning || profile.Points.Count == 0) return Task.CompletedTask;
    _cts = new CancellationTokenSource();
    var token = _cts.Token;
    _ = Task.Run(() => RunLoopAsync(profile, token), token);
    return Task.CompletedTask;
}
```
`StartAsync` se **nečeká** na dokončení klikací smyčky – spustí ji na vlákně z thread poolu (`Task.Run`) a hned
vrátí. Je to nutné, protože klikací smyčka může běžet donekonečna (`RepeatMode.Infinite`) nebo dlouho – kdyby
`StartAsync` čekal na její dokončení (`await RunLoopAsync(...)`), zablokoval by to, kdo ho volá. V UI vrstvě je
volán z `[RelayCommand] StartAsync()` v `MainWindowViewModel`, který běží na UI vlákně – kdyby se tam čekalo na
dokončení celé klikací smyčky, **zamrzlo by celé okno appky** (žádné UI vlákno by nezpracovávalo klávesy/myš/
překreslení), dokud by nedoběhl klikání. Proto se `Task.Run` odpojí od volajícího a komunikuje zpátky jen přes
eventy (`StatusChanged`, `PointClicked`, `ResolutionChangedDuringRun`, viz A.8).

### B.5.2 `Stop()` a cancellation token

```csharp
public void Stop() => _cts?.Cancel();
```
Detailní vysvětlení principu je v A.9 ("kooperativní rušení"). V praxi: `Stop()` jen nastaví příznak – smyčka
doběhne aktuální krok a při nejbližší kontrole (`token.ThrowIfCancellationRequested()`, řádky 79, 129, 141)
vyhodí `OperationCanceledException`, který se odchytí (`catch (OperationCanceledException)`, s komentářem
"očekávané při Stop()") a smyčka se ukončí v `finally` bloku, který vždy vystřelí `StatusChanged` s
`EngineStatus.Stopped`. **Pozor:** `Stop()` tedy není okamžité – klik uprostřed provádění
(`MouseDown`→delay→`MouseUp`) se dokončí, než se smyčka zastaví.

### B.5.3 Hlavní smyčka `RunLoopAsync`

Pro každý cyklus:
1. `PointOrderStrategyFactory.Create(profile.OrderMode)` vybere strategii řazení bodů (viz kapitola B.6).
2. U `RandomNoImmediateRepeat` navíc speciální logika (řádky 64–68), aby se náhodou nezopakoval stejný bod hned
   po sobě mezi dvěma cykly.
3. **Obranná kontrola prázdného pořadí** (řádky 70–75, viz kapitola B.15) – pokud `CustomOrder` odkazuje jen na
   neexistující body, smyčka se krátce zdrží místo aby se donekonečna točila naprázdno.
4. Pro každý bod: `ExecuteSinglePointAsync` (pohyb myši + kliknutí), vystřelí `PointClicked` event, spočítá
   delay do dalšího bodu (`TimingJitter.Compute`, viz kapitola B.7) a čeká (`CancellableDelay`).
5. Po dokončení cyklu zkontroluje `RepeatMode` (Once/FixedCount/Infinite) a `CheckResolutionChanged` – pokud se
   od zachycení bodů změnilo rozlišení obrazovky, smyčka se přeruší a vystřelí se
   `ResolutionChangedDuringRun` (viz kapitola B.8).

`ExecuteSinglePointAsync` (řádky 117–149): pokud je humanizace zapnutá, aplikuje `PositionJitter` na cílovou
pozici a případně vygeneruje Bézier trajektorii (`_movementGenerator.GeneratePath`) a prochází ji krok po kroku
s vlastním delayem na krok. Jinak jen `_simulator.MoveMouse(target)` – okamžitý skok. Kliknutí je vždy
`MouseDown` → 50ms delay → `MouseUp` (natvrdo, ne konfigurovatelné), opakované `point.ClickCount`-krát s 80ms
mezerou mezi kliky.

### B.5.4 Eventy a jak se propojují s UI

`ClickSequenceExecutor` má tři eventy (viz A.8): `StatusChanged`, `PointClicked`, `ResolutionChangedDuringRun`.
Vznikají na **worker vlákně** (uvnitř `Task.Run`), ne na UI vlákně! `MainWindowViewModel` se na ně přihlašuje v
konstruktoru (řádky 53–54) a v handlerech vždy volá `Dispatcher.UIThread.Post(...)`
(`MainWindowViewModel.cs`, `OnStatusChanged`, `OnResolutionChangedDuringRun`) – to je nutné, protože Avalonia
(stejně jako většina GUI frameworků) nedovolí měnit vlastnosti navázané na UI z jiného vlákna než UI vlákna
(viz A.9, sekce o `Task.Run` a vláknech).
Pokud bys při debugování narazil na "chyba, změna vlastnosti mimo UI vlákno" nebo podivné zamrznutí, tohle je
první místo, kam se podívat – chybějící `Dispatcher.UIThread.Post`.

---

## B.6 Pořadí bodů – Strategy pattern (`PointOrderStrategies`)

`src/AutoClicker.Core/Engine/PointOrderStrategies/`. Klasický **Strategy pattern** (v PHP bys měl `interface
PointOrderStrategy` a několik tříd, co ho implementují – tady přesně totéž): `IPointOrderStrategy.GetOrder(points,
customOrder, rng)`.

- `SequentialOrderStrategy` – vrátí body v pořadí, jak jsou v seznamu.
- `CustomOrderStrategy` – seřadí podle `CustomOrder` (seznam GUID), použije se, když uživatel v UI ručně
  přeuspořádá kroky (šipky nahoru/dolů v `SequenceTimelineView`). **Pozor na okrajový případ:** pokud
  `CustomOrder` obsahuje ID, které mezi aktuálními body vůbec neexistuje (např. bod byl mezitím smazaný), tohle
  ID se v `GetOrder` prostě tiše přeskočí (`byId.TryGetValue(id, out var point)`); pokud neexistuje **žádné**
  ID, vrátí se prázdný seznam – to je přesně situace, kterou řeší obranná kontrola v `ClickSequenceExecutor`
  (kapitola B.5.3/B.15).
- `RandomOrderStrategy` – Fisher-Yates shuffle. Používá se pro `Random` i `RandomNoImmediateRepeat` (ten druhý
  má dodatečnou logiku přímo v `ClickSequenceExecutor`, ne ve strategii – trochu neintuitivní místo, kdybys to
  hledal).
- `PointOrderStrategyFactory.Create(SequenceOrderMode)` – tovární metoda, mapuje enum na strategii (viz A.10,
  switch výraz).

---

## B.7 Humanizace – jitter a Bézierův pohyb

Cíl: aby appka neklikala "roboticky" na milimetr přesně ve stejném intervalu (běžný způsob, jak anti-cheat/
anti-bot systémy detekují autoclickery).

### B.7.1 `TimingJitter` (`Engine/Jitter/TimingJitter.cs`)
Přidá náhodnou odchylku k intervalu mezi kliky: `baseMs + náhodné číslo v rozsahu ±jitterMs`, s tvrdou
spodní hranicí 10 ms (`MinDelayMs`), aby appka nikdy nečekala záporně/nulově. Funguje bezpečně i pro záporný
`baseMs`/`jitterMs` (např. z ručně upraveného JSON souboru) – vždy se ořízne na minimum 10 ms.

### B.7.2 `PositionJitter` (`Engine/Jitter/PositionJitter.cs`)
Posune cílový bod kliknutí náhodně v kruhu o poloměru `radiusPx` – ne rovnoměrně ve čtverci, ale rovnoměrně
**po ploše kruhu** (`r = radius * sqrt(random)`, komentář v kódu to vysvětluje: "sqrt => rovnoměrné rozdělení
po ploše kruhu" – bez `sqrt` by body byly nahuštěné blíž ke středu). Záporný/nulový poloměr vrátí přesně střed.

### B.7.3 `BezierMovementPathGenerator` (`Engine/Movement/BezierMovementPathGenerator.cs`)
Nejsložitější kus – generuje trajektorii kurzoru mezi dvěma body jako kubickou Bézierovu křivku:
1. Vypočítá dva kontrolní body posunuté kolmo od přímé spojnice (`BuildControlPoints`) – proto křivka
   "prohýbá", ne přímka.
2. Počet kroků (`steps`) závisí na vzdálenosti (`Math.Clamp((int)(distance / 6.0), 12, 48)`) – delší pohyb =
   víc kroků, ale max 48.
3. Časování kroků není lineární – používá se `Easing.EaseInOutCubic` (kubická ease-in-out křivka, klasika z
   CSS animací/`transition-timing-function`), takže myš zrychluje na začátku a zpomaluje na konci pohybu, jako
   skutečná ruka.
4. S pravděpodobností `OvershootChance` se na konec přidá "přestřelení" cíle o pár pixelů a korekce zpět
   (`ApplyOvershootCorrection`) – simuluje, že člověk občas mine cíl o kousek a doladí pozici.
5. **Obranné ořezání rozsahu trvání** (řádky 23–25):
   ```csharp
   int durationMin = Math.Max(0, config.MovementDurationMsMin);
   int durationMax = Math.Max(durationMin, config.MovementDurationMsMax);
   int durationMs = rng.Next(durationMin, durationMax + 1);
   ```
   Bez tohohle by poškozený/ručně upravený JSON profil s `MovementDurationMsMin > MovementDurationMsMax` shodil
   `Random.Next(min, max)` na `ArgumentOutOfRangeException` a klikací smyčka by tiše spadla. UI samo tuhle
   situaci nikdy nevytvoří (`ProfileEditorViewModel.ToClickProfile` hodnoty srovná ještě před uložením), ale
   engine se na to nespoléhá a chrání se sám – viz kapitola B.15.

`ClickSequenceExecutor.ExecuteSinglePointAsync` pak tuhle trajektorii "přehrává" krok po kroku, s delayem mezi
jednotlivými kroky, což vytváří plynulý pohyb myši místo okamžitého teleportu.

**Pozor:** `IMovementPathGenerator` je rozhraní jen s jednou implementací (`BezierMovementPathGenerator`), ale
`ClickSequenceExecutor` konstruktor ho přijímá jako volitelný parametr (`movementGenerator = null` →
default `new BezierMovementPathGenerator()`) – umožňuje to v testech dosadit jinou/mock implementaci, i když v
produkci se používá vždy jen ta jedna.

---

## B.8 Rozlišení obrazovky – zachycení, detekce změny, přepočet

Appka si při přidání prvního bodu do profilu zapamatuje aktuální konfiguraci monitorů
(`ScreenSnapshot`/`MonitorBounds`) – vidíš to v `ProfileEditorViewModel.AddPoint()`
(`CapturedScreenSnapshot ??= _screenInfoProvider.GetCurrentSnapshot();`). Uloží se to spolu s
profilem (`ClickProfile.CapturedScreenSnapshot`).

- **`IScreenInfoProvider.GetCurrentSnapshot()`** – v `AutoClicker.Core` jen rozhraní, implementace je v `App`
  vrstvě: `AvaloniaScreenInfoProvider` (`src/AutoClicker.App/Services/AvaloniaScreenInfoProvider.cs`) – čte
  `window.Screens.All` z Avalonia API (pozice, rozměry a scaling každého monitoru). Všimni si, že implementace
  je v `App`, ne v `Infrastructure` – protože potřebuje přístup ke konkrétnímu `Window` instance, ne k OS API
  přímo.
- **`ScreenSnapshot.IsCompatibleWith(other)`** (`ScreenSnapshot.cs`) – porovná počet monitorů a jejich přesné
  rozměry/pozice (scaling/DPI se do porovnání záměrně nepočítá). Používá se na dvou místech:
  1. Před spuštěním (`MainWindowViewModel.StartAsync`, řádky 141–154) – pokud se nastavení monitoru liší od
     doby, kdy byly body nadefinované, appka nabídne dialog (`ResolutionMismatchDialog`).
  2. **Za běhu**, mezi cykly (`ClickSequenceExecutor.CheckResolutionChanged`, volané po každém dokončeném
     cyklu) – pokud uživatel za běhu appky přepojí monitor / změní rozlišení, smyčka se sama zastaví a appka
     se zeptá znovu (`MainWindowViewModel.OnResolutionChangedDuringRun`).
- **`ProfileRescaler.Rescale`** (`src/AutoClicker.Core/Screen/ProfileRescaler.cs`) – přepočítá pozice bodů
  poměrem: najde, na kterém monitoru bod původně byl, spočítá relativní pozici (0–1) v rámci toho monitoru, a
  aplikuje ji na odpovídající monitor v novém rozlišení. Pokud počet monitorů nesedí, spadne na první dostupný
  (`monitorIndex < to.Monitors.Count ? ... : to.Monitors[0]`). Prázdný seznam monitorů na kterékoliv straně
  appku nespadne – vrátí profil beze změny.

Uživatel má vždy na výběr ze tří možností (`ResolutionMismatchChoice`: `Rescale`, `ContinueAnyway`, `Cancel`) –
dialog `ResolutionMismatchDialog.axaml` vrací tuto hodnotu jako výsledek modálního okna
(`dialog.ShowDialog<ResolutionMismatchChoice>(OwnerWindow)`).

---

## B.9 Zachytávání screenshotů – `IScreenCaptureProvider`

`src/AutoClicker.Core/Screen/IScreenCaptureProvider.cs` – jednoduché rozhraní, jedna metoda
`CaptureRegion(x, y, width, height)` vracející PNG bajty (nebo `null`, pokud capture není na dané platformě
podporovaný).

Implementace `WindowsScreenCaptureProvider`
(`src/AutoClicker.Infrastructure/Capture/WindowsScreenCaptureProvider.cs`)
používá `System.Drawing.Graphics.CopyFromScreen` – to je **GDI API, které existuje jen na Windows** (proto
`OperatingSystem.IsWindows()` kontrola na začátku a `[SupportedOSPlatform("windows")]` atribut). Na
Linuxu/macOS by tahle třída vždy vrátila `null` – appka to ošetřuje graficky (viz `ProfileEditorViewModel.
RefreshDetailCapture`: `if (png is null) return;` – prostě se nezobrazí náhled, appka nespadne). Celé volání je
navíc obalené v `try/catch`, takže i neočekávané selhání capture (chybějící oprávnění, RDP relace bez GPU) vrátí
jen `null`, ne pád appky.

Použití: `ProfileEditorViewModel.RefreshDetailCapture()` – zavolá se přímo (okamžitě), kdykoliv se změní
vybraný krok (`OnSelectedStepChanged`) nebo se zapne/vypne checkbox "Zobrazit reálný obsah obrazovky"
(`OnShowRealScreenshotChanged`). Zachytí oblast `220×140 px` kolem bodu (`DetailCaptureWidth/Height` konstanty),
vytvoří z PNG bajtů `Avalonia.Media.Imaging.Bitmap` a nastaví do `DetailBitmap` vlastnosti, na kterou je
navázaný `<Image>` v `SequenceMapView.axaml`. Ve view je přes CSS-like binding s converterem
(`ObjectConverters.IsNotNull`/`IsNull`) přepínání mezi zobrazením obrázku a placeholder textu.

**Výkonová oprava – debounce při přepisování souřadnic (viz kapitola B.16):** Pole `X`/`Y` kroku v
`SequenceTimelineView.axaml` jsou navázaná přes `NumericUpDown`, které aktualizuje bindovanou hodnotu **živě při
psaní**, ne až při opuštění pole. Než byla přidána oprava níže, každý jednotlivý stisk klávesy při ručním
přepisování souřadnice vyvolal `OnStepPositionChanged` → `RefreshDetailCapture()` → **synchronní** GDI
`CopyFromScreen` + PNG enkódování na UI vlákně (viz výše) – při rychlém psaní čísla znatelné zadrhávání okna.
`OnStepPositionChanged` teď místo přímého volání `RefreshDetailCapture()` volá `ScheduleDetailCaptureRefresh()`,
která přes `DispatcherTimer` (250 ms) zachytávání **debounce**uje – capture proběhne až 250 ms po poslední změně
souřadnice, takže psaní vícemístného čísla vyvolá jediný capture místo jednoho na znak. Přímé cesty
(`OnSelectedStepChanged`, `OnShowRealScreenshotChanged`) zůstávají okamžité (a nejdřív zruší případný rozjetý
debounce timer), protože tam žádné "psaní" neprobíhá a uživatel čeká na okamžitou odezvu.

**Pozor na paměť:** `DetailBitmap?.Dispose()` se volá před každým novým nastavením – `Bitmap` drží
nespravovanou (unmanaged) paměť/handle, což .NET garbage collector sám neuklidí spolehlivě a včas, proto ruční
`Dispose()`. `IDisposable`/`Dispose()` je .NET obdoba PHP `__destruct()`, jen **explicitní a deterministická**
(voláš ji sám, hned, místo aby garbage collector uklidil "někdy později" jako PHP refcounting) – nejblíž tomu je
v PHP vzor "explicitně zavolej `fclose($handle)`", ne spoléhat na to, že to udělá GC. Pokud bys `Dispose()` při
úpravách vynechal, hrozí postupný únik paměti při rychlém přepínání kroků.

---

## B.10 JSON persistence profilů – `JsonProfileRepository`

`src/AutoClicker.Infrastructure/Persistence/JsonProfileRepository.cs`.
Implementuje `IProfileRepository` (Core rozhraní). Každý profil je samostatný `.json` soubor pojmenovaný podle
GUID (`{profileId}.json`), uložený v `%AppData%\AutoClicker\profiles\` (`Environment.SpecialFolder.
ApplicationData`, konstruktor bez parametrů). Test (`JsonProfileRepositoryTests.cs`) používá druhý
konstruktor s explicitní cestou (`temp` adresář), aby netestoval proti skutečné `%AppData%` složce uživatele.
Jméno souboru se odvozuje **výhradně z GUID**, nikdy z uživatelem zadaného názvu profilu – takže ani exotický
název profilu (lomítka, `..`, apod.) nemůže ovlivnit, kam se soubor zapíše.

`SaveAsync` píše nejdřív do `{id}.json.tmp` a teprve po úspěšném zapsání ho přejmenuje (`File.Move(...,
overwrite: true)`) na finální cestu. Tohle je **atomický zápis** – kdyby appka spadla/vypnul se
počítač uprostřed zápisu, originální `.json` zůstane nedotčený místo poloviny rozbitého JSONu. Analogie:
podobně jako se v PHP dělá bezpečný zápis konfigurace přes `tempnam()` + `rename()`.

```csharp
foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
{
    try
    {
        await using var stream = File.OpenRead(file);
        var profile = await JsonSerializer.DeserializeAsync<ClickProfile>(stream, SerializerOptions);
        if (profile is not null) profiles.Add(profile);
    }
    catch (JsonException)
    {
        // Poškozený nebo ručně upravený soubor profilu - přeskočit, ať nezhatí načtení ostatních profilů.
    }
}
```
`LoadAllAsync` **obalí načtení každého souboru vlastním `try/catch`** (viz A.14) – pokud je jeden `.json` soubor
poškozený (např. useknutý zápis po pádu appky, nebo si ho uživatel ručně "pokazí" v textovém editoru), tenhle
konkrétní soubor se přeskočí a ostatní profily se načtou normálně. Bez tohohle by jediný špatný soubor shodil
načtení **všech** profilů hned při startu appky – a protože se `InitializeAsync()` volá z `async void`-like
handleru (`Opened += async (_, _) => await viewModel.InitializeAsync();` v `MainWindow.axaml.cs`), neodchycená
výjimka by v tomhle místě spadla jako neošetřená a appka by při startu padala **pořád dokola**, dokud by
uživatel ručně nesmazal poškozený soubor z `%AppData%`. Detailně rozebráno v kapitole B.15 (bezpečnost/robustnost).

Serializace používá `System.Text.Json` (vestavěná .NET JSON knihovna, ne Newtonsoft) s
`JsonStringEnumConverter` (enumy jako `SequenceOrderMode.Random` se ukládají jako čitelný string `"Random"`,
ne jako číslo, viz A.12) a `WriteIndented = true` (hezky formátovaný JSON, čitelný i ručně).

---

## B.11 Globální hooky a hotkeys – `SharpHookGlobalListener`

`src/AutoClicker.Infrastructure/Input/SharpHookGlobalListener.cs`. Toto je
nejcitlivější a nejsložitější kus na pochopení, protože pracuje s **globálními OS hooky** – naslouchá stisku
kláves/kliknutí myši **kdekoliv v systému**, ne jen v okně appky (proto appka funguje i "na pozadí").

Používá knihovnu **SharpHook** (`TaskPoolGlobalHook`), která interně obaluje nízkoúrovňové OS API (na Windows
`SetWindowsHookEx`). `_hook.RunAsync()` (voláno z `MainWindow` konstruktoru, `_globalListener.Start()`) spustí
naslouchání na vlastním vlákně/vláknech – **eventy z hooku (`OnKeyPressed`, `OnKeyReleased`, `OnMouseClicked`)
přicházejí na jiném vlákně než UI vlákno appky.**

### B.11.1 Dvě odlišné role tohoto listeneru

1. **Registrované hotkeys** (`RegisterHotkey`/`UnregisterHotkey`) – trvale sledované kombinace (Start hotkey,
   Stop hotkey), uložené v `ConcurrentDictionary<object, (HotkeyConfig, bool Triggered)>` (`Concurrent...` =
   verze `Dictionary`, se kterou je bezpečné pracovat současně z více vláken najednou – obyčejný `Dictionary`
   by se při souběžném přístupu z hook vlákna i UI vlákna mohl vnitřně rozbít). Klíč `object subscriberId` je
   jen unikátní "vlastník" (v `MainWindowViewModel` jsou to `_startHotkeySubscriberId`/
   `_stopHotkeySubscriberId`, obyčejné `new object()` použité jako token identity, ne kvůli hodnotě). `bool
   Triggered` slouží jako "debounce" flag – zabraňuje opakovanému vystřelení eventu, dokud uživatel drží klávesu
   stisknutou (reset se stane až při `OnKeyReleased`).
2. **Jednorázové capture callbacky** (`CaptureNextClick`, `CaptureNextHotkey`) – použité při "Přidat bod" (čeká
   se na příští kliknutí myši kdekoliv na obrazovce) a "Nastavit hotkey" (čeká se na příští kombinaci kláves).
   Implementované jako `Action<T>?` pole (viz A.8), které se atomicky vynuluje přes `Interlocked.Exchange` při
   zavolání – to zaručuje, že callback se spustí **jen jednou** i při souběžném přístupu z hook vlákna.
   Vrací se `IDisposable` (`CaptureCancellation`), pomocí kterého lze zachytávání zrušit dřív (tlačítko "Zrušit
   výběr bodu"/"Zrušit" v UI).

### B.11.2 Jak appka pozná kombinaci kláves (Ctrl+Shift+F6 apod.)

`_pressedKeys` (`HashSet<KeyCode>`, chráněný `lock` – C# obdoba PHP `flock()`/mutexu, jen v paměti mezi vlákny)
sleduje, které klávesy jsou aktuálně stisknuté. Při stisku hlavní klávesy (ne modifikátoru) se ze snapshotu
aktuálně stisknutých kláves poskládá `HotkeyConfig` (`OnKeyPressed`) – proto appka rozezná "Ctrl+Alt+F6" jako
celek, ne jen poslední stisk. `Matches(config, pressed)` pak porovnává, jestli jsou stisknuté *přesně*
modifikátory z configu plus hlavní klávesa.

### B.11.3 Propojení s ViewModely a nutnost `Dispatcher.UIThread.Post`

Callbacky ze `SharpHookGlobalListener` přichází na **hook vlákně**, ne UI vlákně. Proto všude, kde
`ProfileEditorViewModel`/`MainWindowViewModel` reagují na tyto callbacky, je vidět
`Dispatcher.UIThread.Post(() => { ... })` (např. `ProfileEditorViewModel.AddPoint`;
`MainWindowViewModel.OnGlobalHotkeyPressed`). Vynechání tohohle je nejčastější zdroj
záhadných pádů/výjimek při práci s tímto kódem – Avalonia (jako většina UI frameworků) vyžaduje, aby se
bindnuté vlastnosti měnily jen z UI vlákna.

**Pozor při debugování:** `_globalListener.Stop()` volá `_hook.Dispose()` – hook lze po `Dispose()`
znovu spustit jen vytvořením nové instance, ne opětovným `Start()`. To se ale v appce neděje za normálního
běhu (`Stop()` se volá jen při zavírání appky, `MainWindow.axaml.cs`, `Closed += (_, _) =>
_globalListener.Stop();`).

---

## B.12 `MainWindowViewModel` a `ProfileEditorViewModel` – jak spolu komunikují

Tohle jsou dva hlavní ViewModely appky a jejich propojení je klíčové pro pochopení celého UI toku.

- **`MainWindowViewModel`** (`src/AutoClicker.App/ViewModels/MainWindowViewModel.cs`) – "vrchní" ViewModel:
  seznam profilů (`Profiles`), vybraný profil (`SelectedProfile`), stav enginu (`IsRunning`, `StatusText`,
  `CanStart`), příkazy Nový/Uložit/Smazat/Start/Stop. Vlastní instanci `ClickSequenceExecutor` a
  `IProfileRepository`.
- **`ProfileEditorViewModel`** (`ProfileEditorViewModel.cs`) – "vnitřní" editor jednoho konkrétního profilu:
  všechny editovatelné vlastnosti profilu (název, timing, humanizace, hotkeys) a seznam kroků (`Steps`,
  kolekce `SequenceStepViewModel`). Je to vlastnost `Editor` na `MainWindowViewModel` (řádek 28), a v
  `MainWindow.axaml` se na něj přepíná `DataContext` uvnitř vnořených panelů (`DataContext="{Binding Editor}"`,
  řádky 40, 111, 113) – to je běžný Avalonia vzor: vnořit "pod-ViewModel" do jiného `DataContext`u pro danou
  část obrazovky.

### Tok při přepnutí profilu
`SelectedProfile` se změní (uživatel klikne v seznamu) → `OnSelectedProfileChanged` (generovaný partial metoda
z `[ObservableProperty]`, viz A.11, `MainWindowViewModel.cs` řádek 90) → `Editor.LoadFrom(value)` naplní editor
daty z vybraného profilu (`ProfileEditorViewModel.LoadFrom`).

### Tok při uložení
`SaveProfileAsync` → `Editor.ToClickProfile()` sestaví `ClickProfile` record ze stavu editoru → uloží přes
repository → aktualizuje/přidá do `Profiles` kolekce.

### Tok při spuštění (Start)
`StartAsync` (`MainWindowViewModel.cs`, řádky 127–158) – vezme aktuální snapshot obrazovky, sestaví profil,
zkontroluje, že je nastavená Stop klávesa (**appka vyžaduje Stop hotkey, než dovolí Start** – bez ní by
uživatel neměl jak zastavit nekonečnou smyčku kliků, pokud okno schová/appka běží na pozadí), zkontroluje shodu
rozlišení a případně zeptá na přepočet, a nakonec zavolá `_executor.StartAsync(profile)`.

### Propojení hotkeys mezi Editorem a hlavním ViewModelem
`ProfileEditorViewModel` má eventy `HotkeyChanged`/`StopHotkeyChanged` (viz A.8), na které se
`MainWindowViewModel` přihlašuje v konstruktoru (řádky 58–63) a přeposílá je do
`_globalListener.RegisterHotkey/UnregisterHotkey`. Tohle je vzor, jak "vnitřní" ViewModel informuje "vnější" o
změně bez přímé závislosti na `IGlobalInputListener` (i když `ProfileEditorViewModel` na něj taky přímo
odkazuje pro capture funkce – `CaptureNextClick`, `CaptureNextHotkey`).

`CanStart` (`MainWindowViewModel`, řádek 71) je odvozená vlastnost (`!IsRunning && Editor.HasStopHotkey`),
přepočítávaná explicitně (`UpdateCanStart()`) při změně `IsRunning` (`partial void OnIsRunningChanged`) i při
změně `Editor.HasStopHotkey` (odběr `Editor.PropertyChanged`, řádky 64–67) – v CommunityToolkit.Mvvm nejsou
automaticky "computed properties" jako v Vue, musí se ručně přepočítávat a explicitně vyvolávat notifikaci.

---

## B.13 Views a responzivní layout

- **`MainWindow.axaml`** – hlavní okno: seznam profilů vlevo, editor profilu + ovládací tlačítka vpravo.
  Vnořuje `SequenceMapView` a `SequenceTimelineView` (viz níže).
- **`SequenceMapView.axaml`/`.axaml.cs`** (`src/AutoClicker.App/Views/SequenceMapView.axaml`) – "prostorová
  mapa": zmenšený plánek monitorů (`MapMonitorRects`) s tečkami jednotlivých kroků (`Steps`, pozicované přes
  `MapX`/`MapY`, přepočítané v `ProfileEditorViewModel.RecomputeMap()`) a spojnicí (`Polyline` z
  `MapPolylinePoints`) ukazující pořadí kliků. Vedle toho panel s detailním přiblíženým screenshotem vybraného
  bodu (kapitola B.9), s červeným křížkem přesně na souřadnicích `210,140`/`220,130-150` (`Line` prvky) – to je
  **natvrdo napsaný střed** výřezu `220×140`, protože capture je vždy centrovaný na bod
  (`DetailCaptureWidth/2`, `DetailCaptureHeight/2` v `RefreshDetailCapture`). Pokud by se měnily konstanty
  `DetailCaptureWidth/Height` v `ProfileEditorViewModel`, je nutné přepočítat i souřadnice křížku v XAML – nejsou
  provázané automaticky. Mini-mapa (380×200 px) a detailní náhled (440×280 px) mají **záměrně pevnou velikost**
  – jsou to schématické/přiblížené náhledy, ne obsah, který by dávalo smysl škálovat s oknem.
- **`SequenceTimelineView.axaml`** – vodorovná časová osa kroků (karty s název/X/Y/počet kliků/vlastní delay a
  tlačítky Vybrat/přesun/smazání). Všimni si vzoru `Command="{Binding #Root.DataContext.MoveStepUpCommand}"` –
  protože `DataTemplate` uvnitř `ItemsControl` má vlastní `DataContext` (jednotlivý `SequenceStepViewModel`),
  musí se příkaz na "rodičovský" `ProfileEditorViewModel` dostat přes pojmenovanou referenci na kořenový prvek
  (`x:Name="Root"`) – běžný Avalonia/WPF idiom, na který je dobré si zvyknout, protože se objevuje i v
  `SequenceMapView.axaml` (`#Root.DataContext.SelectStepCommand`). Zvláštnost Avalonia XAML resolveru: uvnitř
  vnořeného `ItemsControl.ItemTemplate` typovaný cast (`#Root.((vm:X)DataContext).Command`) při runtime selže –
  musí se použít netypovaná cesta (`#Root.DataContext.Command`), protože `DataContext` je `object` a binding se
  na příkaz dováže reflexí i tak.
- **`ResolutionMismatchDialog.axaml`/`.axaml.cs`** – modální dialog (`Window`, ne `UserControl`) s třemi
  tlačítky, vrací `ResolutionMismatchChoice` přes `Close(hodnota)` a čte se přes `await dialog.ShowDialog<T>
  (owner)` (kapitola B.8).

### B.13.1 Responzivita okna – jak appka reaguje na změnu velikosti

Okno je navržené tak, aby šlo libovolně zvětšovat i zmenšovat (v mezích), aniž by se obsah ořezával mimo
dosah:

- **`MinWidth="900" MinHeight="560"`** na `<Window>` (`MainWindow.axaml`, řádek 11) – dolní hranice zvolená tak,
  aby se do ní vešla nejužší kritická část layoutu: 4sloupcová mřížka "Pořadí/Opakování/Interval/Jitter"
  (`Grid ColumnDefinitions="Auto,*,Auto,*"`). Pod touhle šířkou by `ComboBox`/`NumericUpDown` prvky nešly
  zmenšit dost na to, aby se vešly, a bez omezení by přetekly mimo viditelnou oblast okna beze zbytku (Windows
  render cíl je striktně ohraničený velikostí okna – přetečený obsah by nebyl vidět a nešel by dosáhnout ani
  scrollem).
- **`ScrollViewer` kolem pravého panelu** (`MainWindow.axaml`, řádek 31, `VerticalScrollBarVisibility="Auto"`) –
  celý editor profilu (pole, mapa, timeline) je zabalený ve `ScrollViewer`u. Předtím, když okno bylo nižší než
  obsah, spodní část (mapa/timeline) byla prostě neviditelná a nedosažitelná. Teď se při nedostatku výšky
  objeví svislý posuvník.
- **`WrapPanel` místo `StackPanel`** pro vodorovné řady tlačítek (toolbar nahoře, řádky "Přidat bod"/hotkeys) –
  `WrapPanel` (na rozdíl od `StackPanel`) automaticky zalomí prvky na další řádek, pokud se nevejdou vedle sebe
  na šířku. To je poslední pojistka pro případ užšího okna – tlačítka se nezmizí ani neořežou, jen se přeskupí.
- **Levý panel se seznamem profilů** má `Grid.Column="Auto"` s `MinWidth="180" MaxWidth="320"` (místo dřívější
  pevné šířky 220 px) – přizpůsobí se obsahu (délce názvů profilů) v rozumných mezích, ale nikdy nezmizí ani
  nezabere celé okno.

---

## B.14 Testy (`tests/AutoClicker.Core.Tests`)

xUnit (obdoba PHPUnit) + NSubstitute (obdoba Mockery) jako mockovací knihovna. Testuje se výhradně
`AutoClicker.Core` (+ `JsonProfileRepository` z Infrastructure) – **žádné UI testy**, protože ViewModely a Views
vyžadují běžící Avalonia framework a nejsou v testovacím projektu pokryté (vyžadovalo by to headless Avalonia
test rig, což je nad rámec současné konvence repa). UI/hook změny se ověřují ručním spuštěním appky.

Aktuálně **93 testů**, rozdělených takto:

- `BezierMovementPathGeneratorTests.cs` – trajektorie vždy skončí přesně v cíli, počet kroků je v rozumných
  mezích, delaye nejsou záporné, přestřelení (`overshoot`) přidává přesně dva kroky navíc, a **regresní testy
  na obranné ořezání rozsahu trvání pohybu** (`MovementDurationMsMin > MovementDurationMsMax` i záporné hodnoty
  nesmí shodit generátor – viz B.7.3/B.15).
- `EasingTests.cs` – hraniční hodnoty (0, 0.5, 1) a monotónnost křivky zrychlení/zpomalení.
- `ClickSequenceExecutorTests.cs` – **nejdůležitější testovací soubor** pro pochopení async chování enginu.
  `RunToCompletionAsync` helper čeká na `StatusChanged` event s `EngineStatus.Stopped` přes
  `TaskCompletionSource` (viz A.9), s volitelným timeoutem (`Task.WhenAny(stopped.Task, Task.Delay(ms))`) –
  ukazuje přesně ten vzor, jak zvenčí "počkat", až fire-and-forget smyčka doběhne, protože `StartAsync` sám o
  sobě nečeká. Pokrývá: pořadí kliknutí, počet opakování, okamžité zastavení (`Stop_
  CancelsInfiniteLoopPromptly`), souběžné volání `StartAsync` za běhu (ignoruje se), počet kliknutí na bod,
  per-bodové přepsání intervalu (`DelayAfterMsOverride`), vlastní pořadí (`CustomOrder`) včetně **prázdného
  pořadí bez pádu/zaseknutí**, `RandomNoImmediateRepeat` (žádné dva stejné body za sebou napříč cykly),
  detekci změny rozlišení za běhu, a humanizovaný pohyb přes injektovaný `IMovementPathGenerator`. Používá
  `Substitute.For<IInputSimulator>()` – mock bez skutečného hýbání myší.
- `JsonProfileRepositoryTests.cs` – round-trip test (ulož→načti), mazání (i neexistujícího profilu), přepis
  profilu se stejným Id (needuplikuje se), a **regresní testy na odolnost proti poškozenému JSONu** (poškozený
  i prázdný soubor se přeskočí, ostatní profily se načtou normálně – viz B.10/B.15). Přes dočasný adresář
  (`IDisposable.Dispose()` na konci úklidí temp složku – obdoba `tearDown()` v PHPUnit).
- `PointOrderStrategyTests.cs` – řazení bodů včetně okrajových případů `CustomOrderStrategy` (ID mimo seznam
  bodů se přeskočí, duplicitní ID se v pořadí zopakuje, žádné shodné ID → prázdný výsledek).
- `PositionJitterTests.cs`, `TimingJitterTests.cs` – jednotkové testy jednotlivých algoritmů, statistické (např.
  "1000× zkus, ať jsou všechny výsledky v rozsahu") kvůli náhodnosti, plus okrajové případy záporných vstupů.
- `ProfileRescalerTests.cs` – přepočet pozic mezi různými rozlišeními/počty monitorů, včetně bodu mimo hranice
  monitoru a nesouhlasného počtu monitorů mezi starým a novým snapshotem.
- `ScreenSnapshotTests.cs` – `IsCompatibleWith` (shoda/neshoda počtu monitorů, pozice, rozlišení; scaling se
  záměrně nezapočítává).
- `JsonAppSettingsRepositoryTests.cs` – round-trip jazyka i motivu (`AppSettings.Language`/`Theme`), výchozí
  hodnoty při chybějícím/poškozeném souboru, přepis při druhém uložení, a **regresní test na sync-over-async
  deadlock** (`LoadAsync_BlockingCallOnCapturedSynchronizationContext_DoesNotDeadlock`) – `MainWindow`
  volá `LoadAsync().GetAwaiter().GetResult()` synchronně na UI vlákně ještě před `InitializeComponent()`
  (viz B.3, B.17.3), takže repository musí interně používat `ConfigureAwait(false)`, jinak by pokračování
  po `await` čekalo navěky na zablokovaný `SynchronizationContext`.
- `PerformanceTests.cs` – **hrubé výkonnostní testy** (viz kapitola B.16), ne mikrobenchmarky: generování 5000
  Bézier trajektorií, shuffle 50 000 bodů, přepočet (`ProfileRescaler`) 5000bodového profilu a JSON round-trip
  5000bodového profilu musí doběhnout pod velkoryse nastavenou časovou mez (řádový strop, ne těsný), a klikací
  smyčka nad 30 body s nulovým intervalem nesmí přidávat neúměrnou režii nad teoretické minimum dané pevnými
  50ms/10ms delayi. Cíl není přesné měření, ale zachycení hrubé regrese (např. omylem vložená synchronní práce
  do horké smyčky).

Pokud budeš přidávat/měnit chování v `ClickSequenceExecutor`, `BezierMovementPathGenerator`, `PositionJitter`,
`TimingJitter`, `ProfileRescaler` nebo strategiích řazení, tyhle testy jsou nejrychlejší způsob, jak si ověřit,
že jsi nic nerozbil, bez nutnosti appku ručně spouštět a klikat. Spouští se přes `dotnet test
tests/AutoClicker.Core.Tests` (viz `CLAUDE.md` v kořeni repa pro přesné příkazy).

---

## B.15 Bezpečnost a robustnost – co appka ošetřuje a proč

Appka nemá žádnou síťovou komunikaci (žádný `HttpClient`, žádné volání na internet), neběží se zvýšenými
oprávněními (`app.manifest` nežádá o administrátorská práva), a jediná perzistence je JSON na disku pod
`%AppData%\AutoClicker\profiles\` s jmény souborů odvozenými čistě z GUID (žádná cesta k path traversal ani
kolizi z uživatelského vstupu). Globální klávesnicový/myšový hook (kapitola B.11) je nutný pro funkci globálních
zkratek, ale nikde neloguje ani nikam neposílá zmáčknuté klávesy – jen je v paměti porovnává s nastavenou
kombinací a zahazuje.

Při auditu byly nalezeny a opravené tři reálné, reprodukovatelné chyby robustnosti (ne teoretické):

1. **Pád appky při startu kvůli jednomu poškozenému profilu** (kapitola B.10) – `JsonProfileRepository.
   LoadAllAsync()` teď obaluje deserializaci každého souboru vlastním `try/catch (JsonException)`, takže
   poškozený/ručně upravený `.json` soubor se jen přeskočí místo shození načtení všech profilů (a tím pádem
   opakovaného pádu appky při každém startu).
2. **Pád klikací smyčky při poškozených humanizačních datech** (kapitola B.7.3) – `BezierMovementPathGenerator`
   teď ochranně srovná `MovementDurationMsMin`/`Max` (`Math.Max`) předtím, než je použije v `Random.Next(min,
   max)`, takže `MovementDurationMsMin > MovementDurationMsMax` (možné jen z ručně upraveného/poškozeného JSON
   souboru, UI samo tuhle kombinaci nikdy neuloží) appku nesloží uprostřed běhu do trvale "zaseknutého" stavu.
3. **100% vytížení CPU jádra při poškozeném vlastním pořadí bodů** (kapitola B.5.3/B.6) – pokud `CustomOrder`
   odkazuje jen na neexistující/smazané body, `CustomOrderStrategy` vrátí prázdný seznam; `ClickSequenceExecutor`
   teď v tomhle případě krátce čeká (`CancellableDelay(100, token)`) místo aby se donekonečna točil bez jediného
   čekání (busy-loop) při `RepeatMode.Infinite`.

Všechny tři jsou pokryté regresním testem (kapitola B.14), takže se nemůžou tiše vrátit při budoucích úpravách.
NuGet závislosti appky (`AutoClicker.App` a jeho tranzitivní závislosti – SharpHook, Avalonia,
CommunityToolkit.Mvvm) byly zkontrolované přes `dotnet list package --vulnerable` bez nálezu. (Testovací projekt
hlásí starou tranzitivní `System.Net.Http`/`System.Text.RegularExpressions 4.3.0` přes balíček `xunit` – jde o
neškodnou referenční závislost z `netstandard1.1` éry, kterou runtime na .NET 8 nepoužije a která se nikdy
nedostane do vydávaného `AutoClicker.App.exe`.)

---

## B.16 Výkon a efektivita – audit a nálezy

Appka je "event/delay-driven", ne CPU-bound: klikací smyčka tráví naprostou většinu času v `Task.Delay(...)`
(čekání mezi kroky pohybu, mezi kliky, mezi body) – reálné CPU výpočty (Bézier křivka, jitter, řazení bodů) jsou
řádově mikrosekundy až jednotky milisekund na cyklus, tedy zanedbatelné vůči desítkám/stovkám ms delayů, které
appka mezi kroky záměrně čeká (viz B.7). Z toho plyne, kde efektivita v týhle appce **skutečně** hraje roli: ne
v algoritmech enginu, ale v UI vlákně, kde jakákoliv synchronní práce navíc znamená viditelné zadrhnutí okna.

Audit prošel celý `Core`/`Infrastructure`/`App` kód se zaměřením na tohle. Nalezená a opravená položka:

1. **Screen capture na UI vlákně při každém stisku klávesy v poli X/Y** (viz kapitola B.9) – `NumericUpDown` v
   `SequenceTimelineView.axaml` aktualizuje bindovanou hodnotu živě při psaní, takže `RefreshDetailCapture()`
   (synchronní GDI `CopyFromScreen` + PNG enkódování, viz B.9) se předtím spouštěl na **každý jednotlivý znak**
   při ručním přepisování souřadnice bodu – reálně znatelné zadrhávání UI vlákna při rychlejším psaní. Opraveno
   debounce timerem (`ScheduleDetailCaptureRefresh`, 250 ms) v `ProfileEditorViewModel` – detaily a zdůvodnění
   časování v B.9.

Prošlá místa, kde by se dala čekat neefektivita, ale nebyl nalezen reálný problém (a proč se nezasahovalo –
princip "neopravuj, co není rozbité", viz zásady projektu):
- **`RandomOrderStrategy`/`CustomOrderStrategy` alokují novou kopii seznamu bodů jednou za cyklus** (`Engine/
  PointOrderStrategies/*.cs`), ne jednou za klik – u typického profilu (jednotky až nízké desítky bodů) a
  typického intervalu (desítky až stovky ms) je to alokace v řádu bajtů jednou za interval, ne v horké
  smyčce. Neřešeno, `PerformanceTests.RandomOrderStrategy_ShufflesLargePointList_WithinTimeBudget` ověřuje, že
  i extrémní vstup (50 000 bodů) zůstává rychlý.
- **`JsonProfileRepository.LoadAllAsync` čte soubory profilů sekvenčně, ne paralelně** (`Directory.
  EnumerateFiles` + `await` v cyklu) – u desítek profilů (reálný horní odhad pro tuhle appku) jde o disk I/O v
  řádu jednotek milisekund na soubor, ne o CPU práci, kterou by paralelizace zrychlila výrazně; přidaná
  komplexita (souběžné výjimky z `try/catch` na soubor, pořadí načtení) by nestála za marginální zisk.
- **`ClickSequenceExecutor.RunLoopAsync` volá `orderStrategy.GetOrder(...).ToList()` i pro `SequentialOrderStrategy`**,
  která vrací původní referenci beze změny – `.ToList()` tak i tady vytvoří zbytečnou kopii jednou za cyklus.
  Stejná úvaha jako u prvního bodu: zanedbatelné vůči délce cyklu, neřešeno.

Pokud v budoucnu přibude něco, co skutečně běží v horké smyčce (např. per-krokové logování, per-krokové I/O),
tohle je první místo, kam se vrátit a přehodnotit – a `PerformanceTests.cs` (kapitola B.14) je navržený tak, aby
podobnou regresi (znatelně zpomalenou horkou cestu) zachytil automaticky.

---

## B.17 Motiv appky – Light/Dark/Auto

Appka od Fáze 6 nabízí vlastní barevný motiv (ne výchozí modrou paletu Avalonia FluentTheme), ve dvou
variantách (tmavá/světlá) a s trojicí voleb v horní liště: **Auto** (sleduje motiv OS), **Light**,
**Dark**. Mechanismus stojí na vestavěném Avalonia theming systému, ne na ručním přebarvování.

### B.17.1 `ThemeDictionaries` – paleta jako zdroje závislé na motivu

`src/AutoClicker.App/Styles/AppTheme.axaml` definuje `ResourceDictionary.ThemeDictionaries` se dvěma
pojmenovanými sadami štětců (`x:Key="Dark"` a `x:Key="Light"`) – `ThemeBackgroundBrush`,
`ThemeSurfaceBrush`, `ThemeAccentBrush`, `ThemeGoodBrush` (stav "běží") atd. Obě sady používají
**stejnou barevnou rodinu** (tlumený graphite/lavender), ne prostou inverzi černá↔bílá – světlý režim
má tak pořád stejnou "identitu", jen na světlém podkladu.

Avalonia při vyhodnocení `{DynamicResource ThemeAccentBrush}` (viz A.5/A.11 pro obecný koncept
zdrojů/atributů) sama pozná, jaký `ThemeVariant` (`Light`/`Dark`) je aktuálně aktivní na daném vizuálním
stromu (`ActualThemeVariant`), a vybere odpovídající slovník. `DynamicResource` (na rozdíl od
`StaticResource`) navíc **znovu vyhodnotí vazbu při každé změně motivu** – proto stačí nastavit
`RequestedThemeVariant` a celé okno se samo přebarví, žádný ruční C# kód pro jednotlivé prvky UI není
potřeba.

### B.17.2 `AppControls.axaml` – styl controls nad barvami

`src/AutoClicker.App/Styles/AppControls.axaml` je `Styles` soubor (ne `ResourceDictionary` – v Avalonii
`Styles` obsahuje `Style` selektory typu CSS, `ResourceDictionary` jen pojmenované hodnoty) s pravidly
jako `Button.primary`, `Border.card`, `ComboBoxItem:selected` atd., která nastavují `Background`/
`BorderBrush`/`Foreground`/`CornerRadius` přes `DynamicResource` na paletu z B.17.1. Funguje to, protože
šablony vestavěných Avalonia controls (Button, ComboBox, NumericUpDown, ...) svoje vzhledové vlastnosti
čtou přes `TemplateBinding` – nastavením `Background` na úrovni `Style` se tak barva propíše až do
šablony, aniž by bylo nutné celou šablonu (ControlTemplate) přepisovat.

Obě sady se připojují v `App.axaml`: `AppTheme.axaml` do `Application.Resources.MergedDictionaries`,
`AppControls.axaml` do `Application.Styles` **za** `<FluentTheme />` (pořadí je důležité – v CSS-like
cascade vyhrává poslední shodné pravidlo, takže naše přepsání musí být až po základním Fluent motivu).

### B.17.3 Datový tok Auto/Light/Dark

- `AppSettings.Theme` (`AutoClicker.Core/Models/AppSettings.cs`) – `"Auto"` / `"Light"` / `"Dark"`,
  persistováno stejným mechanismem jako `Language` (`JsonAppSettingsRepository`, viz B.10 – stejný
  soubor `%AppData%\AutoClicker\settings.json`).
- Při startu (`MainWindow` konstruktor, řádky 24–36) appka namapuje uložený string na
  `Avalonia.Styling.ThemeVariant` (`"Light"`→`ThemeVariant.Light`, `"Dark"`→`ThemeVariant.Dark`, jinak
  `ThemeVariant.Default` – "sleduj OS") a nastaví `Application.Current.RequestedThemeVariant` ještě
  **před** `InitializeComponent()`, ať je od prvního vykresleného snímku použitá správná paleta.
- `MainWindowViewModel.SelectedTheme` (`[ObservableProperty]`) drží aktuálně vybranou volbu pro
  ComboBox-like segmentovaný přepínač v horní liště (`ListBox Classes="segmented"`,
  `MainWindow.axaml`). Při změně (`OnSelectedThemeChanged`) appka **živě** přenastaví
  `RequestedThemeVariant` (na rozdíl od jazyka, který se mění až po restartu, viz B.2.2/A.11 – Avalonia
  theming je nativně dynamické, appka pro to nepotřebuje žádný vlastní restart mechanismus) a uloží
  volbu na disk.
- **Pozor na past se sdíleným `AppSettings` recordem:** `AppSettings` má dvě pole (`Language`, `Theme`).
  Kdyby `OnSelectedThemeChanged` uložil jen `new AppSettings { Theme = value.Code }`, `Language` by se
  tiše přepsal na výchozí hodnotu (`record` bez explicitně nastaveného pole použije `init` default, ne
  předchozí uloženou hodnotu – viz A.4). Obě změnové metody (`OnSelectedLanguageChanged` i
  `OnSelectedThemeChanged`) proto vždy ukládají **oba** aktuální stavy najednou
  (`Language = SelectedLanguage.Code, Theme = SelectedTheme.Code`), ne jen to pole, které se zrovna
  změnilo.

### B.17.4 Rozdíl oproti nativnímu HTML `<select>` (pro srovnání s dřívějším mockupem)

Vizuální směr appky vznikl z HTML mockupu, kde platilo omezení, že nativní `<select>` v prohlížeči nejde
skoro vůbec přestylovat (rozbalený seznam zůstává vzhledem OS, i když je zavřená krabička hezky
načesaná). V Avalonii tohle omezení neplatí – `ComboBox` je plně šablonovatelný control, takže i
rozbalený seznam (`ComboBoxItem`, viz `AppControls.axaml`) jde plně domotivovat vlastní paletou. Skutečná
appka tak dosáhne vyššího vizuálního sjednocení, než jaké šlo předvést ve statickém HTML mockupu.

---

## B.18 Shrnutí – kde hledat, když něco nefunguje

| Symptom | Kde hledat |
|---|---|
| Appka neklika vůbec / hned skončí | `ClickSequenceExecutor.StartAsync` – kontrola `profile.Points.Count == 0`, `IsRunning` |
| Appka "zamrzne" UI | Chybějící `Dispatcher.UIThread.Post` v handleru volaném z jiného vlákna (executor eventy, hook eventy) |
| Klik neskončí na správném místě | `PositionJitter`, `ProfileRescaler`, `ScreenSnapshot.IsCompatibleWith` – změna rozlišení mezi definicí bodů a spuštěním |
| Globální hotkey nereaguje | `SharpHookGlobalListener` – zkontroluj `RegisterHotkey`/`_pressedKeys`, zda `_hook.RunAsync()` proběhl (`Start()` volaný v `MainWindow` konstruktoru) |
| Nejde nastavit/zachytit hotkey nebo bod | `_activeCapture` v `ProfileEditorViewModel` – capture je jednorázový a musí se explicitně zrušit (`CancelCaptureCommand`) před dalším pokusem |
| Náhled screenshotu se nezobrazuje | `WindowsScreenCaptureProvider` je Windows-only, na jiné platformě vrací vždy `null` – to je očekávané, ne chyba |
| Profil se neuloží / zmizí | `JsonProfileRepository` – zkontroluj `%AppData%\AutoClicker\profiles\`, atomický zápis přes `.tmp` soubor |
| Appka po startu opakovaně padá | Poškozený `.json` v `%AppData%\AutoClicker\profiles\` – od opravy v B.15 se má jen přeskočit, ne appku shodit; pokud přesto padá, zkontroluj, jestli je to skutečně `JsonException` a ne jiný typ chyby |
| Appka nejde zavřít křížkem | Záměr – `MainWindow.OnWindowClosing` schovává okno místo zavření, appka žije v tray ikoně |
| Stop nefunguje okamžitě | Očekávané – `CancellationToken` je kooperativní, aktuální krok (pohyb/klik) se dokončí |
| Okno při zmenšení "ořízne" spodek/pravou stranu | Zkontroluj `MinWidth`/`MinHeight` na `<Window>` a `ScrollViewer` kolem pravého panelu (`MainWindow.axaml`) – viz B.13.1 |
| Vysoké vytížení CPU při běžícím klikání | Zkontroluj `CustomOrder` profilu – pokud odkazuje na neexistující body, běží obranná pojistka v `ClickSequenceExecutor` (B.15); pokud je vytížení i s platným pořadím, jde o jiný problém |
| Okno "trhá"/zadrhává při ručním přepisování X/Y souřadnice bodu | Zkontroluj `ScheduleDetailCaptureRefresh`/`_captureDebounceTimer` v `ProfileEditorViewModel` – bez debounce by se `RefreshDetailCapture()` (GDI capture) spouštěl na každý stisk klávesy, viz B.9/B.16 |
| Motiv appky se nepřepne / zůstane po restartu jiný, než byl nastaven | `AppSettings.Theme` v `%AppData%\AutoClicker\settings.json` – zkontroluj `OnSelectedThemeChanged`/`OnSelectedLanguageChanged` ukládají oba stavy najednou (viz B.17.3, past se sdíleným recordem) |
| Nový/přestylovaný control (Button, ComboBox, ...) nesleduje motiv appky | Chybí `DynamicResource` na paletu z `AppTheme.axaml`, nebo `StaticResource` použitý omylem místo `DynamicResource` (ten se nepřepočítá při změně motivu) – viz B.17.1 |

---

*Dokument vygenerován analýzou zdrojového kódu ke dni 2026-08-15. Pokrývá všechny `.cs` a `.axaml` soubory ve
`src/` a `tests/` v tomto repozitáři, včetně responzivního UI (B.13.1), 93 testů (B.14), oprav robustnosti
popsaných v B.15, auditu výkonu/efektivity popsaného v B.16 a vlastního Light/Dark/Auto motivu popsaného v B.17.*
