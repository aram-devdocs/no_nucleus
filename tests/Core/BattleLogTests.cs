using System.Linq;
using CommanderLayer.Core.Command;
using Xunit;

namespace CommanderLayer.Tests
{
    public class BattleLogTests
    {
        private static ReportEvent Ev(float time, string text, string opId = null) =>
            new ReportEvent(time, ReportKind.PhaseChanged, text, opId);

        [Fact]
        public void Recent_ReturnsMostRecentFirst()
        {
            var log = new BattleLog();
            log.Append(Ev(1f, "a"));
            log.Append(Ev(2f, "b"));
            log.Append(Ev(3f, "c"));

            var recent = log.Recent(10);

            Assert.Equal(3, log.Count);
            Assert.Equal(new[] { "c", "b", "a" }, recent.Select(e => e.Text).ToArray());
        }

        [Fact]
        public void Append_OverflowDropsOldest()
        {
            var log = new BattleLog(capacity: 3);
            log.Append(Ev(1f, "a"));
            log.Append(Ev(2f, "b"));
            log.Append(Ev(3f, "c"));
            log.Append(Ev(4f, "d")); // pushes out "a"
            log.Append(Ev(5f, "e")); // pushes out "b"

            Assert.Equal(3, log.Count);
            var recent = log.Recent(10);
            Assert.Equal(new[] { "e", "d", "c" }, recent.Select(e => e.Text).ToArray());
        }

        [Fact]
        public void ForOperation_FiltersById_ChronologicalOrder()
        {
            var log = new BattleLog();
            log.Append(Ev(1f, "op1-first", "op1"));
            log.Append(Ev(2f, "other", "op2"));
            log.Append(Ev(3f, "no-op"));
            log.Append(Ev(4f, "op1-second", "op1"));

            var op1 = log.ForOperation("op1").ToArray();

            Assert.Equal(new[] { "op1-first", "op1-second" }, op1.Select(e => e.Text).ToArray());
        }

        [Fact]
        public void ForOperation_NullMatchesNullOperationId()
        {
            var log = new BattleLog();
            log.Append(Ev(1f, "tagged", "op1"));
            log.Append(Ev(2f, "untagged"));

            var untagged = log.ForOperation(null).ToArray();

            Assert.Single(untagged);
            Assert.Equal("untagged", untagged[0].Text);
        }

        [Fact]
        public void Recent_CapsAtRequestedN()
        {
            var log = new BattleLog();
            log.Append(Ev(1f, "a"));
            log.Append(Ev(2f, "b"));
            log.Append(Ev(3f, "c"));

            var recent = log.Recent(2);

            Assert.Equal(2, recent.Count);
            Assert.Equal(new[] { "c", "b" }, recent.Select(e => e.Text).ToArray());
        }

        [Fact]
        public void Recent_CapsAtCount()
        {
            var log = new BattleLog();
            log.Append(Ev(1f, "a"));

            var recent = log.Recent(50);

            Assert.Single(recent);
            Assert.Equal("a", recent[0].Text);
        }

        [Fact]
        public void Recent_OnEmptyLogIsEmpty()
        {
            var log = new BattleLog();
            Assert.Empty(log.Recent(5));
            Assert.Equal(0, log.Count);
        }

        [Fact]
        public void Ctor_StoresFieldsOnEvent()
        {
            var e = new ReportEvent(7.5f, ReportKind.EnemyDestroyed, "boom", "opX");
            Assert.Equal(7.5f, e.Time);
            Assert.Equal(ReportKind.EnemyDestroyed, e.Kind);
            Assert.Equal("boom", e.Text);
            Assert.Equal("opX", e.OperationId);
        }
    }
}
