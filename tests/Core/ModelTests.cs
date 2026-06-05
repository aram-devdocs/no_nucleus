using CommanderLayer.Core.Model;
using Xunit;

namespace CommanderLayer.Tests
{
    public class ModelTests
    {
        [Fact]
        public void HorizontalDistance_IgnoresAltitude()
        {
            var a = new Vec3(0, 0, 0);
            var b = new Vec3(3, 9999, 4); // 3-4-5 triangle on the x,z plane; large y ignored
            Assert.Equal(5f, a.HorizontalDistanceTo(b), 3);
        }

        [Fact]
        public void DistanceTo_IsFull3D()
        {
            var a = new Vec3(0, 0, 0);
            var b = new Vec3(2, 3, 6); // 2-3-6 -> 7
            Assert.Equal(7f, a.DistanceTo(b), 3);
        }

        [Fact]
        public void ObjectiveModel_DefaultsLabelToKind()
        {
            var o = new ObjectiveModel("id", ObjectiveKind.MoveAttack, new Vec3(1, 2, 3));
            Assert.Equal("MoveAttack", o.Label);
        }

        [Fact]
        public void AssignmentSnapshot_EmptyHasNoUnits()
        {
            Assert.Equal(0, AssignmentSnapshot.Empty.Total);
            Assert.Equal(0, AssignmentSnapshot.Empty.CommandableCount);
        }
    }
}
