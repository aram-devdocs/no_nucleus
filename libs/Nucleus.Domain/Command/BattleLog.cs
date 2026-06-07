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

    /// <summary>
    /// An immutable, Unity-free record of one thing that happened in the battle. <see cref="OperationId"/>
    /// is optional (null) for events not tied to a specific operation.
    /// </summary>
    public readonly struct ReportEvent
    {
        public float Time { get; }
        public ReportKind Kind { get; }
        public string Text { get; }
        public string OperationId { get; }

        public ReportEvent(float time, ReportKind kind, string text, string operationId = null)
        {
            Time = time;
            Kind = kind;
            Text = text;
            OperationId = operationId;
        }
    }

    /// <summary>
    /// A fixed-capacity ring buffer of <see cref="ReportEvent"/>s forming the commander's battle feed. When
    /// full, the oldest entry is overwritten. Pure Core: no Unity, no allocation per <see cref="Append"/>
    /// beyond the event being stored.
    /// </summary>
    public sealed class BattleLog
    {
        private readonly ReportEvent[] _buffer;
        private int _start;   // index of the oldest entry
        private int _count;   // number of live entries

        public BattleLog(int capacity = 100)
        {
            if (capacity < 1) capacity = 1;
            _buffer = new ReportEvent[capacity];
        }

        /// <summary>Maximum number of entries retained before the oldest is dropped.</summary>
        public int Capacity => _buffer.Length;

        /// <summary>Number of entries currently held (0..Capacity).</summary>
        public int Count => _count;

        /// <summary>Adds an event. When the buffer is full the oldest entry is overwritten.</summary>
        public void Append(ReportEvent e)
        {
            int end = (_start + _count) % _buffer.Length;
            _buffer[end] = e;

            if (_count == _buffer.Length)
            {
                // Full: the write landed on the oldest slot, so advance start to drop it.
                _start = (_start + 1) % _buffer.Length;
            }
            else
            {
                _count++;
            }
        }

        /// <summary>Adds an event UNLESS the newest entry is identical (same kind + text) — so a steady-state
        /// condition (e.g. "defending HQ" every tick) appears once, not as feed spam. Use for recurring barks;
        /// use <see cref="Append"/> for genuine one-shot events.</summary>
        public void AppendDistinct(ReportEvent e)
        {
            if (_count > 0)
            {
                int last = (_start + _count - 1) % _buffer.Length;
                if (_buffer[last].Kind == e.Kind && _buffer[last].Text == e.Text) return;
            }
            Append(e);
        }

        /// <summary>
        /// Returns up to <paramref name="n"/> most-recent events, newest first. Capped at both
        /// <paramref name="n"/> and the current <see cref="Count"/>.
        /// </summary>
        public IReadOnlyList<ReportEvent> Recent(int n)
        {
            if (n < 0) n = 0;
            int take = n < _count ? n : _count;
            var result = new List<ReportEvent>(take);
            for (int i = 0; i < take; i++)
            {
                // Walk backwards from the newest entry.
                int idx = (_start + _count - 1 - i) % _buffer.Length;
                result.Add(_buffer[idx]);
            }
            return result;
        }

        /// <summary>Yields the events belonging to <paramref name="opId"/> in chronological (oldest-first) order.</summary>
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
