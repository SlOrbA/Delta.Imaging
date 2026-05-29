# Delta.Imaging — Tulevat Parannusehdotukset

Projektissa on nyt CI/CD-automatisointi ja testit toiminnassa. Seuraavassa listataan potentiaaliset parannukset seuraaviin julkaisuihin.

---

## 🟡 Prioriteetit

### 1. GitHub Actions deprecation -varoitukset

**Status:** ⚠️ Aktiivinen ongelma

Useissa workflowissa käytetään Node.js 20 -pohjaisia actioneja, jotka poistetaan käytöstä kesäkuussa 2026.

**Affected actions:**
- `actions/checkout@v4` → `v4` tukee Node 24 (check latest)
- `actions/download-artifact@v4`
- `actions/setup-dotnet@v4`
- `microsoft/setup-msbuild@v2`
- `dorny/test-reporter@v1`

**Korjaus:**
```bash
# Tarkista päivitykset:
gh action upgrade --repo SlOrbA/Delta.Imaging

# Tai manuaalisesti päivitä workflow YAML-tiedostot
```

**Hyöty:** Turvataan CI/CD jatkossa

---

### 2. Testikattavuuden laajentaminen

**Current coverage:** 10 testiä, kaikki perustoiminnot

**Lisättävät testit:**

#### a) Reunatapaukset
```csharp
[Fact]
public void Encode_VerySmallImage_ThrowsOrReturnsEmpty() { }

[Fact]
public void Encode_VeryLargeImage_Succeeds() { }

[Fact]
public void Decode_CorruptedWSQ_ThrowsException() { }
```

#### b) Eri parametrit
```csharp
[Theory]
[InlineData(0.75)]
[InlineData(2.5)]
[InlineData(5.0)]
public void Encode_VariousBitRates_Succeeds(double bitrate) { }
```

#### c) Platform-spesifika
- GDI+ conversion (Windows vain)
- WPF image conversion

**Hyöty:** Parempi regressiotestaus, dokumentointi

---

### 3. CHANGELOG.md luonti

Lisää `CHANGELOG.md` (Keep a Changelog -formaatilla):

```markdown
# Changelog

## [1.2.0] - 2026-05-29

### Added
- GitHub Actions workflow for automated DLL builds
- Modern MinGW-w64 (GCC 14+) support
- CI/CD test automation with xUnit

### Fixed
- GCC 10+ -fcommon compatibility
- SAFESEH errors for Win32 builds
- Missing MinGW runtime shims (__mingw_fprintf, etc.)

### Changed
- PlatformToolset updated to v143 (VS 2022)

## [1.1.0] - 2026-05-29

### Added
- xUnit test project for Delta.Wsq
```

**Hyöty:** Käyttäjät näkevät muutokset selkeästi

---

### 4. Dokumentaation parantaminen

#### a) Root README.md
Lisää osiot:
- Build badges (workflow status)
- CI/CD pipeline kuvailu
- Testin ajaminen lokaalisti
- Release notes linkki

#### b) Delta.Wsq/README.md
Lisää:
- Suppported WSQ format spec linkit
- Example usage (load/save kuva)
- API documentation

#### c) BUILDING.md
Lisää:
- Lokaalin buildin step-by-step
- Troubleshooting (GCC versio, MSYS2 setup)
- Performance tips

**Hyöty:** Helpompi omaksua projekti

---

### 5. CI/CD optimointi

#### a) Caching strategia
Nykyinen workflow ei cachaa:
- NBIS-lähdekoodi (muuttuu harvoin)
- MinGW binaarit (MSYS2-cache jo käytössä)

**Ratkaisu:**
```yaml
- name: Cache NBIS source
  uses: actions/cache@v4
  with:
    path: C:\nbis-src-*
    key: nbis-${{ hashFiles('something-that-changes-with-nbis') }}
```

#### b) Parallelisaatio
32-bit ja 64-bit buildaus voi olla paralleeli (ovat jo eri jobissa, mutta voi laajentaa):
- Testit ajetaan nyt 32-bit build jälkeen, voisi olla omassa jobissa

