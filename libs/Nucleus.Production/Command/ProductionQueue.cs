using System.Collections.Generic;
using System.Linq;

namespace Nucleus.Core.Command
{
    /// <summary>A single queued buy: which convoy, cost, and the squad/role it reinforces. The runtime drains
    /// these into actual game purchases.</summary>
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

    /// <summary>A queued buy with its live delivery timing: where it is in line, how far along (0..1), and the
    /// seconds until it arrives — so the UI can show a progress bar + countdown + map arrival marker.</summary>
    public readonly struct QueueItemView
    {
        public readonly string Name;
        public readonly string Contents;
        public readonly float Cost;
        public readonly string ForSquadId;
        public readonly bool Manual;
        public readonly float Progress01;     // 0..1 build progress of THIS item
        public readonly float EtaSeconds;      // seconds until it delivers

        public QueueItemView(string name, string contents, float cost, string forSquadId, bool manual,
            float progress01, float etaSeconds)
        {
            Name = name; Contents = contents; Cost = cost; ForSquadId = forSquadId; Manual = manual;
            Progress01 = progress01; EtaSeconds = etaSeconds;
        }
    }

    /// <summary>FIFO queue of pending convoy purchases. Tracks total queued cost for the funds/UI logic.</summary>
    public sealed class ProductionQueue
    {
        private readonly List<PurchaseRequest> _pending = new List<PurchaseRequest>();

        public IReadOnlyList<PurchaseRequest> Pending => _pending;
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

        /// <summary>Live delivery view of the queue under a one-at-a-time cooldown drain: the head builds from the
        /// last purchase and delivers a cooldown later; each subsequent item starts when the one ahead delivers.
        /// Pure function of (now, lastPurchase, cooldown) so the UI's progress bar + countdown + arrival markers
        /// are deterministic. <paramref name="cooldownSeconds"/> &lt;= 0 means instant (full progress, zero ETA).</summary>
        public IReadOnlyList<QueueItemView> Snapshot(float nowSeconds, float lastPurchaseSeconds, float cooldownSeconds)
        {
            var views = new List<QueueItemView>(_pending.Count);
            for (int i = 0; i < _pending.Count; i++)
            {
                var r = _pending[i];
                float progress, eta;
                if (cooldownSeconds <= 0f) { progress = 1f; eta = 0f; }
                else
                {
                    float start = lastPurchaseSeconds + cooldownSeconds * i;   // this item starts when the one ahead delivers
                    float deliver = start + cooldownSeconds;
                    progress = (nowSeconds - start) / cooldownSeconds;
                    progress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
                    eta = deliver - nowSeconds;
                    if (eta < 0f) eta = 0f;
                }
                views.Add(new QueueItemView(r.ConvoyName, r.Contents, r.Cost, r.ForSquadId, r.Manual, progress, eta));
            }
            return views;
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
