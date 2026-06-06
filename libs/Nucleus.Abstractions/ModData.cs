using System;
using UnityEngine;

namespace CommanderLayer.Abstractions
{
    /// <summary>Identity + presentation metadata for a mod (stable Id is the config/registry/Thunderstore key).</summary>
    public sealed class ModInfo
    {
        public string Id;
        public string DisplayName;
        public string Version;
        public string Author;
        public string Description;
        public Sprite Icon;
    }

    /// <summary>A map-bezel button a mod registers; the host attaches it to a blank VirtualMFD slot and wires
    /// the click. <see cref="LabelColor"/> is polled so the label can reflect live state (e.g. green when open).</summary>
    public sealed class MapButtonSpec
    {
        public string ModId;
        public string Label;
        public Action OnClick;
        public Func<Color> LabelColor;
    }

    /// <summary>A row a mod contributes to the main-menu loader.</summary>
    public sealed class MenuItemSpec
    {
        public string ModId;
        public string Label;
        public Action OnClick;
    }
}
