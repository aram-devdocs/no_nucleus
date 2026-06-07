using System.Collections.Generic;

namespace Nucleus.Core.Command
{
    /// <summary>The category of a single entry in the battle feed; drives icon/colour in the UI layer.</summary>
    public enum ReportKind
    {
        ObjectiveAdded,
        OperationStarted,
        PhaseChanged,
        SquadFormed,
        SquadDepleted,
        UnitLost,
        EnemyDestroyed,
        ObjectiveComplete,
        ProductionQueued,
        ProductionArrived,
        Blocked
    }

    /// <summary>One immutable battle event. <see cref="OperationId"/> is null for events not tied to an operation.</summary>
    public readonly struct ReportEvent
    {
        public float Time { get; }
        public ReportKind Kind { get; }
        public string Text { get; }
        public string? OperationId { get; }

        public ReportEvent(float time, ReportKind kind, string text, string? operationId = null)
        {
            Time = time;
            Kind = kind;
            Text = text;
            OperationId = operationId;
        }
    }

    /// <summary>Fixed-capacity ring buffer forming the commander's battle feed; oldest entry is overwritten when full.</summary>
    public sealed class BattleLog
    {
        private readonly ReportEvent[] _buffer;
        private int _start;   // oldest entry
        private int _count;

        public BattleLog(int capacity = 100)
        {
            if (capacity < 1) capacity = 1;
            _buffer = new ReportEvent[capacity];
        }

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public void Append(ReportEvent e)
        {
            int end = (_start + _count) % _buffer.Length;
            _buffer[end] = e;

            if (_count == _buffer.Length)
                _start = (_start + 1) % _buffer.Length;   // overwrote the oldest slot — advance past it
            else
                _count++;
        }

        /// <summary>Appends unless the newest entry is identical (same kind + text), so a steady-state bark
        /// (e.g. "defending HQ" every tick) appears once. Use <see cref="Append"/> for one-shot events.</summary>
        public void AppendDistinct(ReportEvent e)
        {
            if (_count > 0)
            {
                int last = (_start + _count - 1) % _buffer.Length;
                if (_buffer[last].Kind == e.Kind && _buffer[last].Text == e.Text) return;
            }
            Append(e);
        }

        /// <summary>Up to <paramref name="n"/> most-recent events, newest first.</summary>
        public IReadOnlyList<ReportEvent> Recent(int n)
        {
            if (n < 0) n = 0;
            int take = n < _count ? n : _count;
            var result = new List<ReportEvent>(take);
            for (int i = 0; i < take; i++)
            {
                int idx = (_start + _count - 1 - i) % _buffer.Length;
                result.Add(_buffer[idx]);
            }
            return result;
        }

        /// <summary>Events for <paramref name="opId"/>, oldest first.</summary>
        public IEnumerable<ReportEvent> ForOperation(string opId)
        {
            for (int i = 0; i < _count; i++)
            {
                int idx = (_start + i) % _buffer.Length;
                if (_buffer[idx].OperationId == opId)
                {
                    yield return _buffer[idx];
                }
            }
        }
    }
}
