using System.Collections.Generic;
using System.Linq;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// A single queued buy: which convoy, what it costs, and which squad/role it reinforces. Pure record of
    /// intent — the runtime drains these into actual game purchases. Unity-free.
    /// </summary>
    public sealed class PurchaseRequest
    {
        public string ConvoyName { get; }
        public float Cost { get; }
        public string ForSquadId { get; }
        public RoleFamily ForRole { get; }
        /// <summary>Real contents for display, e.g. "3× MBT". Empty when unknown.</summary>
        public string Contents { get; }
        /// <summary>True if the player queued this buy; false if the autonomous commander did (source).</summary>
        public bool Manual { get; }

        public PurchaseRequest(string convoyName, float cost, string forSquadId, RoleFamily forRole,
            string contents = "", bool manual = false)
        {
            ConvoyName = convoyName;
            Cost = cost;
            ForSquadId = forSquadId;
            ForRole = forRole;
            Contents = contents ?? "";
            Manual = manual;
        }
    }

    /// <summary>
    /// FIFO queue of pending convoy purchases plus a human-readable build status. Tracks the total committed
    /// (queued) cost so the UI / funds logic can reason about outstanding spend. Pure state.
    /// </summary>
    public sealed class ProductionQueue
    {
        private readonly List<PurchaseRequest> _pending = new List<PurchaseRequest>();

        public IReadOnlyList<PurchaseRequest> Pending => _pending;

        /// <summary>Sum of the cost of every request still in the queue.</summary>
        public float QueuedCost => _pending.Sum(r => r.Cost);

        public void Enqueue(PurchaseRequest r)
        {
            if (r != null) _pending.Add(r);
        }

        /// <summary>Pull the oldest request (FIFO). Returns null when the queue is empty.</summary>
        public PurchaseRequest Dequeue()
        {
            if (_pending.Count == 0) return null;
            var head = _pending[0];
            _pending.RemoveAt(0);
            return head;
        }

        /// <summary>One status line per pending buy: source (you/AI), name, real contents, cost, and target
        /// squad if any — e.g. "BUILD(you) Armor column [3× MBT] · 1200 → Bravo".</summary>
        public IReadOnlyList<string> Describe() =>
            _pending.Select(r =>
            {
                string src = r.Manual ? "you" : "AI";
                string contents = string.IsNullOrEmpty(r.Contents) ? "" : $" [{r.Contents}]";
                string squad = string.IsNullOrEmpty(r.ForSquadId) ? "" : $" → {r.ForSquadId}";
                return $"BUILD({src}) {r.ConvoyName}{contents} · {r.Cost:0}{squad}";
            }).ToList();
    }
}
