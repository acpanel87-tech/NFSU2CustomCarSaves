namespace NFSU2CustomCarSaves;

public static class Layout
{
    public static readonly byte[] CareerMagic = [0x32, 0x30, 0x43, 0x4D]; // 20CM
    public static readonly byte[] GarageMagic = [0x4E, 0x32, 0x47, 0x41]; // N2GA

    public const int HeaderSize = 0x1C;
    public const int ProfileSize = 54966;
    public const int NameOffset = 0xD220 + 5; // in-game profile name
    public const int NameLength = 7;
    public const int CareerSlotCount = 5;
    public const int MaxSlotCount = 10;
    public const int SlotStride = 0x7F2;          // 2034
    public const int TunedCarSize = 0x430;        // 1072
    public const int InventorySize = SlotStride - TunedCarSize; // 962
    /// <summary>Career TunedCar start (SAVE_REC start is 4 bytes earlier @ 0x5868).</summary>
    public const int CareerTunedBase = 0x586C;
    public const int SaveRecPrefix = 4;
    /// <summary>Editor SLOT_BASE relative to SAVE_REC (= +8), hackch/MemphisRider.</summary>
    public const int SlotEditorFromSaveRec = 8;
    public const int CarHashInTuned = 24;         // from TunedCar; = SAVE_REC+28
    public const int LockInTuned = 0x42C;         // from TunedCar; = SAVE_REC+1072
    public const int BoughtInTuned = 0x42D;       // from TunedCar; = SAVE_REC+1073
    public const int LockInSaveRec = 1072;
    public const int BoughtInSaveRec = 1073;
    public const int MoneyOffset = 0xA16A;
    /// <summary>
    /// Career cash int32 @ MoneyOffset. Practical in-game / editor ceiling is $999,999,999
    /// (Starladdey + community save tools). Raw int32 can go higher, but the game UI treats
    /// nine 9s as the usable max bank.
    /// </summary>
    public const int MaxMoney = 999_999_999;
    /// <summary>
    /// Starter cash for yellow Start (post-Rachel freeroam base). Not MaxMoney —
    /// 100% Save / Stats wizard can raise it later.
    /// </summary>
    public const int FreshStartMoney = 10_000;
    /// <summary>
    /// Single byte immediately after Bank (sample value 1). Unknown — RAW label only.
    /// </summary>
    public const int UnknownAfterBankOffset = 0xA16E;
    /// <summary>
    /// Byte near bank after Entered int32 (Mosta=0, Usman=1). Unknown — RAW only.
    /// </summary>
    public const int UnknownNearBankOffset = 0xA173;
    /// <summary>
    /// Proven Races Entered (int32 LE, unaligned). Mosta=246, Usman=223 after Bank.
    /// Safe UI clamp 999 (PS2 cheat, unverified); Extended writes full uint32 range.
    /// </summary>
    public const int RacesEnteredOffset = 0xA16F;
    /// <summary>PS2 cheat ceiling for races entered — unverified on PC.</summary>
    public const int RacesEnteredSafeMax = 999;
    public const int GarageCarCountOffset = 0xA175;
    /// <summary>Options → Player → Transmission. 0 = Automatic, 1 = Manual (diffed from nfsu2 backups).</summary>
    public const int TransmissionManualOffset = 0xC580;
    /// <summary>Companion flag that flips opposite of <see cref="TransmissionManualOffset"/>.</summary>
    public const int TransmissionAutoFlagOffset = 0xC74F;
    public const int PerfOff = 0x310;             // rel. SlotEditorBase
    public const int PerfCount = 68;
    public const int UniquePerfOff = 0x2E8;       // 9 × uint32
    public const int UniquePerfCount = 9;
    public const int PerfPurchaseOff = 0x586;     // ownership bitset
    /// <summary>5 dyno-setups (Circuit/Drag/Drift/Street X/URL) × 41 bytes — hackch technical.md.</summary>
    public const int DynoOff = 0x354;             // rel. SlotEditorBase
    public const int DynoSetupCount = 5;
    public const int DynoSetupSize = 41;
    public const int DynoTotalSize = DynoSetupCount * DynoSetupSize; // 205


    // Career progress (file) — CareerSaveBase = MoneyOffset - 0x7134
    public const int CareerSaveBase = MoneyOffset - 0x7134; // 0x3036
    public const int CareerCurrentStageRel = 0x7AF8;
    public const int CareerNumUsed7B00Rel = 0x7AFC;
    public const int CareerStage0Rel = 0x72D8;
    public const int CareerStageSize = 0x104;
    public const int CareerStageCount = 8;
    public const int CareerStageFinishedRel = 0xFC;
    public const int CareerIsStartedRel = 0x873C;
    public const int Career7B00Rel = 0x7B00;
    public const int Career7B00Count = 384;
    public const int Career7B00Stride = 8;
    /// <summary>Career7148 (nfsu2-re) — race progress / hash table before stages.</summary>
    public const int Career7148Rel = 0x7148;
    public const int Career7148Size = 0x190;
    /// <summary>8-byte race rows packing finish place in byte+5 (1=win, 5=loss in Jane/DYNO dumps).</summary>
    public const int Career7148RaceStride = 8;
    public const int Career7148RaceBytes = 0x180; // field_0[128] + raceHash area
    public const int CareerMoneyMarkersRel = 0x8768;
    public const int CareerMoneyMarkersLen = 256;
    /// <summary>Career.field_8869[8][2] — normaliter allemaal 1.</summary>
    public const int CareerField8869Rel = 0x8869;
    public const int CareerField8869Len = 16;
    /// <summary>Career887C — outrun/sponsor/visual path state (OnOutrunFinish, SponsorSigned).</summary>
    public const int Career887CRel = 0x887C;
    public const int Career887CSize = 0x190;

