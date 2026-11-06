# Career-stats offsets from Mosta.bin (sample)

Source analysis of real PC profile `NFS Underground 2/Mosta/Mosta` (54966 bytes, nfsu2.xyz OPFS), matched to in-game Career Status. Little-endian. Also verified on `Templates/JANE`.

Full RE context: `docs/research-careerstats.md`.

## Proven (UI ↔ bytes)

| Field | Offset | Type | Mosta |
|-------|--------|------|-------|
| Bank | `0xA16A` | int32 | 99,999,999 |
| Garage counter | `0xA175` | uint8 | 5 |
| Total Rep Stage 1 | `0x9B43` | float32 | 800.0 |
| Total Rep Stage 2 | `0x9C44` | float32 | 4500.0 |
| Total Rep Stage 3 | `0x9D45` | float32 | 7500.0 |
| Total Rep Stage 4 | `0x9E46` | float32 | 9400.0 |
| Total Rep Stage 5 | `0x9F47` | float32 | 11800.0 |
| Rep-races Stage 1..5 | rep+4 | uint8 | 5 / 19 / 34 / 45 / 49 |

Packed stage table: base `0x9962`, stride `0x101`. Relative to rep float: +4 = race count; +0x1C/+0x1D (= record +0xFC/+0xFD) = finished + stage number.

**Average Reputation is not stored** = Total Rep / race count (Mosta UI exact).

## Probable

| Field | Offset | Type | Note |
|-------|--------|------|------|
| Total Spent slot *k* (0..3) | `0x6720 + k×0x7F2` | int32 | = SAVE_REC+`0xEB8`. Mosta s0=119850; Usman s0=17800, s2=102950. UI = current car. |
| Total Spent slot 4 | `0x86E8` | uint32 | **unverified** — both samples `2520688044` (not Spent) |
| current_car_slot | — | — | **unverified** — no disk write |
| Races Entered | `0xA16F` | int32 LE | **proven**: Mosta **246**, Usman **223** |
| unknown, after Bank | `0xA16E` | uint8 | Sample value 1 — RAW only |
| unknown_near_bank | `0xA173` | uint8 | Mosta 0, Usman 1 — RAW only |
| Rep Stage *n* | `0x9B43 + (n-1)×0x101` | float32 | **proven** (Mosta + Usman); races @ +4 |

Stride note: `0xFE4` (= 2×`0x7F2`) skips odd slots — Usman’s 102950 is slot **2**, not slot 1.

## Not fixed disk ints

Overall Wins / Win% / per-type wins — still runtime (`Career::UpdateCareerStatsObject`). Win% = Wins / Entered when Wins is known.
