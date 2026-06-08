using Nucleus.Core.Command;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>The pure-Domain wording SSOT — the brain feed (Campaign) and the UI (ObjectiveVisuals) both
    /// read through this, so they can't drift to "DestroyTarget — Sead" vs "Destroy target · SEAD".</summary>
    public class ObjectiveTextTests
    {
        [Theory]
        [InlineData(ObjectiveKind.CapturePoint, "Capture point", "CAP")]
        [InlineData(ObjectiveKind.DestroyTarget, "Destroy target", "DESTROY")]
        [InlineData(ObjectiveKind.DefendArea, "Defend area", "DEFEND")]
        [InlineData(ObjectiveKind.ControlAirspace, "Control airspace", "AIR")]
        [InlineData(ObjectiveKind.Resupply, "Resupply", "SUPPLY")]
        [InlineData(ObjectiveKind.Recon, "Recon", "RECON")]
        [InlineData(ObjectiveKind.SuppressAirDefense, "Suppress air defense", "SEAD")]
        [InlineData(ObjectiveKind.NavalStrike, "Naval strike", "NAVAL")]
        public void Name_and_tag_are_readable(ObjectiveKind kind, string name, string tag)
        {
            Assert.Equal(name, ObjectiveText.Name(kind));
            Assert.Equal(tag, ObjectiveText.Tag(kind));
        }

        [Theory]
        [InlineData(CombatPhase.Recon, "Scouting")]
        [InlineData(CombatPhase.AirSuperiority, "Air superiority")]
        [InlineData(CombatPhase.Sead, "SEAD")]
        [InlineData(CombatPhase.Strike, "Strike")]
        [InlineData(CombatPhase.Assault, "Assault")]
        [InlineData(CombatPhase.Capture, "Capturing")]
        [InlineData(CombatPhase.Hold, "Holding")]
        public void Phase_labels_are_readable(CombatPhase phase, string label)
        {
            Assert.Equal(label, ObjectiveText.PhaseLabel(phase));
        }
    }
}