    /// <summary>
    /// Packed on-disk stage records (Mosta/Jane UI proof). Not the opaque 0xA30E×0x104 blob.
    /// Total Reputation float @ base + stageIndex(1..5)*stride + 0xE0; race count uint8 at float+4.
    /// </summary>
    public const int PackedStageBase = 0x9962;
    public const int PackedStageStride = 0x101;
    public const int PackedStageRepRel = 0xE0;
    public const int PackedStageRepRacesRel = 0xE4; // = RepRel + 4
    public const int PackedStageFinishedRel = 0xFC;
    public const int PackedStageIndexRel = 0xFD;
    public const int PackedStageMinIndex = 1;
    public const int PackedStageMaxIndex = 5;

    /// <summary>
    /// Probable Total Spent On Car for garage slot 0 (Mosta=119850, Usman=17800).
    /// Per-slot: base + slot × <see cref="SlotStride"/> (= SAVE_REC + <see cref="TotalSpentOnCarRelInSaveRec"/>).
    /// Editable slots 0…3; slot 4 reads constant junk (0x954C25AC) in Mosta/Usman — unverified.
    /// </summary>
    public const int TotalSpentOnCarBase = 0x6720;
    /// <summary>Spent field relative to SAVE_REC start (0x6720 − 0x5868).</summary>
    public const int TotalSpentOnCarRelInSaveRec = 0xEB8;
    /// <summary>Editable Total Spent slots (0…3). Slot 4 is unverified / not Spent.</summary>
    public const int TotalSpentEditableSlotCount = 4;
    /// <summary>Alias for slot 0 base — prefer <see cref="TotalSpentOnCarOffsetForSlot"/>.</summary>
    public const int TotalSpentOnCarOffset = TotalSpentOnCarBase;

    public const int GarageHeaderSize = 32;
    public const int GarageSize = GarageHeaderSize + CareerSlotCount * SlotStride; // 10202
    public const int ProfilePayloadOff = 36;
    public const int ProfileCarsPartSize = 39230;
    public const int ExtraSlotRecordBase = ProfileSize;

    public static int CareerRecordOffset(int slot0to4) =>
        CareerTunedBase + slot0to4 * SlotStride;

    /// <summary>
    /// Absolute start of the on-disk SAVE_REC (0x7F2).
    /// Career: 0x5868 + n×stride. Extra (GARAGE.DAT body): ExtraSlotRecordBase + n×stride.
    /// </summary>
    public static int SaveRecOffset(int slot0to9)
    {
        if (slot0to9 < 0 || slot0to9 >= MaxSlotCount) throw new ArgumentOutOfRangeException(nameof(slot0to9));
        if (slot0to9 < CareerSlotCount)
            return (CareerTunedBase - SaveRecPrefix) + slot0to9 * SlotStride;
        return ExtraSlotRecordBase + (slot0to9 - CareerSlotCount) * SlotStride;
    }

    /// <summary>TunedCar start (= SAVE_REC + 4). Career editors historically use this.</summary>
    public static int TunedOffset(int slot0to9) => SaveRecOffset(slot0to9) + SaveRecPrefix;

    /// <summary>hackch SLOT_BASE (= SAVE_REC + 8). Perf/unlock offsets are relative to this.</summary>
    public static int SlotEditorOffset(int slot0to9) => SaveRecOffset(slot0to9) + SlotEditorFromSaveRec;

    /// <summary>Legacy name: TunedCar offset for career; for extras also TunedCar (SAVE_REC+4).</summary>
    public static int ExtendedRecordOffset(int slot0to9) => TunedOffset(slot0to9);

    /// <summary>
    /// Total Spent offset for career garage slot 0..4 at SAVE_REC + 0xEB8
    /// (= <see cref="TotalSpentOnCarBase"/> + slot × <see cref="SlotStride"/>).
    /// Writes are only safe for slots &lt; <see cref="TotalSpentEditableSlotCount"/>.
    /// </summary>
    public static int TotalSpentOnCarOffsetForSlot(int slot0to4)
    {
        if (slot0to4 < 0 || slot0to4 >= CareerSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot0to4), "Spent slot index must be 0…4.");
        return SaveRecOffset(slot0to4) + TotalSpentOnCarRelInSaveRec;
    }
}
