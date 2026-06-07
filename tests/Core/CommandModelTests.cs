using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Tests
{
    public class CommandModelTests
    {
        private static Vec3 P => new Vec3(0, 0, 0);
        private static Objective Obj(ObjectiveKind k) => new Objective("o", k, P, ObjectiveSource.Auto);

        [Fact]
        public void Operation_defaults_to_auto_and_planning()
        {
            var op = new Operation("op", Obj(ObjectiveKind.DestroyTarget), new[] { "sq1" });
            Assert.Equal(AutonomyLevel.Auto, op.Autonomy);
            Assert.Equal(OperationStatus.Planning, op.Status);
            Assert.Contains("sq1", op.SquadIds);
            Assert.False(op.IsTerminal);
            op.Status = OperationStatus.Complete;
            Assert.True(op.IsTerminal);
        }
    }
}
