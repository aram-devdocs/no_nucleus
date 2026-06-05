using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Ports
{
    /// <summary>
    /// Converts between the on-screen map and world space. Implemented by the Game layer over DynamicMap.
    /// </summary>
    public interface IMapProjection
    {
        /// <summary>True if the cursor is over the map; outputs the world position under it.</summary>
        bool TryCursorToWorld(out Vec3 world);

        /// <summary>World position to a map-local position (use X,Z) under the map's icon layer.</summary>
        Vec3 WorldToMapLocal(Vec3 world);
    }
}
