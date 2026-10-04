namespace Flow.Audio.Graph;

/// <summary>Linear ADSR with edge-triggered gate and retrigger from current level.
/// Zero-time stages are skipped; nonzero stages reach their target on frame N.</summary>
internal struct GateEnvelope
{
    private enum Stage { Idle, Attack, Decay, Sustain, Release }
    private Stage _stage;
    private bool _gate;
    private double _level, _start, _target;
    private int _elapsed, _length;
    internal double Read(bool gate, int attack, int decay, double sustain, int release)
    {
        if (gate != _gate)
        {
            if (gate) Begin(Stage.Attack, 1, attack);
            else Begin(Stage.Release, 0, release);
            _gate = gate;
        }
        // At most attack + decay can collapse before reaching sustain, or release
        // before idle. No unbounded loop even when every duration is zero.
        for (int transition = 0; transition < 3; transition++)
        {
            if (_stage == Stage.Idle) return _level = 0;
            if (_stage == Stage.Sustain) return _level = sustain;
            if (_length != 0) break;
            _level = _target; Advance(decay, sustain);
        }
        if (_stage == Stage.Idle) return _level = 0;
        if (_stage == Stage.Sustain) return _level = sustain;
        _elapsed++;
        _level = _elapsed >= _length ? _target : _start + (_target - _start) * _elapsed / _length;
        if (_elapsed >= _length) Advance(decay, sustain);
        return _level;
    }
    private void Begin(Stage stage, double target, int length)
    { _stage = stage; _start = _level; _target = target; _length = length; _elapsed = 0; }
    private void Advance(int decay, double sustain)
    {
        if (_stage == Stage.Attack) Begin(Stage.Decay, sustain, decay);
        else if (_stage == Stage.Decay) _stage = Stage.Sustain;
        else _stage = Stage.Idle;
    }
}
