using System.Collections.Generic;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Зберігає поточні зарезервовані точки призначення юнітів, щоб різні системи наказів
    /// не відправляли техніку в одну й ту саму координату.
    /// </summary>
    public static class UnitDestinationReservations
    {
        private struct Reservation
        {
            public Vector3 Destination;
            public float Radius;
        }

        private static readonly Dictionary<GameObject, Reservation> Reservations = new();
        private static readonly List<GameObject> StaleOwners = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Reservations.Clear();
            StaleOwners.Clear();
        }

        /// <summary>Бронює майбутню або поточну точку призначення для юніта; повторний виклик оновлює його бронювання.</summary>
        public static void Reserve(GameObject owner, Vector3 destination, float radius)
        {
            if (owner == null)
                return;

            Reservations[owner] = new Reservation
            {
                Destination = destination,
                Radius = Mathf.Max(0.1f, radius)
            };
        }

        /// <summary>Звільняє бронювання, коли юніт доїхав, отримав інший наказ, був вимкнений або знищений.</summary>
        public static void Release(GameObject owner)
        {
            if (owner != null)
                Reservations.Remove(owner);
        }

        /// <summary>Перевіряє, чи позицію вже тримає інший юніт, враховуючи радіус його та нового бронювання.</summary>
        public static bool IsReservedByOther(GameObject owner, Vector3 destination, float radius)
        {
            RemoveStaleReservations();

            float ownRadius = Mathf.Max(0.1f, radius);

            foreach (KeyValuePair<GameObject, Reservation> pair in Reservations)
            {
                GameObject reservedOwner = pair.Key;

                if (reservedOwner == null || reservedOwner == owner)
                    continue;

                Vector3 offset = pair.Value.Destination - destination;
                offset.y = 0f;

                float blockedDistance = ownRadius + pair.Value.Radius;

                if (offset.sqrMagnitude <= blockedDistance * blockedDistance)
                    return true;
            }

            return false;
        }

        private static void RemoveStaleReservations()
        {
            StaleOwners.Clear();

            foreach (GameObject owner in Reservations.Keys)
            {
                if (owner == null)
                    StaleOwners.Add(owner);
            }

            foreach (GameObject staleOwner in StaleOwners)
                Reservations.Remove(staleOwner);
        }
    }
}
