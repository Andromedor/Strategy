using System;
using System.Collections.Generic;
using Strategy.Maps;
using UnityEngine;

namespace Strategy.Data
{
    [Serializable]
    public struct UnitAssetEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private UnitData _asset;

        public string Id => _id;
        public UnitData Asset => _asset;
    }

    [Serializable]
    public struct BuildingAssetEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private BuildingData _asset;

        public string Id => _id;
        public BuildingData Asset => _asset;
    }

    [Serializable]
    public struct ProductionAssetEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private ProductionItemData _asset;

        public string Id => _id;
        public ProductionItemData Asset => _asset;
    }

    [Serializable]
    public struct MapAssetEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private MapDefinition _asset;

        public string Id => _id;
        public MapDefinition Asset => _asset;
    }

    [CreateAssetMenu(fileName = "GameAssetRegistry", menuName = "RTS/Game Asset Registry")]
    public sealed class GameAssetRegistry : ScriptableObject
    {
        [SerializeField] private List<UnitAssetEntry> _units = new();
        [SerializeField] private List<BuildingAssetEntry> _buildings = new();
        [SerializeField] private List<ProductionAssetEntry> _productionItems = new();
        [SerializeField] private List<MapAssetEntry> _maps = new();

        public bool TryGetId(UnitData asset, out string id)
        {
            id = string.Empty;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Asset == asset && !string.IsNullOrWhiteSpace(_units[i].Id))
                {
                    id = _units[i].Id;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetId(BuildingData asset, out string id)
        {
            id = string.Empty;
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].Asset == asset && !string.IsNullOrWhiteSpace(_buildings[i].Id))
                {
                    id = _buildings[i].Id;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetId(ProductionItemData asset, out string id)
        {
            id = string.Empty;
            for (int i = 0; i < _productionItems.Count; i++)
            {
                if (_productionItems[i].Asset == asset && !string.IsNullOrWhiteSpace(_productionItems[i].Id))
                {
                    id = _productionItems[i].Id;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetId(MapDefinition asset, out string id)
        {
            id = string.Empty;
            for (int i = 0; i < _maps.Count; i++)
            {
                if (_maps[i].Asset == asset && !string.IsNullOrWhiteSpace(_maps[i].Id))
                {
                    id = _maps[i].Id;
                    return true;
                }
            }

            return false;
        }

        public UnitData GetUnit(string id)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Id == id)
                    return _units[i].Asset;
            }

            return null;
        }

        public BuildingData GetBuilding(string id)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].Id == id)
                    return _buildings[i].Asset;
            }

            return null;
        }

        public ProductionItemData GetProductionItem(string id)
        {
            for (int i = 0; i < _productionItems.Count; i++)
            {
                if (_productionItems[i].Id == id)
                    return _productionItems[i].Asset;
            }

            return null;
        }

        public MapDefinition GetMap(string id)
        {
            for (int i = 0; i < _maps.Count; i++)
            {
                if (_maps[i].Id == id)
                    return _maps[i].Asset;
            }

            return null;
        }
    }
}
