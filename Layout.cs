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
    public const int MaxMoney = 0x7FFFFFFF;
    public const int GarageCarCountOffset = 0xA175;
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
    public const int CareerMoneyMarkersRel = 0x8768;
    public const int CareerMoneyMarkersLen = 256;
    /// <summary>Career.field_8869[8][2] — normaliter allemaal 1.</summary>
    public const int CareerField8869Rel = 0x8869;
    public const int CareerField8869Len = 16;

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
}
