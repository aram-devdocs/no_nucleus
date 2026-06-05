namespace CommanderLayer.Core.Model
{
    /// <summary>Minimal local-faction descriptor used to label and theme the UI.</summary>
    public sealed class FactionInfo
    {
        public string Name { get; }
        public ColorRgba Color { get; }

        public FactionInfo(string name, ColorRgba color)
        {
            Name = name;
            Color = color;
        }
    }
}
