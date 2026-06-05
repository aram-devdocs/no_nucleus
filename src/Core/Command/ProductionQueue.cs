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

        public PurchaseRequest(string convoyName, float cost, string forSquadId, RoleFamily forRole)
        {
            ConvoyName = convoyName;
            Cost = cost;
            ForSquadId = forSquadId;
            ForRole = forRole;
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

        /// <summary>One status line per pending buy, e.g. "BUILDING · Armor convoy -> Squad Bravo · 1200".</summary>
        public IReadOnlyList<string> Describe() =>
            _pending.Select(r => $"BUILDING · {r.ConvoyName} -> Squad {r.ForSquadId} · {r.Cost:0}").ToList();
    }
}
