using System;
using System.Collections.Generic;
using System.IO;
using Strategy.Maps;
using UnityEngine;

namespace Strategy.Save
{
    public static class SaveGameFileIO
    {
        private const string SaveFolderName = "Saves";
        private const string QuickSaveFileName = "quick_save.json";

        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);
        public static string QuickSavePath => Path.Combine(SaveDirectory, QuickSaveFileName);

        public static void WriteQuickSave(SaveGameSnapshot snapshot)
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(QuickSavePath, JsonUtility.ToJson(snapshot, true));
        }

        public static bool TryRead(string path, out SaveGameSnapshot snapshot)
        {
            snapshot = null;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                snapshot = JsonUtility.FromJson<SaveGameSnapshot>(File.ReadAllText(path));
                return snapshot != null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGameFileIO: failed to read save '{path}': {exception.Message}");
                return false;
            }
        }

        public static void GetSaveFiles(List<string> results)
        {
            if (results == null)
                return;

            results.Clear();

            if (!Directory.Exists(SaveDirectory))
                return;

            results.AddRange(Directory.GetFiles(SaveDirectory, "*.json"));
            results.Sort((first, second) => File.GetLastWriteTimeUtc(second).CompareTo(File.GetLastWriteTimeUtc(first)));
        }

        public static string GetDisplayName(string path, MapCatalog catalog)
        {
            if (!TryRead(path, out SaveGameSnapshot snapshot))
                return Path.GetFileNameWithoutExtension(path);

            string mapName = snapshot.mapId;
            MapDefinition map = catalog != null ? catalog.FindById(snapshot.mapId) : null;
            if (map != null)
                mapName = map.DisplayName;

            string timestamp = string.IsNullOrWhiteSpace(snapshot.savedAtUtc)
                ? File.GetLastWriteTime(path).ToString("g")
                : snapshot.savedAtUtc;
            return $"{mapName} - {timestamp}";
        }
    }
}
