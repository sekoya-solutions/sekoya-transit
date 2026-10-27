#nullable enable
using System;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// The baked map art (ADR 0032): the road and rail atlas material plus building and vehicle sprites.
    /// Written by the editor bake tool (Transport Tycoon / Art / Bake Map Art); read by the map views.
    /// </summary>
    [CreateAssetMenu(menuName = "Transport Tycoon/Map Art Set", fileName = "MapArtSet")]
    public sealed class MapArtSet : ScriptableObject
    {
        [SerializeField]
        private Material? _tileAtlasMaterial;

        [SerializeField]
        private Sprite? _roadStation;

        [SerializeField]
        private Sprite? _railStation;

        [SerializeField]
        private Sprite[] _houses = Array.Empty<Sprite>();

        [SerializeField]
        private IndustrySprite[] _industries = Array.Empty<IndustrySprite>();

        [SerializeField]
        private Sprite? _genericIndustry;

        [SerializeField]
        private Sprite[] _truck = Array.Empty<Sprite>();

        [SerializeField]
        private Sprite[] _locomotive = Array.Empty<Sprite>();

        [SerializeField]
        private Sprite[] _wagon = Array.Empty<Sprite>();

        public Material? TileAtlasMaterial
        {
            get
            {
                return _tileAtlasMaterial;
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

        public int HouseCount
        {
            get
            {
                return _houses.Length;
            }
        }

        /// <summary>
        /// The house drawn for a town, picked from the fixed list by town id.
        /// </summary>
        public Sprite? GetHouse(int townId)
        {
            if (_houses.Length == 0)
            {
                return null;
            }

            int index = townId % _houses.Length;
            if (index < 0)
            {
                index += _houses.Length;
            }

            return _houses[index];
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

        public Sprite? GetVehicle(MapVehicleArt vehicle, MapHeading heading)
        {
            Sprite[] set;
            switch (vehicle)
            {
                case MapVehicleArt.Locomotive:
                    set = _locomotive;
                    break;
                case MapVehicleArt.Wagon:
                    set = _wagon;
                    break;
                default:
                    set = _truck;
                    break;
            }

            int index = (int)heading;
            return index >= 0 && index < set.Length ? set[index] : null;
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

    public enum MapVehicleArt
    {
        Truck = 0,
        Locomotive = 1,
        Wagon = 2,
    }
}
