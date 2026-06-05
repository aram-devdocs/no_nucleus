namespace CommanderLayer.Core.Model
{
    /// <summary>
    /// Unity-free color (components in 0..1). Keeps Core free of UnityEngine.Color; the UI layer
    /// converts this to a UnityEngine.Color at the boundary.
    /// </summary>
    public readonly struct ColorRgba
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public ColorRgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ColorRgba White => new ColorRgba(1f, 1f, 1f);
    }
}
