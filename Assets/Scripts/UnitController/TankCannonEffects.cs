using System.Collections;
using UnityEngine;

namespace UnitController
{
    public class TankCannonEffects : MonoBehaviour
    {
        [SerializeField] private Transform _gun;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Vector3 _recoilLocalDirection = Vector3.back;
        [SerializeField] private float _recoilDistance = 0.35f;
        [SerializeField] private float _recoilBackDuration = 0.045f;
        [SerializeField] private float _recoilReturnDuration = 0.16f;
        [SerializeField] private int _flashParticles = 18;
        [SerializeField] private int _smokeParticles = 24;
        [SerializeField] private bool _useMuzzleLight = true;

        private ParticleSystem _muzzleFlash;
        private ParticleSystem _smoke;
        private Light _muzzleLight;
        private Material _flashMaterial;
        private Material _smokeMaterial;
        private Coroutine _recoilCoroutine;
        private Coroutine _lightCoroutine;
        private Vector3 _gunRestLocalPosition;
        private bool _hasGunRestPosition;

        private void Awake()
        {
            EnsureReady();
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_flashMaterial);
            DestroyRuntimeObject(_smokeMaterial);
        }

        public void Configure(Transform gun, Transform muzzle)
        {
            if (_gun == null)
                _gun = gun;

            if (_muzzle == null)
                _muzzle = muzzle;

            EnsureReady();
        }

        public void PlayShotEffect()
        {
            EnsureReady();

            if (_muzzleFlash != null)
                _muzzleFlash.Emit(_flashParticles);

            if (_smoke != null)
                _smoke.Emit(_smokeParticles);

            if (_gun != null)
            {
                if (_recoilCoroutine != null)
                    StopCoroutine(_recoilCoroutine);

                _recoilCoroutine = StartCoroutine(PlayRecoil());
            }

            if (_useMuzzleLight && _muzzleLight != null)
            {
                if (_lightCoroutine != null)
                    StopCoroutine(_lightCoroutine);

                _lightCoroutine = StartCoroutine(FlashMuzzleLight());
            }
        }

        private void EnsureReady()
        {
            if (_gun != null && !_hasGunRestPosition)
            {
                _gunRestLocalPosition = _gun.localPosition;
                _hasGunRestPosition = true;
            }

            if (_muzzle == null)
                return;

            if (_muzzleFlash == null)
                _muzzleFlash = CreateMuzzleFlash();

            if (_smoke == null)
                _smoke = CreateSmoke();

            if (_useMuzzleLight && _muzzleLight == null)
                _muzzleLight = CreateMuzzleLight();
        }

        private ParticleSystem CreateMuzzleFlash()
        {
            ParticleSystem particles = CreateParticleSystem("Muzzle Flash");

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 4.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.34f, 0.95f),
                new Color(1f, 0.38f, 0.08f, 0.8f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _flashMaterial = CreateParticleMaterial(new Color(1f, 0.66f, 0.12f, 0.9f));
            renderer.sortingFudge = 2f;

            return particles;
        }

        private ParticleSystem CreateSmoke()
        {
            ParticleSystem particles = CreateParticleSystem("Muzzle Smoke");

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.43f, 0.39f, 0.5f),
                new Color(0.18f, 0.17f, 0.16f, 0.25f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.08f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.5f, 0.48f, 0.44f), 0f),
                    new GradientColorKey(new Color(0.25f, 0.24f, 0.22f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.45f, 0f),
                    new GradientAlphaKey(0.12f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _smokeMaterial = CreateParticleMaterial(new Color(0.42f, 0.4f, 0.36f, 0.45f));
            renderer.sortingFudge = 1f;

            return particles;
        }

        private ParticleSystem CreateParticleSystem(string objectName)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.layer = gameObject.layer;
            particleObject.transform.SetParent(_muzzle, false);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            return particles;
        }

        private Light CreateMuzzleLight()
        {
            GameObject lightObject = new GameObject("Muzzle Light");
            lightObject.layer = gameObject.layer;
            lightObject.transform.SetParent(_muzzle, false);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.18f);
            light.range = 4f;
            light.intensity = 0f;
            light.enabled = false;

            return light;
        }

        private IEnumerator PlayRecoil()
        {
            if (!_hasGunRestPosition)
                yield break;

            Vector3 recoilDirection = _recoilLocalDirection.sqrMagnitude > 0.001f
                ? _recoilLocalDirection.normalized
                : Vector3.back;
            Vector3 currentPosition = _gun.localPosition;
            Vector3 recoilPosition = _gunRestLocalPosition + recoilDirection * _recoilDistance;

            yield return MoveGun(currentPosition, recoilPosition, _recoilBackDuration);
            yield return MoveGun(recoilPosition, _gunRestLocalPosition, _recoilReturnDuration);

            _gun.localPosition = _gunRestLocalPosition;
            _recoilCoroutine = null;
        }

        private IEnumerator MoveGun(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                _gun.localPosition = to;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0f, 1f, t);
                _gun.localPosition = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            _gun.localPosition = to;
        }

        private IEnumerator FlashMuzzleLight()
        {
            _muzzleLight.enabled = true;
            _muzzleLight.intensity = 8f;
            yield return new WaitForSeconds(0.045f);
            _muzzleLight.intensity = 0f;
            _muzzleLight.enabled = false;
            _lightCoroutine = null;
        }

        private static Material CreateParticleMaterial(Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            SetMaterialColor(material, color);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeObject);
            else
                DestroyImmediate(runtimeObject);
        }
    }
}
