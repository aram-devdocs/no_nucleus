namespace CommanderLayer.Core.Ports
{
    /// <summary>Time source, abstracted so the controller's throttling is testable. Seconds since start.</summary>
    public interface IClock
    {
        float Now { get; }
    }
}
