namespace BatteryBuddy.Pet.Ui;

/// <summary>Counts quick successive clicks: Register says true on the Count-th click with no gap longer than MaxGap.</summary>
public sealed class ClickStreak
{
    readonly int _count;
    readonly TimeSpan _maxGap;
    int _clicks;
    TimeSpan _last;

    public ClickStreak(int count, TimeSpan maxGap)
    {
        _count = count;
        _maxGap = maxGap;
    }

    public bool Register(TimeSpan now)
    {
        _clicks = _clicks > 0 && now - _last <= _maxGap ? _clicks + 1 : 1;
        _last = now;
        if (_clicks < _count) return false;
        _clicks = 0;
        return true;
    }

    public void Reset() => _clicks = 0;
}
