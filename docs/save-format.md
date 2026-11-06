# NFSU2 career save format notes (CCS)

Document-only research for Career Stats. **No save-code changes in this step.**  
Sources: `Layout.cs`, `SaveMaxOut.cs`, Jane template dumps, nfsu2-re comments already in CCS.

## Profile

| Item | Value |
|------|--------|
| Size | **54966** bytes (`Layout.ProfileSize`) |
| Magic | `20CM` career / `N2GA` garage |
| Career base | `CareerSaveBase = MoneyOffset - 0x7134` → **`0x3036`** |

Absolute career field ≈ `0x3036 + relative`.

---

## Hypothesis: Overall Career Statistics at load

**Claim:** UI values such as Races Entered, Overall Wins, Win%, Races Won (Circuit…URL), Average Rep, and often Completion % are **not** stored as simple ints next to Bank. They are filled at runtime by something like `Career::UpdateCareerStatsObject` from **event/race rows** and related career blobs.

**CCS evidence:**

- Money @ `0xA16A` is a real int32.
- Byte `@ 0xA16E` = unknown after Bank; `@ 0xA173` = `unknown_near_bank` (Mosta 0 / Usman 1) — RAW only. **Races Entered** proven `@ 0xA16F`. **Total Spent** probable for slots **0..3** `@ 0x6720 + k×0x7F2`; slot **4** unverified (constant `2520688044`). Wins / type / magazines / DVD / sponsorships = unverified/derived.
- `ForcePerfectWinRecord` only flips **known row patterns** so the game can recompute Win% ≈ 100%.
- Magazines / DVD / sponsors are applied as **opaque Jane copies** of `Career7148` + `Career887C`, not field-by-field editors.

**Conclusion:** Treat Overall Status race/rep/spent/visual counters as **runtime** until a controlled diff proves otherwise. Prefer Jane coherent copies + win-row flips for “fully modded” progress.

---

## Blob layouts (proven structure, opaque meaning)

### Career7148 — race hash / finish table

| | |
|--|--|
| Absolute | **`0xA17E`** (`0x3036 + 0x7148`) |
| Size | `0x190` (400) |
| Race area | first `0x180` bytes |
| Row | **8 bytes**: `uint32 hash` LE + 4 flag bytes |
| Observed | `hash≠0`, flags `00 05 00 xx` → **loss**; CCS flips byte+5 `5→1` → **win** |

Jane sample (Templates/JANE): ~48 non-zero rows, ~11 loss-like, ~33 win-like before MaxOut win-force.

**Not proven:** which hash = Circuit vs Sprint vs Drift, etc.

### Career stages — 8 × `0x104`

| | |
|--|--|
| Absolute stage0 | **`0xA30E`** |
| Count / size | 8 × 260 bytes = 2080 |
| Finished rel | `+0xFC` inside each stage |

Jane `+0xFC` samples are **not** clean 0/1 booleans (e.g. `00 01 00 30`, `11 5E AE 00`, `BE FA 49 02`).  
**Do not poke** these as “stage finished” flags — known to re-trigger Rachel / break shops.

**Not the reputation table.** Total Rep / race counts live in the **packed** stage table at absolute **`0x9962`**, stride **`0x101`** (proven via Mosta UI + Jane; see `docs/research-careerstats.md`). Avg Rep = Total / races (not stored).

### Career7B00 — event table

| | |
|--|--|
| Absolute | **`0xAB36`** |
| Rows | 384 × **8 bytes** |
| Row | `uint32 hash` + flags `[+4..+7]` |
| Win flip | `1,0,0,1` → `1,1,0,1` (field_5 = finished/won, nfsu2-re) |

Jane: ~137 non-zero; ~44 with `1,0,0,1`; ~11 with `1,1,0,1`.

**Critical overlap:** Hidden shops `0xAD9A`…+58 sit **inside** the 7B00 span. Apply Jane 7B00 first, then force shops; win-force must **skip** the shop window.

### Career887C — sponsors / outrun / visual path

| | |
|--|--|
| Absolute | **`0xB8B2`** |
| Size | `0x190` (400) |

Opaque. SelfTest expects byte-identity to Jane after MaxOut. First bytes in Jane start `01 00 00 01 …`.  
Believed home of sponsorship / outrun / magazine-DVD **path** state — **no per-item map yet**.

### World + hidden shops

