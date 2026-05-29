# Delta.Imaging — Parannusehdotukset

Tämä dokumentti listaa havaitut ongelmat ja parannusehdotukset koko projektille.

---

## 🔴 Kriittinen: GitHub Actions workflow ei toimi

### Ongelma

`build-nbis-dll.yml` epäonnistuu CI:ssa kahdesta syystä:

1. **MSYS2 shell -ongelma**: Workflow käyttää suoraa `C:\msys64\usr\bin\bash.exe -l` kutsua sen sijaan että käyttäisi `msys2`-shelliä `setup-msys2`-actionin kautta. Tämä aiheuttaa sen, että asennetut paketit (gcc, make) eivät ole PATH:ssa.

2. **Shell-synkronointi**: `setup-msys2@v2` action luo shellin nimellä `msys2 {0}`, jota pitää käyttää myöhemmissä stepeissä.

### Korjaus

Muuta kaikki bash-stepit käyttämään:
```yaml
shell: msys2 {0}
```

Sen sijaan:
```yaml
shell: C:\msys64\usr\bin\bash.exe -l -e -o pipefail {0}
```

Ja vaihda MSYSTEM `msys2/setup-msys2`-actionin parametrilla:
```yaml
- name: Build NBIS 32-bit static libraries
  shell: msys2 {0}
  run: |
    export MSYSTEM=MINGW32
    source /etc/profile
    # ... rest of build
```

---

## 🟠 Koodiongelmat

### 1. `RawImageData.Empty` on muuttuva staattinen kenttä

**Tiedosto:** `Delta.Wsq/Delta.Wsq/RawImageData.cs` rivi 6

```csharp
public static RawImageData Empty = new RawImageData() { ... };
```

**Ongelma:** Kuka tahansa voi muuttaa `Empty`-objektin sisältöä tai itse viitteen, mikä rikkoo koko sovelluksen tilan.

**Korjaus:** Käytä jompaakumpaa:
```csharp
// Vaihtoehto A: readonly + immutable object
public static readonly RawImageData Empty = new RawImageData() { ... };

// Vaihtoehto B: Property joka palauttaa aina uuden instanssin
public static RawImageData Empty => new RawImageData() { ... };
```

### 2. `WsqCodec.Encode` palauttaa `null` virheen sattuessa

**Tiedosto:** `Delta.Wsq/Delta.Wsq/WsqCodec.cs` rivi 48

```csharp
return null;  // Palautetaan null jos encode epäonnistuu
```

**Ongelma:** Tämä on epäjohdonmukaista — tyhjä syöte palauttaa `new byte[0]`, mutta virhetilanne palauttaa `null`.

**Korjaus:** Palauta tyhjä taulukko virhetilanteessa tai heitä poikkeus:
```csharp
return Array.Empty<byte>();  // Johdonmukainen tyhjän syötteen kanssa
// TAI
throw new WsqEncodingException("WSQ encoding failed");  // Selkeä virheviesti
```

### 3. `hacked_vsscanf` / `hacked_vfscanf` rajoitus

**Tiedosto:** `Delta.Wsq/nbis/mingw_shim.cpp` rivit 20-40

```cpp
void* a[20];  // Max 20 argumenttia
```

**Ongelma:** Jos NBIS-koodi käyttää yli 20 argumenttia scanf-kutsussa, tulos on määrittelemätön.

**Korjaus:** Tämä on tiedostettu rajoitus alkuperäisestä koodista. Dokumentoi se BUILDING.md:hen ja lisää kommentti koodiin. NBIS ei käytännössä käytä näin montaa argumenttia.

---

## 🟡 CI/CD-parannukset

### 1. Testiprojektin DLL-polku on väärä

**Tiedosto:** `Delta.Wsq/tests/Delta.Wsq.Tests/Delta.Wsq.Tests.csproj` rivit 27-34

```xml
<Content Include="..\..\..\dependencies\nbis32.dll" ... />
```

**Ongelma:** Polku `..\..\..\dependencies\` menee `Delta.Wsq/`-kansion yläpuolelle repo-juureen, mutta `test.yml` lataa DLL:t kansioon `Delta.Wsq/dependencies/`.

**Korjaus:** Muuta polut:
```xml
<Content Include="..\..\dependencies\nbis32.dll" ... />
<Content Include="..\..\dependencies\nbis64.dll" ... />
```

### 2. Workflow ei käytä cachea

MSYS2-pakettien asennus kestää ~1 min joka ajossa. Lisää MSYS2-cache:

```yaml
- name: Set up MSYS2
  uses: msys2/setup-msys2@v2
  with:
    msystem: MINGW32
    update: true
    cache: true  # <-- Lisää tämä
    install: >-
      mingw-w64-i686-gcc
      mingw-w64-x86_64-gcc
      make
