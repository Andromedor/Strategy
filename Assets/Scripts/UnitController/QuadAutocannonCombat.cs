using System.Collections;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    public class QuadAutocannonCombat : UnitCombat
    {
        [Header("Autocannon")]
        [SerializeField] private Transform[] _muzzlePoints;
        [SerializeField] private AutocannonVisualEffects _autocannonEffects;
        [SerializeField] private float _barrelSpacing = 0.22f;
        [SerializeField] private string _leftMuzzleName = "ShootPoint_Left";
        [SerializeField] private string _rightMuzzleName = "ShootPoint_Right";

        private int _nextMuzzleIndex;

        protected override void Awake()
        {
            EnsureMuzzlePoints();
            EnsureEffects();
            base.Awake();
        }

        protected override IEnumerator FireAtTarget(Transform target)
        {
            if (_unitData == null || target == null || BulletPool.Instance == null)
                yield break;

            int muzzleIndex = GetNextMuzzleIndex();
            Transform muzzle = GetMuzzle(muzzleIndex);

            if (muzzle == null)
                yield break;

            if (!BulletPool.Instance.TryGetBullet(out GameObject bullet))
                yield break;

            Vector3 targetPoint = GetTargetPoint(target);
            Vector3 shotDirection = targetPoint - muzzle.position;

            bullet.transform.position = muzzle.position;
            bullet.transform.rotation = shotDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(shotDirection.normalized, Vector3.up)
                : muzzle.rotation;

            BulletController bulletController = bullet.GetComponent<BulletController>();

            if (bulletController == null)
            {
                BulletPool.Instance.ReturnBullet(bullet);
                yield break;
            }

            bulletController.Initialize(_unitData.Damage, _unitData.Speed, target, gameObject);
            _autocannonEffects?.PlayShotEffect(muzzleIndex);
        }

        private void EnsureMuzzlePoints()
        {
            if (HasUsableMuzzles())
                return;

            Transform gun = _gun != null ? _gun : transform;
            Transform center = _pointPosition != null ? _pointPosition : FindChildRecursive(gun, "ShootPoint");

            if (center == null)
                center = gun;

            Transform left = FindChildRecursive(gun, _leftMuzzleName) ??
                             CreateMuzzle(gun, center, _leftMuzzleName, -Mathf.Abs(_barrelSpacing));
            Transform right = FindChildRecursive(gun, _rightMuzzleName) ??
                              CreateMuzzle(gun, center, _rightMuzzleName, Mathf.Abs(_barrelSpacing));

            _muzzlePoints = new[] { left, right };

            if (_pointPosition == null)
                _pointPosition = center;
        }

        private void EnsureEffects()
        {
            if (_autocannonEffects == null)
                _autocannonEffects = GetComponent<AutocannonVisualEffects>();

            if (_autocannonEffects == null)
                _autocannonEffects = gameObject.AddComponent<AutocannonVisualEffects>();

            _autocannonEffects.Configure(_gun, _muzzlePoints);
        }

        private bool HasUsableMuzzles()
        {
            if (_muzzlePoints == null || _muzzlePoints.Length < 2)
                return false;

            for (int i = 0; i < _muzzlePoints.Length; i++)
            {
                if (_muzzlePoints[i] == null)
                    return false;
            }

            return true;
        }

        private int GetNextMuzzleIndex()
        {
            if (_muzzlePoints == null || _muzzlePoints.Length == 0)
                return 0;

            int index = Mathf.Clamp(_nextMuzzleIndex, 0, _muzzlePoints.Length - 1);
            _nextMuzzleIndex = (_nextMuzzleIndex + 1) % _muzzlePoints.Length;
            return index;
        }

        private Transform GetMuzzle(int muzzleIndex)
        {
            if (_muzzlePoints != null &&
                muzzleIndex >= 0 &&
                muzzleIndex < _muzzlePoints.Length &&
                _muzzlePoints[muzzleIndex] != null)
            {
                return _muzzlePoints[muzzleIndex];
            }

            return _pointPosition;
        }

        private static Transform CreateMuzzle(Transform parent, Transform center, string objectName, float localX)
        {
            GameObject muzzleObject = new GameObject(objectName);
            muzzleObject.layer = parent.gameObject.layer;
            muzzleObject.transform.SetParent(parent, false);
            muzzleObject.transform.localPosition = center.localPosition + new Vector3(localX, 0f, 0f);
            muzzleObject.transform.localRotation = center.localRotation;
            muzzleObject.transform.localScale = Vector3.one;
            return muzzleObject.transform;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.name == childName)
                    return child;

                Transform nested = FindChildRecursive(child, childName);

                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
