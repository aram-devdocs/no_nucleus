using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;

namespace CommanderLayer.Game
{
    /// <summary>
    /// IMapProjection over DynamicMap. Cursor→world uses the map's own TryGetCursorCoordinates; world→map
    /// reproduces the engine's icon placement (worldPos.AsVector3() * mapDisplayFactor) on the (x,z) plane.
    /// </summary>
    public sealed class DynamicMapProjection : IMapProjection
    {
        public bool TryCursorToWorld(out Vec3 world)
        {
            world = default;
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || !DynamicMap.mapMaximized)
            {
                return false;
            }
            if (map.TryGetCursorCoordinates(out var gp))
            {
                world = GameConvert.ToVec3(gp);
                return true;
            }
            return false;
        }

        public Vec3 WorldToMapLocal(Vec3 world)
        {
            var map = SceneSingleton<DynamicMap>.i;
            float factor = map != null ? map.mapDisplayFactor : 0f;
            var v = GameConvert.ToGlobal(world).AsVector3() * factor;
            // Map icons live on a 2D plane: local (x, z) -> RectTransform (x, y), z = 0.
            return new Vec3(v.x, v.z, 0f);
        }

        public float MapScale
        {
            get
            {
                var map = SceneSingleton<DynamicMap>.i;
                return map != null ? map.mapDisplayFactor : 0f;
            }
        }
    }
}
