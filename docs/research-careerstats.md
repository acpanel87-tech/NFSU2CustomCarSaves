# Research: CareerStats vs 54966-byte career save

Read-only research (2026-11-06). No save-writer code changed.  
Repos cloned beside the CCS repo (not inside the app tree):

| Repo | Local path | Role |
|------|------------|------|
| [yugecin/nfsu2-re](https://github.com/yugecin/nfsu2-re) | `../nfsu2-re-readonly` | structs / funcs / vars / SPEED2.CT |
| [hackch/NFSU2-Savegame-Editor](https://github.com/hackch/NFSU2-Savegame-Editor) | `../hackch-nfsu2-sge-readonly` | Inspiration only (MIT) — layout ideas, **no code copied** |
| [Starladdey/nfsu2-save-editor](https://github.com/Starladdey/nfsu2-save-editor) | `../starladdey-nfsu2-se-readonly` | Inspiration only (MIT) — product UX / known shop-unlock surface |

Primary sources in nfsu2-re: `docs/structs.html`, `docs/vars.html`, `docs/funcs.html`, `SPEED2.CT`.  
CCS anchor save: `Templates/JANE` (54966 bytes, magic `20CM`).

---

## 1. Verdict (short)

**`struct CareerStats` is a runtime UI cache, not a block inside the career save.**

- Global: `careerStats` @ RAM **`0x835C00`**, size **`0x21C`** (`docs/vars.html`, `structs.html`).
- Filled by `Career::UpdateCareerStatsObject` (`0x528A60`) from `profileData.career`.
- Consumed by `StatusCareerFNGObject::CreateStatsUIData` (`0x4E13C0`).
- Therefore **Races Entered / Overall Wins / Win% / per-type wins / magazines&DVD totals / region N/N / completion float** in the Career Status UI are **not** “ints next to Bank” on disk.

Persistent progress lives in **`struct Career`** (and garage cars). Stats UI aggregates those blobs at load/open-status.

---

## 2. Memory map (nfsu2-re)

### ProfileData / Career

| Item | Value | Source |
|------|-------|--------|
| `profileData` | RAM `0x83A9D0` | `a_fields.c`, SPEED2.CT, vars |
| `ProfileData.career` | +**`0x20370`** | `struc_ProfileData@20370` |
| `Career` size | `0x8A0C` | `EXPECT_SIZE` |
| `Career.smsMessageList` | +**`0x5D20`** | size `0x1414` |
| `Career.bank` (`CareerBank.money`) | +**`0x7134`** | int32 |
| `Career.field_7138` | +`0x7138` | char |
| `Career.field_713C` (`Career713C`) | +`0x713C` | size `0xA` |
| `Career713C.field_8` | +`0x7144` | char (CCS completion flag) |
| `Career.field_7148` | +`0x7148` | size `0x190` |
| `Career.stage0`…`stage7` | +`0x72D8`, stride **`0x104`** | `CareerStage` |
| `Career.currentStage` | +`0x7AF8` | int |
| `Career.numUsed7B00` | +`0x7AFC` | int |
| `Career.field_7B00[384]` | +`0x7B00`, stride 8 | event/shop rows |
| `Career.isCareerStarted` | +`0x873C` | char |
| `Career.field_887C` | +`0x887C` | size `0x190` |

`smsMessageList` ends at `0x5D20+0x1414 = 0x7134` → exactly **`bank`**. That packing is solid in RE.

### CareerStats (RAM only)

```
struct CareerStats { /* size 0x21C */
  +0x00 int   money;
  +0x04 int   racesEntered;
  +0x08 int   overallWins;
  +0x0C int   winPercent;          // comment [0-1]
  +0x10 int   circuitWins;
  +0x14 int   sprintWins;
  +0x18 int   dragWins;
  +0x1C int   driftWins;
  +0x20 int   streetxWins;
  +0x24 int   urlWins;
  +0x28 int   magazinesWon;
  +0x2C int   magazinesToWin;
  +0x30 int   sponsorsSigned;
  +0x34 CareerStatsHiddenShopsDiscovered[8]; // {int16 discovered, int16 numShops}
  +0x54 float partsUnlocked;       // [0-1]
  +0x68 float gameCompletionStatus;// [0-1]
  +0x70 int   regionWorldEventWins / Max;
  +0x78 int   regionSponsorEventWins / Max;
  +0x80 int   regionURLEventWins / Max;
  +0x88 int   dvdCoversWon / ToWin;
  +0x90 int   regionDVDCovers / Max;
  +0x98 float regionCompletion;    // [0-1]
  +0x9C CareerStatsRegionDvdCovers[8]; // cover[4] {float star, char didGet}, numCovers
  +0x1DC float stageTotalReputation[8];
  +0x1FC float stageAverageReputationPerRaceForStage[8];
};
```

**Not present in CareerStats:** Visual Rating, Total Spent On Car (those are elsewhere / computed).

### CareerStage (RAM layout)

| Rel | Type | RE name |
|-----|------|---------|
| +0x00… | opaque hash areas | `field_0` / `8C` / `9C` |
| +0xC4 | `uint32[6]` | `showcases` (magazines/DVDs, “probably hashes”) |
| +0xDC | int | `outrunsRaced` |
| +0xE0 | float | `totalReputation` |
| +0xE4 | int | `racesFinished` (also used for avg rep) |
| +0xE8 | ptr | `signedSponsor` (RAM) |
| +0xFC | char | `stageFinished?` |
| +0x100 | ptr | `settings` (RAM) |

`CareerStage::GetAverageReputationPerRace` / `IncreaseRacesFinishedAndAddReputation` confirm rep is **stored per stage**, then copied into `CareerStats` arrays by `UpdateCareerStatsObject`.

### Other RE notes

| Topic | Finding |
|-------|---------|
| Hidden shops in 7B00 | `Career7B00`: hash + flags; `field_4` must be 1 for map visibility; `field_6` “finished (won?)” for races |
| Spent | `CareerCar434.moneySpentOnCar` @ +`0x3BC` (per career car), updated in `Career::HandleMoneyAfterPartBought` — **not** in `CareerStats` |
| Visual | `CalculateVisualRep` (`0x5234C0`) — computed, not a CareerStats field |
| Magazines/DVD path | Stage `showcases`, `Career887C` (`SponsorSigned`, `OnOutrunFinish`), SMS DVD helpers — aggregates still land in CareerStats at runtime |

### Career7148 (RE vs CCS heuristic)

RE struct: `field_0[128]` + `raceHash[64]` @ +`0x80` + tail `0x10` (size `0x190`).  
CCS `ForcePerfectWinRecord` treats the first `0x180` bytes as **8-byte rows** with finish place in byte+5 (observed in Jane/DYNO). Both views can be true operationally (row packing overlapping the hash area); do not assume RE `raceHash[]` alone stores win/loss.

---

## 3. Mapping RAM Career → 54966-byte save

### Proven anchor (CCS + RE + Jane)

CCS: `CareerSaveBase = MoneyOffset - 0x7134 = 0x3036`  
Formula: **`saveAbs ≈ 0x3036 + careerRel`** for the bank-aligned region.

| Career rel | Save abs | Jane (`Templates/JANE`) | Confidence |
|------------|----------|-------------------------|------------|
| `bank.money` `0x7134` | **`0xA16A`** | int32 LE **`99,747,225`** (`99 05 F2 05`) | **Bewezen in save** |
| `field_7138` `0x7138` | `0xA16E` | `01` | Unknown byte after Bank (RAW only) |
| *(proven)* Races Entered | **`0xA16F`** | int32 LE Mosta **`246`**, Usman **`223`** | Unaligned; two-save proof |
| *(probable)* Total Spent slot *k* | **`0x6720 + k×0x7F2`** | int32 | SAVE_REC+`0xEB8`; UI = current car |
| `Career713C` `0x713C` | `0xA172` | field_0 bytes `00 00 01 06` | RE layout (MSB of Entered when value &lt; 2²⁴) |
| `Career713C.field_8` `0x7144` | **`0xA17A`** | **`01`** (CCS complete flag) | **Bewezen in save** |
| `field_7148` `0x7148` | `0xA17E` | non-zero race area | CCS + Jane |
| `isCareerStarted` `0x873C` | `0xB772` | `01` | **Bewezen in save** |
| `field_7B00` `0x7B00` | `0xAB36` | ~137 non-zero rows | CCS + Jane |
| `field_887C` `0x887C` | `0xB8B2` | Jane-copied by MaxOut | CCS |

So yes: **`profileData.career` / `Career.smsMessageList (0x5D20)` align with the same Career-relative coordinate system that puts bank at `0xA16A`.** The save is not a raw dump of all of `ProfileData` (header, cars, checksums differ), but **Career offsets from `smsMessageList`/`bank` onward match CCS.**

`Career.currentStage` @ save `0xAB2E`: Jane reads **`0`**. RE says this is the stage index; a finished free-play template may legitimately sit at 0, or the field is refreshed on load. **Do not treat Jane’s 0 as “mapping broken” without a mid-career sample.**

### Important conflict: byte `0xA175`

| Interpretation | Source |
|----------------|--------|
| Garage car count (uint8) | CCS `Layout.GarageCarCountOffset` — Jane = **6** (Jane has 6 cars) |
| `currentStage` uint32 | hackch `CURRENT_STAGE_OFF = 0xa175` — would also read **6** |
| High byte of `Career713C.field_0` | RE: field_0 starts `@0xA172` → `A175` is MSB of that int |

**Confidence: ambiguous.** RE places true `currentStage` at **`0xAB2E`**, not `0xA175`. CCS garage sync writing a single byte at `0xA175` may be coincidentally correct for UI or may corrupt `Career713C.field_0`. Needs a controlled sample (change car count only / change stage only). **Schema: leave as-is; do not invent a second offset.**

### Stage reputation on disk ≠ `0xA30E + 0x104*i` (CCS Layout stage blob)

CCS copies Jane “stages” at **`0xA30E`** (`CareerSaveBase + 0x72D8`), size `8×0x104`. Interpreting that region with RAM `CareerStage` field offsets (`+0xE0` float, `+0xE4` int) yields **garbage** in Jane.

Separately, Jane contains a **second** stage table (documented by hackch as inspiration; offsets verified in our Jane bytes):

| | |
|--|--|
| Base | **`0x9962`** |
| Stride | **`0x101`** (257) — 3 bytes shorter than RAM `0x104` (pointers/pad likely stripped by `WriteToSaveData`) |
| Stage 1 `+0xE0` | float **800** @ `0x9B43` |
| Stage 2 | **4500** @ `0x9C44` |
| Stage 3 | **7500** |
| Stage 4 | **9400** @ `0x9E46` |
| Stage 5 | **11800** @ `0x9F47` |
| Matching `+0xE4` | ints 5, 19, 34, 45, 49 (plausible `racesFinished`) |
| `+0xFC` | `01` finished flags on stages 1–5 |

`0x9962` falls **inside** the Career-relative `smsMessageList` span if you naïvely apply `0x3036 + rel`. Conclusion: **save serialization is not a pure memcpy of in-memory `Career` for every subobject** — bank/713C/7148/7B00/887C line up; **packed stage records live at absolute `0x9962`.** Treat `0xA30E` as an opaque CCS/Jane progress blob (still required for MaxOut coherence), not as the reputation float table.

**UI verification still required** before marking stage rep as `proven` in the schema (confirm Career Status “Total Reputation — Stage N” equals those floats).

---

## 4. Hypothesis check: “stats come from Jane blobs”

| Claim | Result |
|-------|--------|
| Entered / Wins / Win% / type wins are **not** disk ints beside Bank | **Confirmed** — they exist on `CareerStats` @ `0x835C00`, filled by `UpdateCareerStatsObject` |
| Game still **derives** them from career progress blobs | **Confirmed by architecture** — function takes `Career*`, UI reads `careerStats` |
| Stage Total / Avg Reputation are **only** runtime | **Partially false** — RAM `CareerStage.totalReputation` / `racesFinished` are real fields; Avg is computed (`GetAverageReputationPerRace`). On disk, Jane shows packed floats at **`0x9962` table** (pending UI confirm) |
| Magazines / DVD / sponsors as CareerStats counters | **Runtime aggregates**; sources in stage showcases / 887C / SMS — no per-counter ints proven next to money |
| Visual Rating / Spent | **Not in CareerStats**; spent on `CareerCar434`; visual via `CalculateVisualRep` |

CCS approach (Jane coherent copy + win-row flips) remains the safe way to make the **runtime** CareerStats UI look “fully modded” without writing the RAM struct into the save.

---

## 5. Field ledger (wishlist × evidence)

Legend — confidence: **bewezen in save** / **uit RE-docs** / **gok** / **Jane-bytes (UI open)**.

| UI / schema id | Where it lives | Confidence | Still need in our saves |
|----------------|----------------|------------|-------------------------|
| Bank | CareerBank @ save `0xA16A` | **bewezen in save** (Jane `99747225`) | — |
| Game Completion (flag) | Career713C.field_8 @ `0xA17A` | **bewezen in save** | Pair: mid-career flag 0 vs 1 vs UI % |
| Game Completion % (float) | CareerStats+0x68 only | **uit RE-docs** (runtime) | — |
| Hidden shops aggregate | save `0xAD9A`×58 (+ 7B00 discovery) | **bewezen in save** (CCS) | District index map |
| Extreme tiers / car lot / TX / slots | existing CCS offsets | **bewezen in save** | — |
| Garage counter @ `0xA175` | CCS vs RE/hackch conflict | **gok / conflict** | Car-count-only vs stage-only diffs |
| Races Entered | CareerStats+4 | **uit RE-docs** (runtime) | Optional: prove absence near money after 1 race |
| Overall Wins | CareerStats+8 | **uit RE-docs** (runtime) | idem |
| Win% | CareerStats+0xC | **uit RE-docs** (runtime); CCS flips rows | — |
| Circuit…URL wins | CareerStats+0x10…0x24 | **uit RE-docs** (runtime) | Pair A: one win per type → which blob row + UI |
| Magazines / DVD / sponsors counts | CareerStats + sources in stage/887C | **uit RE-docs** | Pair E + magazine claim |
| Total Reputation stage N | packed `0x9962+N*0x101+0xE0` | **bewezen in save** (Mosta UI) | Write test → `writable:true` |
| Rep-races stage N | rep float + 4 (uint8) | **bewezen in save** (Mosta) | Same write test |
| Avg Reputation stage N | total / races (not stored) | **bewezen formula** (Mosta UI) | — |
| Region event N/N | CareerStats region ints | **uit RE-docs** (runtime) | Pair region event |
| DVD star unlocks | CareerStatsRegionDvdCovers (runtime) + stage/887C | **uit RE-docs** | Pair E |
| Visual Rating | `CalculateVisualRep` | **uit RE-docs** | Per-car visual-only pair |
| Total Spent On Car | CareerCar434+0x3BC | **uit RE-docs**; Jane slot0 spent `0` | Buy parts → non-zero spent |

---

## 6. Other editors (inspiration only)

### hackch/NFSU2-Savegame-Editor

- Same profile size / money @ `0xa16a`.
- Career helper offsets: `STAGE_OFF=0x9962`, `STAGE_STRIDE=0x101`, event table near `0xa17d` — **useful map of packed stage records**; their `CURRENT_STAGE_OFF=0xa175` **conflicts** with RE `currentStage` @ `0xAB2E` and CCS garage byte.
- Shop discovery via 8-byte event rows + BUN database — aligns with 7B00 ideas, absolute event base differs from CCS `0xAB36`.
- **No source copied into CCS.**

### Starladdey/nfsu2-save-editor

- Browser editor: money, slots, car lot, hidden shops, sandbox 100% — same practical surface as CCS MaxOut.
- Bundled Jane asset; minified JS — not used as offset authority.
- **No source copied into CCS.**

---

## 7. Mosta.bin proof (nfsu2.xyz OPFS, 2026-11-06)

Real PC profile `NFS Underground 2/Mosta/Mosta` (54966 bytes), saved with known Career Status values. Little-endian. Same packed-stage numbers also present in `Templates/JANE`.

### Bewezen (UI ↔ bytes exact)

| Veld | Offset | Type | Mosta |
|------|--------|------|-------|
| Bank | `0xA16A` | int32 | 99,999,999 |
| Garage counter | `0xA175` | uint8 | 5 |
| Total Rep Stage 1 | `0x9B43` | float32 | 800.0 |
| Total Rep Stage 2 | `0x9C44` | float32 | 4500.0 |
| Total Rep Stage 3 | `0x9D45` | float32 | 7500.0 |
| Total Rep Stage 4 | `0x9E46` | float32 | 9400.0 |
| Total Rep Stage 5 | `0x9F47` | float32 | 11800.0 |
| Rep-races Stage 1..5 | rep+4 | uint8 | 5 / 19 / 34 / 45 / 49 |

Stage table: base **`0x9962`**, stride **`0x101`**. Relative to each rep float: **`+0x1C` / `+0x1D`** (= record `+0xFC` / `+0xFD`) = finished flag `1` + stage number `1..5`.

**Average Reputation is not stored** = Total Rep / rep-races. Mosta UI match: 160.0, 236.8, 220.6, 208.9, 240.8.

Schema: these fields are **`proven`** with **`writable: false`** until a write+reload test (e.g. Stage 5 → 20000.0).

### Waarschijnlijk

| Veld | Offset | Type | Note |
|------|--------|------|------|
| Total Spent slot *k* | `0x6720 + k×0x7F2` | int32 | Mosta s0=119850; Usman s0=17800 / s2=102950 |

### Niet als vaste ints (Mosta UI)

Entered 246, Wins 222, Win%, Circuit 56 / Sprint 36 / Drift 36 / Drag 34 / Street X 36 / URL 24 — no reliable disk cluster. Loose hits sit inside repeating 8-byte rows (`01 00 <flag 01\|05> 00` + hash). Confirms event-list derivation / `UpdateCareerStatsObject`.

---

## 8. Schema policy

- Promote only Mosta/Jane **UI-matched** disk fields to `proven`.
- Keep Entered/Wins/type wins as `runtime`.
- Spent stays `unverified` at candidate `0x6720`.
- Do **not** treat CCS Layout blob `0xA30E` as the reputation table.

---

## 9. Smallest next tests

1. Export → win **one Sprint** → export → diff (which 8-byte entry + flag 01/05).
2. Repeat: one **loss**, one other type → hash→type map.
3. Write test: Stage 5 rep → `20000.0` in a copy → load in nfsu2.xyz → if UI updates, set `writable:true` on rep + race-count fields.
