using System;
using System.Collections.Generic;
using Strategy.AI;
using Strategy.Core;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Save
{
    [Serializable]
    public sealed class SaveGameSnapshot
    {
        public string version = "1";
        public string savedAtUtc;
        public string mapId;
        public MatchLaunchMode mode;
        public SkirmishTeamMode teamMode;
        public TeamType localTeam;
        public int localPlayerId;
        public List<TeamSlotSnapshot> teams = new();
        public List<ResourceSnapshot> resources = new();
        public List<UnitSnapshot> units = new();
        public List<BuildingSnapshot> buildings = new();
        public List<OutpostSnapshot> outposts = new();

        public MatchLaunchConfig ToLaunchConfig(Strategy.Maps.MapDefinition map)
        {
            List<TeamLaunchSlot> launchSlots = new();
            for (int i = 0; i < teams.Count; i++)
            {
                TeamSlotSnapshot team = teams[i];
                launchSlots.Add(new TeamLaunchSlot(
                    team.team,
                    team.allianceId,
                    team.controller,
                    team.playerId,
                    team.spawnSlotIndex,
                    team.playerName,
                    team.aiDifficulty,
                    team.startingResources));
            }

            return new MatchLaunchConfig(mode, teamMode, map, localTeam, localPlayerId, launchSlots);
        }
    }

    [Serializable]
    public struct TeamSlotSnapshot
    {
        public TeamType team;
        public int allianceId;
        public TeamControllerKind controller;
        public int playerId;
        public int spawnSlotIndex;
        public string playerName;
        public AiDifficultyLevel aiDifficulty;
        public int startingResources;
    }

    [Serializable]
    public struct ResourceSnapshot
    {
        public TeamType team;
        public int amount;
    }

    [Serializable]
    public struct UnitSnapshot
    {
        public string unitId;
        public TeamType team;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public float currentHealth;
    }

    [Serializable]
    public sealed class BuildingSnapshot
    {
        public string buildingId;
        public TeamType team;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public float currentHealth;
        public SerializableVector2Int originCell;
        public int rotationSteps;
        public FactorySnapshot factory;
    }

    [Serializable]
    public sealed class FactorySnapshot
    {
        public string currentItemId;
        public float remainingSeconds;
        public List<string> queuedItemIds = new();
    }

    [Serializable]
    public struct OutpostSnapshot
    {
        public string sceneName;
        public SerializableVector3 position;
        public bool hasOwner;
        public TeamType owner;
        public bool hasCapturingTeam;
        public TeamType capturingTeam;
        public float captureProgressSeconds;
        public bool isUpgraded;
        public float resourceTimer;
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [Serializable]
    public struct SerializableVector2Int
    {
        public int x;
        public int y;

        public SerializableVector2Int(Vector2Int value)
        {
            x = value.x;
            y = value.y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }
    }
}
