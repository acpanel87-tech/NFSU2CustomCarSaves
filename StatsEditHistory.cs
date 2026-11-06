namespace NFSU2CustomCarSaves;

/// <summary>One logical field change for the Stats undo list.</summary>
public readonly record struct StatsChangeEntry(
    string FieldId,
    string Label,
    int Offset,
    string OldValue,
    string NewValue);

/// <summary>Undo/redo stack of full-buffer snapshots + change entries (Career Stats).</summary>
public sealed class StatsEditHistory
{
    private readonly List<(byte[] Snapshot, IReadOnlyList<StatsChangeEntry> Changes, string Label)> _undo = new();
    private readonly List<(byte[] Snapshot, IReadOnlyList<StatsChangeEntry> Changes, string Label)> _redo = new();
    private const int MaxDepth = 64;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public IReadOnlyList<StatsChangeEntry> LastChanges { get; private set; } = Array.Empty<StatsChangeEntry>();
    public string LastLabel { get; private set; } = "";

    /// <summary>Call before mutating <paramref name="working"/>.</summary>
    public void RememberBefore(byte[] working, string label)
    {
        Push(_undo, CareerStatsService.Snapshot(working), Array.Empty<StatsChangeEntry>(), label);
        _redo.Clear();
        LastLabel = label;
        LastChanges = Array.Empty<StatsChangeEntry>();
    }

    /// <summary>Call after mutate; diffs against the remembered before-state.</summary>
    public StatsEditResult CommitAfter(byte[] working, string label)
    {
        CareerStatsService.EnsureCareerChecksums(working);
        if (_undo.Count == 0)
        {
            var empty = CareerStatsService.Diff(working, working, label);
            return empty;
        }
        byte[] before = _undo[^1].Snapshot;
        var entries = CareerStatsService.DescribeChanges(before, working);
        _undo[^1] = (before, entries, label);
        LastChanges = entries;
        LastLabel = label;
        return CareerStatsService.Diff(before, working, label);
    }

    /// <summary>One-shot: snapshot → mutate → CRC → change list.</summary>
    public StatsEditResult Apply(byte[] working, string label, Action mutate)
    {
        RememberBefore(working, label);
        mutate();
        return CommitAfter(working, label);
    }

    public StatsEditResult Undo(byte[] working)
    {
        if (!CanUndo) return new StatsEditResult("nothing to undo", 0, "", CareerStatsService.CareerChecksumsOk(working));
        var current = CareerStatsService.Snapshot(working);
        var (snap, changes, label) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Push(_redo, current, changes, label);
        Buffer.BlockCopy(snap, 0, working, 0, Math.Min(snap.Length, working.Length));
        CareerStatsService.EnsureCareerChecksums(working);
        LastChanges = changes;
        LastLabel = "undo: " + label;
        return CareerStatsService.Diff(current, working, LastLabel);
    }

    public StatsEditResult Redo(byte[] working)
    {
        if (!CanRedo) return new StatsEditResult("nothing to redo", 0, "", CareerStatsService.CareerChecksumsOk(working));
        var current = CareerStatsService.Snapshot(working);
        var (snap, changes, label) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Push(_undo, current, changes, label);
        Buffer.BlockCopy(snap, 0, working, 0, Math.Min(snap.Length, working.Length));
        CareerStatsService.EnsureCareerChecksums(working);
        LastChanges = changes;
        LastLabel = "redo: " + label;
        return CareerStatsService.Diff(current, working, LastLabel);
    }

    public IEnumerable<string> FormatChangeLines()
    {
        foreach (var e in LastChanges)
            yield return $"{e.Label} ({e.FieldId}) @ 0x{e.Offset:X}: {e.OldValue} → {e.NewValue}";
    }

    private static void Push(
        List<(byte[] Snapshot, IReadOnlyList<StatsChangeEntry> Changes, string Label)> stack,
        byte[] snap,
        IReadOnlyList<StatsChangeEntry> changes,
        string label)
    {
        stack.Add((snap, changes, label));
        while (stack.Count > MaxDepth)
            stack.RemoveAt(0);
    }
}
