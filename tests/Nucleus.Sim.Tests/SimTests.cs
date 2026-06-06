using Nucleus.Sim;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>
    /// Headless end-to-end campaign invariants: step the real CommanderBrain over a seeded battlefield for
    /// many ticks and assert emergent properties. This is the north-star regression net — it proves the brain
    /// runs stably and deterministically and actually engages, without the game.
    /// </summary>
    public class SimTests
    {
        private static SimWorld World(ulong seed)
        {
            var (friendly, enemy) = Scenarios.CombinedArms();
            return new SimWorld(friendly, enemy, seed);
        }

        [Fact]
        public void Same_seed_produces_identical_trace()
        {
            var a = World(12345).Run(300);
            var b = World(12345).Run(300);
            Assert.Equal(a.Fingerprint(), b.Fingerprint());
        }

        [Fact]
        public void Never_produces_NaN_or_infinity()
        {
            var r = World(7).Run(1000);
            Assert.False(r.AnyNaN, "a unit position/hp went non-finite");
        }

        [Fact]
        public void Brain_runs_many_ticks_without_throwing()
        {
            var ex = Record.Exception(() => World(99).Run(2000));
            Assert.Null(ex);
        }

        [Fact]
        public void Brain_detects_enemies_and_generates_objectives()
        {
            var r = World(1).Run(200);
            Assert.True(r.MaxObjectives > 0, "brain never generated an objective from detected enemies");
        }

        [Fact]
        public void Brain_issues_unit_tasks_against_a_real_threat()
        {
            var r = World(2).Run(200);
            Assert.True(r.TasksTotal > 0, "brain issued no unit tasks despite a detected, engageable threat");
        }

        [Fact]
        public void War_progresses_enemy_strength_declines()
        {
            var r = World(2).Run(600);
            Assert.True(r.EnemyEnd < r.EnemyStart,
                $"war did not progress: enemies {r.EnemyStart} -> {r.EnemyEnd} over {r.Ticks} ticks");
        }

        [Fact]
        public void Brain_opens_operations_from_objectives()
        {
            var r = World(3).Run(400);
            Assert.True(r.MaxOperations > 0, "brain generated objectives but never opened an operation");
        }

        [Fact]
        public void Operations_advance_through_combat_phases()
        {
            var r = World(3).Run(600);
            Assert.True(r.MaxPhase > 0, $"operations never advanced past Recon (maxPhase={r.MaxPhase})");
        }

        [Theory]
        [InlineData(1UL)]
        [InlineData(2UL)]
        [InlineData(7UL)]
        [InlineData(42UL)]
        [InlineData(1000UL)]
        [InlineData(8675309UL)]
        public void Fuzz_each_seed_is_deterministic_finite_and_active(ulong seed)
        {
            var a = World(seed).Run(300);
            var b = World(seed).Run(300);
            Assert.Equal(a.Fingerprint(), b.Fingerprint()); // deterministic per seed
            Assert.False(a.AnyNaN, "non-finite state");
            Assert.True(a.TasksTotal > 0, "brain went inactive");
        }
    }
}
