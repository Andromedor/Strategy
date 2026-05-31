using System.Collections.Generic;
using Strategy.Core;
using Strategy.Buildings;
using Strategy.Units;
using UnityEngine;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Trigger-collider wrapper around an Outpost that tracks which team's units (and blocking buildings)
    /// are inside the capture zone. Feeds occupancy counts into Outpost.TickCapture every frame.
    /// </summary>
    public class OutpostCaptureZone : MonoBehaviour
    {
        private const float DefaultPlaneSize = 10f;

        [SerializeField] private Outpost _outpost;

        [Header("Editable Zone")]
        [SerializeField] private BoxCollider _captureCollider;
        [SerializeField] private Transform _captureZoneVisual;
        [SerializeField] private Vector3 _zoneSize = new Vector3(24f, 5f, 24f);
        [SerializeField] private Vector3 _zoneCenter = new Vector3(0f, 2.5f, -12f);
        [SerializeField] private bool _syncVisualWithZone = true;

        private readonly Dictionary<TeamComponent, int> _unitsInside = new();
        private readonly Dictionary<GameObject, int> _blockingBuildingsInside = new();
        private readonly List<TeamComponent> _staleUnits = new();
        private readonly List<GameObject> _staleBuildings = new();

        private int _playerUnitLayer = -1;
        private int _enemyUnitLayer = -1;
        private int _buildingLayer = -1;

        private void Awake()
        {
            ApplyZoneSettings();
            _playerUnitLayer = LayerMask.NameToLayer("PlayerUnit");
            _enemyUnitLayer = LayerMask.NameToLayer("EnemyUnit");
            _buildingLayer = LayerMask.NameToLayer("Building");
        }

        private void Reset()
        {
            ResolveZoneReferences();
            ApplyZoneSettings();
        }

        private void OnValidate()
        {
            ApplyZoneSettings();
        }

        private void Update()
        {
            CleanupDestroyedEntries();

            int playerUnits = 0;
            int enemyUnits = 0;

            foreach (TeamComponent unit in _unitsInside.Keys)
            {
                if (unit == null)
                    continue;

                if (unit.Team == TeamType.Player)
                    playerUnits++;

                if (unit.Team == TeamType.Enemy)
                    enemyUnits++;
            }

            if (_outpost != null)
            {
                _outpost.TickCapture(
                    playerUnits,
                    enemyUnits,
                    _blockingBuildingsInside.Count > 0,
                    Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (TryGetUnit(other, out TeamComponent unit))
                AddTouch(_unitsInside, unit);

            GameObject blockingBuilding = GetBlockingBuilding(other);

            if (blockingBuilding != null)
                AddTouch(_blockingBuildingsInside, blockingBuilding);
        }

        private void OnTriggerExit(Collider other)
        {
            if (TryGetUnit(other, out TeamComponent unit))
                RemoveTouch(_unitsInside, unit);

            GameObject blockingBuilding = GetBlockingBuilding(other);

            if (blockingBuilding != null)
                RemoveTouch(_blockingBuildingsInside, blockingBuilding);
        }

        /// <summary>Extracts the TeamComponent from a collider if it belongs to a unit layer (PlayerUnit or EnemyUnit).</summary>
        private bool TryGetUnit(Collider other, out TeamComponent unit)
        {
            unit = other.GetComponentInParent<TeamComponent>();

            if (unit == null)
                return false;

            return IsUnitLayer(unit.gameObject.layer) || IsUnitLayer(other.gameObject.layer);
        }

        /// <summary>
        /// Returns the root GameObject of a building that blocks capture (BuildingProduction,
        /// ConstructionCenter, or any object on the Building layer), or null if the collider is not a blocker.
        /// </summary>
        private GameObject GetBlockingBuilding(Collider other)
        {
            if (_outpost != null && other.GetComponentInParent<Outpost>() == _outpost)
                return null;

            BuildingProduction production = other.GetComponentInParent<BuildingProduction>();

            if (production != null)
                return production.gameObject;

            ConstructionCenter constructionCenter = other.GetComponentInParent<ConstructionCenter>();

            if (constructionCenter != null)
                return constructionCenter.gameObject;

            return GetParentOnLayer(other.transform, _buildingLayer);
        }

        private bool IsUnitLayer(int layer)
        {
            return layer == _playerUnitLayer || layer == _enemyUnitLayer;
        }

        /// <summary>Walks up the transform hierarchy and returns the first GameObject whose layer matches, or null.</summary>
        private static GameObject GetParentOnLayer(Transform start, int layer)
        {
            if (layer < 0)
                return null;

            Transform current = start;

            while (current != null)
            {
                if (current.gameObject.layer == layer)
                    return current.gameObject;

                current = current.parent;
            }

            return null;
        }

        /// <summary>Increments the touch/overlap reference count for an item, adding a new entry if needed.</summary>
        private static void AddTouch<T>(Dictionary<T, int> touches, T item)
        {
            if (touches.TryGetValue(item, out int count))
                touches[item] = count + 1;
            else
                touches.Add(item, 1);
        }

        /// <summary>Decrements the touch reference count for an item and removes it from the dictionary when it reaches zero.</summary>
        private static void RemoveTouch<T>(Dictionary<T, int> touches, T item)
        {
            if (!touches.TryGetValue(item, out int count))
                return;

            count--;

            if (count <= 0)
                touches.Remove(item);
            else
                touches[item] = count;
        }

        /// <summary>Removes dictionary entries for units or buildings that have been destroyed since the last frame.</summary>
        private void CleanupDestroyedEntries()
        {
            _staleUnits.Clear();
            _staleBuildings.Clear();

            foreach (TeamComponent unit in _unitsInside.Keys)
            {
                if (unit == null)
                    _staleUnits.Add(unit);
            }

            foreach (GameObject building in _blockingBuildingsInside.Keys)
            {
                if (building == null)
                    _staleBuildings.Add(building);
            }

            foreach (TeamComponent unit in _staleUnits)
                _unitsInside.Remove(unit);

            foreach (GameObject building in _staleBuildings)
                _blockingBuildingsInside.Remove(building);
        }

        /// <summary>Syncs the BoxCollider size/center and optional visual plane scale with the serialized zone fields.</summary>
        private void ApplyZoneSettings()
        {
            ResolveZoneReferences();
            ClampZoneSize();

            if (_captureCollider != null)
            {
                _captureCollider.isTrigger = true;
                _captureCollider.size = _zoneSize;
                _captureCollider.center = _zoneCenter;
            }

            if (_captureZoneVisual == null || !_syncVisualWithZone)
                return;

            Vector3 visualPosition = _captureZoneVisual.localPosition;
            visualPosition.x = _zoneCenter.x;
            visualPosition.z = _zoneCenter.z;
            _captureZoneVisual.localPosition = visualPosition;

            Vector3 visualScale = _captureZoneVisual.localScale;
            visualScale.x = _zoneSize.x / DefaultPlaneSize;
            visualScale.z = _zoneSize.z / DefaultPlaneSize;
            _captureZoneVisual.localScale = visualScale;
        }

        /// <summary>Auto-populates the Outpost, BoxCollider, and visual transform references if they are not assigned in the Inspector.</summary>
        private void ResolveZoneReferences()
        {
            if (_outpost == null)
                _outpost = GetComponent<Outpost>();

            if (_captureCollider == null)
                _captureCollider = GetComponent<BoxCollider>();

            if (_captureZoneVisual == null)
            {
                Transform captureZone = transform.Find("CaptureZone");

                if (captureZone != null)
                    _captureZoneVisual = captureZone;
            }
        }

        private void ClampZoneSize()
        {
            _zoneSize.x = Mathf.Max(0.1f, Mathf.Abs(_zoneSize.x));
            _zoneSize.y = Mathf.Max(0.1f, Mathf.Abs(_zoneSize.y));
            _zoneSize.z = Mathf.Max(0.1f, Mathf.Abs(_zoneSize.z));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(_zoneCenter, _zoneSize);
            Gizmos.matrix = previousMatrix;
        }
    }
}