```

### 3. NBIS-klooni joka ajossa on turhaa

NBIS-lähdekoodi ei muutu. Cachea se tai käytä sparse checkoutia.

### 4. Release v1.1.0 osoittaa vanhaan committiin

Release `v1.1.0` luotiin ennen korjauksia. Päivitä tag tai luo uusi release `v1.1.1`.

---

## 🟢 Dokumentaatio ja rakenne

### 1. README.md on vanhentunut

**Tiedosto:** `README.md`

- Ei mainitse GitHub Actions workflowja
- Ei kerro, miten testit ajetaan
- Ei kerro uudesta build-prosessista

**Korjaus:** Lisää osiot:
- Build Status -badge
- CI/CD-kuvaus
- Testien ajaminen (`dotnet test`)

### 2. Yksikkötestit kattavat vain perustoiminnot

**Tiedosto:** `Delta.Wsq/tests/Delta.Wsq.Tests/WsqCodecTests.cs`

Testit kattavat:
- ✅ Decode toimii
- ✅ Encode tuottaa validin WSQ:n
- ✅ Round-trip säilyttää dimensiot
- ✅ GetComments toimii

Puuttuu:
- ❌ Reunatapaukset (erittäin pienet/suuret kuvat)
- ❌ Eri bitrate-arvot
- ❌ Virheellinen WSQ-data (korruptoitunut tiedosto)
- ❌ Suorituskyky/muistivuodot (pitkä ajo)

### 3. Ei ole CHANGELOG.md

Lisää `CHANGELOG.md` joka dokumentoi versiomuutokset (Keep a Changelog -formaatilla).

---

## 📋 Toimenpidelista (prioriteettijärjestyksessä)

| # | Prioriteetti | Toimenpide |
|---|---|---|
| 1 | 🔴 Kriittinen | Korjaa `build-nbis-dll.yml` käyttämään `shell: msys2 {0}` |
| 2 | 🔴 Kriittinen | Korjaa testiprojektin DLL-polut |
| 3 | 🟠 Tärkeä | Korjaa `RawImageData.Empty` → `readonly` |
| 4 | 🟠 Tärkeä | Tee `WsqCodec.Encode`-virhekäsittely johdonmukaiseksi |
| 5 | 🟡 Hyvä | Lisää MSYS2-cache workflowhin |
| 6 | 🟡 Hyvä | Päivitä README.md |
| 7 | 🟢 Nice-to-have | Lisää CHANGELOG.md |
| 8 | 🟢 Nice-to-have | Laajenna testikattavuutta |

---

## Korjattu workflow (esimerkki)

```yaml
name: Build nbis DLL

on:
  push:
    branches: ["**"]
  pull_request:
  workflow_call:
  workflow_dispatch:
  release:
    types: [created]

jobs:
  build:
    name: Build nbis32.dll / nbis64.dll
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up MSYS2
        uses: msys2/setup-msys2@v2
        with:
          msystem: MINGW64
          update: true
          cache: true
          install: >-
            mingw-w64-i686-gcc
            mingw-w64-x86_64-gcc
            make

      - name: Clone NBIS source
        shell: pwsh
        run: |
          git clone https://github.com/lessandro/nbis C:\nbis-src-32
          Copy-Item -Recurse -Force C:\nbis-src-32 C:\nbis-src-64

      - name: Build NBIS 32-bit static libraries
        shell: msys2 {0}
        run: |
          export MSYSTEM=MINGW32
          source /etc/profile
          mkdir -p /c/NBISBuild32
          cd /c/nbis-src-32
          ./setup.sh C:/NBISBuild32 --MSYS --STDLIBS --32
          make config
          make it
          make install

      - name: Build NBIS 64-bit static libraries
        shell: msys2 {0}
        run: |
          export MSYSTEM=MINGW64
          source /etc/profile
          mkdir -p /c/NBISBuild64
          cd /c/nbis-src-64
          ./setup.sh C:/NBISBuild64 --MSYS --STDLIBS --64
          make config
          make it
          make install

      # ... rest of workflow unchanged ...
```

---

*Dokumentti luotu: 2026-05-29*
