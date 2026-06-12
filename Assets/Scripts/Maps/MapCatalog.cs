using System;
using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Maps
{
    [CreateAssetMenu(fileName = "MapCatalog", menuName = "RTS/Maps/Map Catalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        [SerializeField] private List<MapDefinition> _maps = new();

        public IReadOnlyList<MapDefinition> Maps => _maps;

        public void GetMapsForMode(SkirmishTeamMode mode, List<MapDefinition> results)
        {
            if (results == null)
                return;

            results.Clear();

            if (_maps == null)
                return;

            for (int i = 0; i < _maps.Count; i++)
            {
                MapDefinition map = _maps[i];
                if (map != null && map.SupportsMode(mode))
                    results.Add(map);
            }

            results.Sort((first, second) =>
            {
                int playerCompare = first.MaxPlayers.CompareTo(second.MaxPlayers);
                return playerCompare != 0
                    ? playerCompare
                    : string.Compare(first.DisplayName, second.DisplayName, StringComparison.Ordinal);
            });
        }

        public MapDefinition FindById(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || _maps == null)
                return null;

            for (int i = 0; i < _maps.Count; i++)
            {
                MapDefinition map = _maps[i];
                if (map != null && map.MapId == mapId)
                    return map;
            }

            return null;
        }
    }
}