| | Absolute | Len |
|--|----------|-----|
| World | `0xAD80` | 26 |
| Hidden shops | `0xAD9A` | **58** bytes 0/1 |

Jane unlocks **51/58**; MaxOut forces all 58. District UI (City Core 5/5 …) is **not** mapped to flag indices yet.

### Car lot

`0xBC` … `0x300` (580 bytes) — Jane shop-stock table (floats/hashes). Safe edit = Jane copy.

### Scalars (proven)

| Field | Absolute | Type | Game-useful max |
|-------|----------|------|-----------------|
| Bank | `0xA16A` | int32 | **999,999,999** |
| Garage count | `0xA175` | uint8 | sync 1…10 |
| Complete | `0xA17A` | uint8 | 0/1 |
| IsStarted | `0xB772` | uint8 | 0/1 |
| Tx manual | `0xC580` | uint8 | 0/1 (+ companion `0xC74F`) |
| Shop tiers | `0xC45B`… / `0xC483`… stride 8 | uint8 | Extreme = **3** |
| Slot lock | SAVE_REC+**1072** | uint8 | 0 unlocked |

---

## What CCS can already set / influence

| Goal | How (safe) |
|------|------------|
| Max Bank | Write `0xA16A` → 999,999,999 |
| Completion flag | `0xA17A` = 1 (+ Jane career for real progress) |
| ~100% Win% | Flip 7148 losses + 7B00 `1,0,0,1` rows |
| Magazines/DVD/sponsors (bulk) | Jane copy 7148+887C |
| Stage progress / free-play | Jane stages + 7B00 (+ markers) coherent |
| All shops | 58 bytes = 1 (+ Jane world) |
| Extreme parts catalogs | Tier bytes → 3 |
| Car lot | Jane `0xBC`…`0x300` |
| Slots / garage counter | Unlock locks; `UpdateGarageCarCount` |
| Transmission | Manual/Auto pair |

## What needs sample saves (no invented offsets)

| Target | Why | What you should capture |
|--------|-----|-------------------------|
| Races Won per type | Hash→type unknown | See checklist below |
| Entered / Wins / Win% as ints | Suspected runtime-only | Diff after one race won’t show ints near money; still useful to **confirm** |
| Total / Avg reputation | **Read-proven** @ `0x9B43`… + races @ +4; avg derived | Write test: Stage 5 → 20000.0 |
| Entered / type wins | Event-list derived | One Sprint win / one loss diffs |
| District shop groups | 58 flags unmapped | Diff before/after discovering one district’s shops |
| Region event N/N | Event→region unknown | Diff before/after one World/Sponsor/URL event |
| DVD star unlocks | Flag layout unknown | Diff before/after unlocking DVD #1 only |
| Visual Rating (global) | Likely per-car | Diff same career, only car visual stars changed |

---

## How to make useful sample pairs (in-game)

Use **PC or nfsu2.xyz**, same profile name. Export zip / copy career file **before** and **after**. Keep backups.

### Pair A — one race type
1. Save A0: mid-career, note Overall Stats + Races Won.
2. Win **exactly one Circuit** (or only Sprint / only Drift…), exit to menu, Save A1.
3. Repeat for each type if possible (Circuit, Sprint, Drift, Drag, Street X, URL) → six pairs.

### Pair B — win% / entered
1. Save B0.
2. Enter a race and **lose** once → B1.
3. Enter and **win** once → B2.  
   (Confirms row flips vs fake ints.)

### Pair C — reputation
1. Save C0 at known Stage N rep.
2. Play until Total Reputation for that stage visibly increases → C1.

### Pair D — hidden shops (one district)
1. Save D0 with City Core incomplete if possible.
2. Discover **one** City Core shop only → D1.

### Pair E — DVD unlock
1. Save E0 without DVD Cover #1.
2. Meet 7.0 star requirement and claim → E1.

Deliver pairs as zip exports or raw 54966-byte career files + a one-line note of what changed on screen.

---

## Related CCS files

- Schema: `Stats/stats.schema.json` (this step)
- RE follow-up: `docs/research-careerstats.md` (CareerStats RAM @ `0x835C00`, packed stage rep candidates @ `0x9962`)
- Code constants: `Layout.cs`, `SaveMaxOut.cs`
- Writers today: `CareerStatsService`, `PlayerOptionsService`, `GarageDatService`
