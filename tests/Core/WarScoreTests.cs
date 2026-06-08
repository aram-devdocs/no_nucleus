using System;
using Nucleus.Core.War;
using Xunit;

namespace Nucleus.Core.Tests
{
    /// <summary>
    /// The attrition win-condition: losing units/bases and spending on reinforcement all drop a faction's
    /// score, the spend penalty grows EXPONENTIALLY as bases are lost (a based-wiped side that buys carrier
    /// groups bleeds far faster), and a faction is defeated at zero. Pure, headless.
    /// </summary>
    public class WarScoreTests
    {
        [Fact]
        public void Negative_ctor_tuning_throws_with_the_offending_parameter()
        {
            // Five same-typed floats — a transposed/negative arg must fail loudly.
            Assert.Throws<ArgumentOutOfRangeException>(() => new WarScore(start: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WarScore(unitValue: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WarScore(baseValue: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WarScore(spendPenaltyBase: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WarScore(falloffPerBaseLost: -1f));
            var ok = Record.Exception(() => new WarScore()); // defaults are valid
            Assert.Null(ok);
        }

        [Fact]
        public void Unit_and_base_losses_reduce_score()
        {
            var s = new WarScore(start: 1000f, unitValue: 8f, baseValue: 120f);
            s.UnitLost(3);   // -24
            s.BaseLost();    // -120
            Assert.Equal(1000f - 24f - 120f, s.Score, 3);
            Assert.Equal(3, s.UnitsLost);
            Assert.Equal(1, s.BasesLost);
        }

        [Fact]
        public void Spend_penalty_grows_exponentially_as_bases_are_lost()
        {
            var s = new WarScore(spendPenaltyBase: 0.02f, falloffPerBaseLost: 0.6f);
            float full = s.SpendPenalty;            // 0 bases lost
            s.BaseLost();
            float oneLost = s.SpendPenalty;
            s.BaseLost();
            float twoLost = s.SpendPenalty;
            // each base lost multiplies the penalty by e^0.6 (~1.82)
            Assert.True(oneLost > full * 1.5f);
            Assert.True(twoLost > oneLost * 1.5f);
        }

        [Fact]
        public void Based_wiped_side_bleeds_faster_per_dollar_than_an_intact_side()
        {
            var intact = new WarScore(start: 1000f);
            var wiped = new WarScore(start: 1000f);
            for (int i = 0; i < 4; i++) wiped.BaseLost();   // lose 4 bases (the base-loss hit aside)

            float intactBefore = intact.Score, wipedBefore = wiped.Score;
            intact.Spend(1000f);
            wiped.Spend(1000f);

            float intactDrop = intactBefore - intact.Score;
            float wipedDrop = wipedBefore - wiped.Score;
            Assert.True(wipedDrop > intactDrop * 5f, $"wiped {wipedDrop} should bleed far more than intact {intactDrop} per $");
        }

        [Fact]
        public void Score_floors_at_zero_and_marks_defeated()
        {
            var s = new WarScore(start: 50f, baseValue: 120f);
            s.BaseLost();
            Assert.Equal(0f, s.Score);
            Assert.True(s.Defeated);
        }

        [Fact]
        public void WarState_declares_a_winner_when_one_side_is_attrited_out()
        {
            var war = new WarState(new WarSide("A"), new WarSide("B"));
            Assert.False(war.IsOver);
            Assert.Null(war.Winner);

            for (int i = 0; i < 20; i++) war.Opfor.Score.BaseLost(); // attrit B out
            Assert.True(war.IsOver);
            Assert.Same(war.Blufor, war.Winner);
        }

        [Fact]
        public void TrySpend_debits_funds_and_attrition_and_refuses_when_unaffordable()
        {
            var side = new WarSide("A", CommanderKind.Human, startFunds: 100f);
            Assert.False(side.TrySpend(200f));               // unaffordable
            Assert.Equal(100f, side.Funds);

            float before = side.Score.Score;
            Assert.True(side.TrySpend(100f));
            Assert.Equal(0f, side.Funds);
            Assert.True(side.Score.Score < before);          // spending costs attrition too
        }
    }
}