#### c) Artifact cleanup
Vanha master-build DLL:t on GitHub Artifacts -storagessa. Cleanup-policy:
```yaml
retention-days: 30
```

**Hyöty:** Nopempi CI, pienempi storage

---

### 6. Build-konfiguraatioiden hallinta

#### a) Debug-build CI:ssa
Nykyään vain Release rakentuu. Lisää Debug-build PR:ille?

#### b) Static analysis
```yaml
- name: Run Code Analysis
  run: dotnet analyze
```

#### c) Security scanning
```yaml
- name: Dependabot vulnerability scan
  uses: github/super-linter@v5
```

**Hyöty:** Turvallisuus, code quality

---

### 7. Versiointi ja release prosessi

#### a) Automatic versioning
Käytä `semantic-release` tai Git-tagit versionointiin:

```yaml
- name: Create Release
  if: startsWith(github.ref, 'refs/tags/v')
  uses: softprops/action-gh-release@v1
```

#### b) Pre-release handling
Beta/RC-versiot (v1.2.0-rc.1)

**Hyöty:** Johdonmukaiset releases

---

### 8. Koodikehitystapojen dokumentointi

Lisää `.github/CONTRIBUTING.md`:
- PR template
- Code style guidelines
- Testing requirements
- Branch naming strategy

Lisää `DEVELOPERS.md`:
- Project structure overview
- NBIS integration guide
- MinGW shim pattern explanation

**Hyöty:** Helpompi contributor onboarding

---

## 📋 Toteutussuunnitelma

| # | Prioriteetti | Tehtävä | Aika (h) |
|---|---|---|---|
| 1 | 🔴 Korkea | Päivitä GitHub Actions deprecation | 1 |
| 2 | 🟡 Keskimääräinen | Lisää testit reunatapauksille | 3-4 |
| 3 | 🟡 Keskimääräinen | Kirjoita CHANGELOG.md | 1 |
| 4 | 🟡 Keskimääräinen | Paranna dokumentaatiota | 2-3 |
| 5 | 🟢 Matala | CI caching optimointi | 1-2 |
| 6 | 🟢 Matala | Static analysis lisäys | 1 |
| 7 | 🟢 Matala | CONTRIBUTING.md + DEVELOPERS.md | 2 |
| 8 | 🟢 Matala | Release automation | 1-2 |

**Yhteensä:** ~12-15 h

---

## Quick wins (< 30 min each)

1. ✨ **Add CI badge to README** → Shows build status
2. ✨ **Create CHANGELOG.md v1.0 → v1.2.0** → Documents history
3. ✨ **Update Node.js actions** → Prevents CI breakage
4. ✨ **Add .github/PULL_REQUEST_TEMPLATE.md** → Better PRs

---

## Tekniset velat (Tech Debt)

### 1. Platform-specific analyzer warnings
Koodissa on CA1416 varoituksia (GDI+ Windows-only):
```
'Image.Palette' is only supported on: 'windows' 6.1 and later
```

**Fix:**
```csharp
#if WINDOWS
  // GDI+ specific code
#endif
```

### 2. MinGW shim rajoitus
- Max 20 scanf argumenttia
- Dokumentoi `.cpp`-tiedostoon
- Lisää kommentti BUILDING.md:ään

### 3. Dependency updates
- Paint.NET DLL:t voivat olla vanhentuneita
- Tarkista versions requirements

---

## Ei-suunnitellut, mutta hyvät ideat

### 1. Cross-platform build support
- Linux NBIS build (GCC native)?
- macOS support?

### 2. NuGet packaging
Pakkaa `Delta.Wsq.dll` NuGet-paketiksi keskitetylle jakelulle

### 3. Web API wrapper
ASP.NET Core API WSQ codec -ominaisuuksille

### 4. Performance benchmarking
BenchmarkDotNet -integraatio

---

*Dokumentti luotu: 2026-05-29*
*Projektin tila: Production-ready CI/CD, perustestit toiminnassa*
