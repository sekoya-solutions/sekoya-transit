#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// The baked map art (ADR 0032): the road and rail atlas material plus building and vehicle sprites. Cargo icon
    /// sprites for map labels are baked from the UI cargo SVGs (Issue #193).
    /// Written by the editor bake tool (Sekoya Transit / Art / Bake Map Art); read by the map views.
    /// </summary>
    [CreateAssetMenu(menuName = "Sekoya Transit/Map Art Set", fileName = "MapArtSet")]
    public sealed class MapArtSet : ScriptableObject
    {
        private readonly System.Collections.Generic.Dictionary<Sprite, float> _topHeights =
            new System.Collections.Generic.Dictionary<Sprite, float>();

        [SerializeField]
        private Material? _tileAtlasMaterial;

        [SerializeField]
        private Texture2D? _groundAtlas;

        [SerializeField]
        private Sprite? _roadStation;

        [SerializeField]
        private Sprite? _railStation;

        [SerializeField]
        private Sprite[] _townTiers = Array.Empty<Sprite>();

        [SerializeField]
        private IndustrySprite[] _industries = Array.Empty<IndustrySprite>();

        [SerializeField]
        private Sprite? _genericIndustry;

        [SerializeField]
        private VehicleTypeSprites[] _truckTypes = Array.Empty<VehicleTypeSprites>();

        [SerializeField]
        private VehicleTypeSprites[] _locomotiveTypes = Array.Empty<VehicleTypeSprites>();

        // Keyed by the locomotive type id: a wagon takes the tier of the locomotive that pulls it.
        [SerializeField]
        private VehicleTypeSprites[] _wagonTypes = Array.Empty<VehicleTypeSprites>();

        [SerializeField]
        private CargoIconSprite[] _cargoIcons = Array.Empty<CargoIconSprite>();

        [SerializeField]
        private Sprite? _genericCargoIcon;

        [SerializeField]
        private Sprite? _cashIcon;

        public Material? TileAtlasMaterial
        {
            get
            {
                return _tileAtlasMaterial;
            }
        }

        /// <summary>The ground atlas of terrain variants and soft borders (Issue #272); null draws flat colors.</summary>
        public Texture2D? GroundAtlas
        {
            get
            {
                return _groundAtlas;
            }
        }

        public Sprite? RoadStation
        {
            get
            {
                return _roadStation;
            }
        }

        public Sprite? RailStation
        {
            get
            {
                return _railStation;
            }
        }

        public int TownTierCount
        {
            get
            {
                return _townTiers.Length;
            }
        }

        /// <summary>
        /// The town sprite of a growth tier (1 = the first tier, Issue #292); a tier above the last one draws the last sprite.
        /// </summary>
        public Sprite? GetTownTier(int tier)
        {
            if (_townTiers.Length == 0)
            {
                return null;
            }

            return _townTiers[Math.Clamp(tier - 1, 0, _townTiers.Length - 1)];
        }

        /// <summary>
        /// The growth tier (1 = the first tier) of a town of the given population, from the tiers' minimum populations;
        /// 1 when the list is missing.
        /// </summary>
        public static int TownTierForPopulation(int population, IReadOnlyList<int>? minPopulations)
        {
            int tier = 1;
            if (minPopulations != null)
            {
                for (int index = 1; index < minPopulations.Count && population >= minPopulations[index]; index++)
                {
                    tier = index + 1;
                }
            }

            return tier;
        }

        /// <summary>
        /// The sprite of an industry id, or the generic industry sprite when the id has no art of its own.
        /// </summary>
        public Sprite? GetIndustry(string industryId)
        {
            for (int index = 0; index < _industries.Length; index++)
            {
                IndustrySprite entry = _industries[index];
                if (string.Equals(entry.IndustryId, industryId, StringComparison.Ordinal) && entry.Sprite != null)
                {
                    return entry.Sprite;
                }
            }

            return _genericIndustry;
        }

        /// <summary>
        /// The height of the sprite's opaque top above its pivot, in world units: Sprite.bounds spans the whole canvas, so
        /// this reads the tight mesh instead. Cached per sprite, as reading the vertices allocates.
        /// </summary>
        public float GetTopHeight(Sprite sprite)
        {
            if (!_topHeights.TryGetValue(sprite, out float top))
            {
                top = float.MinValue;
                foreach (Vector2 vertex in sprite.vertices)
                {
                    top = Mathf.Max(top, vertex.y);
                }

                _topHeights[sprite] = top;
            }

            return top;
        }

        /// <summary>
        /// The map label icon of a cargo id, or the generic cargo icon when the id has no icon of its own.
        /// </summary>
        public Sprite? GetCargoIcon(string cargoId)
        {
            for (int index = 0; index < _cargoIcons.Length; index++)
            {
                CargoIconSprite entry = _cargoIcons[index];
                if (string.Equals(entry.CargoId, cargoId, StringComparison.Ordinal) && entry.Sprite != null)
                {
                    return entry.Sprite;
                }
            }

            return _genericCargoIcon;
        }

        /// <summary>
        /// The cash icon of the map cash popups (Issue #284): the dollar icon used by the UI.
        /// </summary>
        public Sprite? CashIcon
        {
            get
            {
                return _cashIcon;
            }
        }

        /// <summary>
        /// The sprite of a vehicle for a heading (Issue #288). <paramref name="typeId"/> is the vehicle type id (a wagon
        /// passes the id of its locomotive); a null or unknown id falls back to the first tier (MK1).
        /// </summary>
        public Sprite? GetVehicle(MapVehicleArt vehicle, string? typeId, MapHeading heading)
        {
            VehicleTypeSprites[] table;
            switch (vehicle)
            {
                case MapVehicleArt.Locomotive:
                    table = _locomotiveTypes;
                    break;
                case MapVehicleArt.Wagon:
                    table = _wagonTypes;
                    break;
                default:
                    table = _truckTypes;
                    break;
            }

            int index = (int)heading;
            Sprite? fallback = null;
            for (int entryIndex = 0; entryIndex < table.Length; entryIndex++)
            {
                Sprite[] sprites = table[entryIndex].Sprites;
                Sprite? sprite = index >= 0 && index < sprites.Length ? sprites[index] : null;
                if (sprite == null)
                {
                    continue;
                }

                if (typeId != null && string.Equals(table[entryIndex].TypeId, typeId, StringComparison.Ordinal))
                {
                    return sprite;
                }

                fallback ??= sprite;
            }

            return fallback;
        }

        /// <summary>
        /// The four heading sprites (indexed by <see cref="MapHeading"/>) of one vehicle tier.
        /// </summary>
        [Serializable]
        public sealed class VehicleTypeSprites
        {
            [SerializeField]
            private string _typeId = string.Empty;

            [SerializeField]
            private Sprite[] _sprites = Array.Empty<Sprite>();

            public string TypeId
            {
                get
                {
                    return _typeId;
                }
            }

            public Sprite[] Sprites
            {
                get
                {
                    return _sprites;
                }
            }
        }

        [Serializable]
        public sealed class IndustrySprite
        {
            [SerializeField]
            private string _industryId = string.Empty;

            [SerializeField]
            private Sprite? _sprite;

            public string IndustryId
            {
                get
                {
                    return _industryId;
                }
            }

            public Sprite? Sprite
            {
                get
                {
                    return _sprite;
                }
            }
        }
    }

    [Serializable]
    public sealed class CargoIconSprite
    {
        [SerializeField]
        private string _cargoId = string.Empty;

        [SerializeField]
        private Sprite? _sprite;

        public string CargoId
        {
            get
            {
                return _cargoId;
            }
        }

        public Sprite? Sprite
        {
            get
            {
                return _sprite;
            }
        }
    }

    public enum MapVehicleArt
    {
        Truck = 0,
        Locomotive = 1,
        Wagon = 2,
    }
}
