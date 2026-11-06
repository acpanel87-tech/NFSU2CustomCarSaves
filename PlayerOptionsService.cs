namespace NFSU2CustomCarSaves;

/// <summary>
/// In-game Options → Player settings stored in the career profile.
/// Transmission mapped from Automatic/Manual backup diffs (Mosta 2026-11-05).
/// </summary>
public static class PlayerOptionsService
{
    public enum TransmissionMode
    {
        Automatic = 0,
        Manual = 1,
    }

    public static bool BufferHasTransmission(byte[] working) =>
        working.Length > Layout.TransmissionAutoFlagOffset;

    public static TransmissionMode GetTransmission(byte[] working)
    {
        if (!BufferHasTransmission(working))
            return TransmissionMode.Automatic;
        return working[Layout.TransmissionManualOffset] != 0
            ? TransmissionMode.Manual
            : TransmissionMode.Automatic;
    }

    public static void SetTransmission(byte[] working, TransmissionMode mode)
    {
        if (!BufferHasTransmission(working))
            throw new InvalidOperationException("Save buffer too small for Player → Transmission.");
        bool manual = mode == TransmissionMode.Manual;
        working[Layout.TransmissionManualOffset] = (byte)(manual ? 1 : 0);
        working[Layout.TransmissionAutoFlagOffset] = (byte)(manual ? 0 : 1);
    }

    public static bool BufferHasMoney(byte[] working) =>
        working.Length >= Layout.MoneyOffset + 4;

    public static int GetMoney(byte[] working)
    {
        if (!BufferHasMoney(working)) return 0;
        return BitConverter.ToInt32(working, Layout.MoneyOffset);
    }

    /// <summary>Clamp to 0 … MaxMoney (game int32 ceiling).</summary>
    public static void SetMoney(byte[] working, int amount)
    {
        if (!BufferHasMoney(working))
            throw new InvalidOperationException("Save buffer too small for money.");
        if (amount < 0) amount = 0;
        if (amount > Layout.MaxMoney) amount = Layout.MaxMoney;
        BitConverter.GetBytes(amount).CopyTo(working, Layout.MoneyOffset);
    }
}
