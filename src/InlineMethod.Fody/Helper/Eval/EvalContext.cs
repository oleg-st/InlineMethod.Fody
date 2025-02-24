using System.Collections.Generic;

namespace InlineMethod.Fody.Helper.Eval;

public class EvalContext
{
    public Dictionary<Tracker, Value?> Trackers { get; } = [];
    public Value FromNull() => FromI32(0);
    public ValueI32 FromI32(int value) => new(this, value);
    public ValueI32 FromU32(uint value) => new(this, (int)value);
    public ValueI64 FromI64(long value) => new(this, value);
    public ValueI64 FromU64(ulong value) => new(this, (long)value);
    public ValueF32 FromF32(float value) => new(this, value);
    public ValueF64 FromF64(double value) => new(this, value);
    public void StartTracker(Tracker tracker) => Trackers.Add(tracker, null);
    public bool GetTrackerValue(Tracker tracker, out Value? value) => Trackers.TryGetValue(tracker, out value);
    public void FinishTracker(Tracker tracker, Value value) => Trackers[tracker] = value;
}