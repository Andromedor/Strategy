using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Maps
{
    [CreateAssetMenu(fileName = "MapDefinition", menuName = "RTS/Maps/Map Definition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private string _mapId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _scenePath;
        [SerializeField] private Sprite _preview;
        [SerializeField, Min(2)] private int _maxPlayers = 4;
        [SerializeField] private List<SkirmishTeamMode> _supportedModes = new()
        {
            SkirmishTeamMode.OneVsOne,
            SkirmishTeamMode.TwoVsTwo
        };
        [SerializeField, Min(0)] private int _defaultStartingResources = 500;

        public string MapId => string.IsNullOrWhiteSpace(_mapId) ? name : _mapId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string ScenePath => _scenePath;
        public Sprite Preview => _preview;
        public int MaxPlayers => Mathf.Max(2, _maxPlayers);
        public IReadOnlyList<SkirmishTeamMode> SupportedModes => _supportedModes;
        public int DefaultStartingResources => Mathf.Max(0, _defaultStartingResources);

        public bool SupportsMode(SkirmishTeamMode mode)
        {
            return _supportedModes != null && _supportedModes.Contains(mode) && (int)mode <= MaxPlayers;
        }

        private void OnValidate()
        {
            _maxPlayers = Mathf.Max(2, _maxPlayers);
            _defaultStartingResources = Mathf.Max(0, _defaultStartingResources);
            _supportedModes ??= new List<SkirmishTeamMode>();

            for (int i = _supportedModes.Count - 1; i >= 0; i--)
            {
                if ((int)_supportedModes[i] > _maxPlayers)
                    _supportedModes.RemoveAt(i);
            }
        }
    }

}
