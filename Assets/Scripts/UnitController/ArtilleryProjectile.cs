using System.Collections.Generic;
using Building_and_creat_Uniit;
using UnityEngine;

namespace UnitController
{
    public class ArtilleryProjectile : MonoBehaviour
    {
        private Vector3 _start;
        private Vector3 _impactPoint;
        private float _arcHeight;
        private float _flightTime;
        private float _elapsed;
        private GameObject _owner;
        private TeamComponent _ownerTeam;
        private Transform _directTarget;
        private bool _isDirectHit;
        private float _damage;
        private float _splashRadius;
        private float _directDamageMinMultiplier;
        private float _directDamageMaxMultiplier;
        private float _splashDamageMinMultiplier;
        private float _splashDamageMaxMultiplier;
        private Vector3 _lastPosition;

        private static Shader _cachedShader;
        private static Mesh _cachedParticleMesh;

        /// <summary>
        /// Створює ігровий об'єкт-снаряд (сфера-примітив) із матеріалом і компонентом снаряда.
        /// </summary>
        public static ArtilleryProjectile Create(Vector3 position)
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "Artillery Shell";
            projectileObject.transform.position = position;
            projectileObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.45f);

            Collider collider = projectileObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            MeshRenderer renderer = projectileObject.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateMaterial(new Color(0.12f, 0.115f, 0.1f, 1f), 0.45f);

            return projectileObject.AddComponent<ArtilleryProjectile>();
        }

        /// <summary>
        /// Встановлює всі параметри польоту, пошкодження та цілі перед запуском снаряда.
        /// </summary>
        public void Initialize(
            Vector3 start,
            Vector3 impactPoint,
            float arcHeight,
            float flightTime,
            GameObject owner,
            TeamComponent ownerTeam,
            Transform directTarget,
            bool isDirectHit,
            float damage,
            float splashRadius,
            float directDamageMinMultiplier,
            float directDamageMaxMultiplier,
            float splashDamageMinMultiplier,
            float splashDamageMaxMultiplier)
        {
            _start = start;
            _impactPoint = impactPoint;
            _arcHeight = arcHeight;
            _flightTime = Mathf.Max(0.05f, flightTime);
            _owner = owner;
            _ownerTeam = ownerTeam;
            _directTarget = directTarget;
            _isDirectHit = isDirectHit;
            _damage = damage;
            _splashRadius = splashRadius;
            _directDamageMinMultiplier = directDamageMinMultiplier;
            _directDamageMaxMultiplier = directDamageMaxMultiplier;
            _splashDamageMinMultiplier = splashDamageMinMultiplier;
            _splashDamageMaxMultiplier = splashDamageMaxMultiplier;
            _lastPosition = start;
        }

        /// <summary>
        /// Кожен кадр просуває снаряд по параболічній траєкторії; при досягненні кінця — детонує.
        /// </summary>
        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _flightTime);
            Vector3 nextPosition = GetArcPosition(t);
            Vector3 direction = nextPosition - _lastPosition;

            transform.position = nextPosition;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);

            _lastPosition = nextPosition;

            if (t >= 1f)
                Detonate();
        }

        /// <summary>
        /// Обчислює позицію на траєкторії у момент часу t ∈ [0, 1] з дугою висотою _arcHeight.
        /// </summary>
        private Vector3 GetArcPosition(float t)
        {
            Vector3 position = Vector3.Lerp(_start, _impactPoint, t);
            position.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;
            return position;
        }

        /// <summary>
        /// Вимикає компонент, створює ефект вибуху, застосовує пошкодження та знищує снаряд.
        /// </summary>
        private void Detonate()
        {
            enabled = false;
            CreateExplosionEffect(_impactPoint);
            ApplyDamage();
            Destroy(gameObject);
        }

        /// <summary>
        /// Завдає пряме пошкодження цілі та сплеш-пошкодження всім юнітам у радіусі вибуху.
        /// </summary>
        private void ApplyDamage()
        {
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();

            if (_isDirectHit && _directTarget != null)
            {
                IDamageable directDamageable = _directTarget.GetComponentInParent<IDamageable>();

                if (directDamageable != null && CanDamage(_directTarget))
                {
                    float directMultiplier = Random.Range(_directDamageMinMultiplier, _directDamageMaxMultiplier);
                    directDamageable.TakeDamage(_damage * directMultiplier);
                    damaged.Add(directDamageable);
                }
            }

            Collider[] hits = Physics.OverlapSphere(_impactPoint, _splashRadius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (_owner != null && hit.transform.IsChildOf(_owner.transform))
                    continue;

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable == null || damaged.Contains(damageable))
                    continue;

                ITeam targetTeam = hit.GetComponentInParent<ITeam>();

                if (_ownerTeam != null && targetTeam != null && targetTeam.Team == _ownerTeam.Team)
                    continue;

                float splashMultiplier = Random.Range(_splashDamageMinMultiplier, _splashDamageMaxMultiplier);
                damageable.TakeDamage(_damage * splashMultiplier);
                damaged.Add(damageable);
            }
        }

        /// <summary>
        /// Перевіряє, чи можна атакувати ціль: власника та союзників не атакуємо.
        /// </summary>
        private bool CanDamage(Transform target)
        {
            if (_owner != null && target.IsChildOf(_owner.transform))
                return false;

            ITeam targetTeam = target.GetComponentInParent<ITeam>();

            if (_ownerTeam != null && targetTeam != null && targetTeam.Team == _ownerTeam.Team)
                return false;

            return true;
        }

        /// <summary>
        /// Створює візуальний ефект вибуху: точкове світло, спалах, дим і уламки.
        /// </summary>
        private static void CreateExplosionEffect(Vector3 position)
        {
            GameObject effectObject = new GameObject("Artillery Explosion");
            effectObject.transform.position = position;

            Light light = effectObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.15f);
            light.range = 4.5f;
            const float explosionLightIntensity = 7f;
            light.intensity = explosionLightIntensity;

            ParticleSystem flash = CreateParticleSystem(
                effectObject.transform,
                "Explosion Flash",
                new Color(1f, 0.62f, 0.12f, 0.95f),
                new Color(1f, 0.18f, 0.02f, 0.65f),
                0.05f,
                0.12f,
                0.22f,
                0.48f,
                34,
                9f,
                0.06f);

            ParticleSystem smoke = CreateParticleSystem(
                effectObject.transform,
                "Explosion Smoke",
                new Color(0.42f, 0.39f, 0.32f, 0.5f),
                new Color(0.18f, 0.16f, 0.13f, 0.24f),
                0.45f,
                0.95f,
                0.12f,
                0.34f,
                72,
                4.8f,
                0.12f,
                0.15f);

            ParticleSystem debris = CreateParticleSystem(
                effectObject.transform,
                "Explosion Debris",
                new Color(0.25f, 0.18f, 0.1f, 1f),
                new Color(0.12f, 0.09f, 0.06f, 0.85f),
                0.25f,
                0.6f,
                0.045f,
                0.11f,
                64,
                12f,
                0.08f,
                1.2f);

            ParticleSystem sparks = CreateParticleSystem(
                effectObject.transform,
                "Explosion Sparks",
                new Color(1f, 0.82f, 0.22f, 1f),
                new Color(1f, 0.32f, 0.04f, 0.4f),
                0.12f,
                0.32f,
                0.025f,
                0.06f,
                38,
                16f,
                0.04f,
                0.8f);

            flash.Play();
            smoke.Play();
            debris.Play();
            sparks.Play();

            effectObject.AddComponent<ArtilleryExplosionCleanup>().Initialize(light, 1.35f, explosionLightIntensity);
        }

        /// <summary>
        /// Будує та налаштовує систему частинок із заданими параметрами; повертає готовий ParticleSystem.
        /// </summary>
        private static ParticleSystem CreateParticleSystem(
            Transform parent,
            string objectName,
            Color colorA,
            Color colorB,
            float minLifetime,
            float maxLifetime,
            float minSize,
            float maxSize,
            int particles,
            float speed,
            float shapeRadius = 0.08f,
            float gravityModifier = 0f)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = Vector3.zero;

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = speed;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravityModifier;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, particles) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = shapeRadius;

            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(colorA, 0f),
                    new GradientColorKey(colorB, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(colorA.a, 0f),
                    new GradientAlphaKey(colorB.a * 0.65f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetParticleMesh();
            renderer.material = CreateMaterial(colorA, 0.35f);

            return particleSystem;
        }

        private static Mesh GetParticleMesh()
        {
            if (_cachedParticleMesh != null)
                return _cachedParticleMesh;

            GameObject meshSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cachedParticleMesh = meshSource.GetComponent<MeshFilter>().sharedMesh;

            if (Application.isPlaying)
                Destroy(meshSource);
            else
                DestroyImmediate(meshSource);

            return _cachedParticleMesh;
        }

        /// <summary>
        /// Створює матеріал із заданим кольором і гладкістю; шейдер шукається один раз і кешується.
        /// </summary>
        private static Material CreateMaterial(Color color, float smoothness)
        {
            if (_cachedShader == null)
            {
                _cachedShader =
                    Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard") ??
                    Shader.Find("Sprites/Default");
            }

            Material material = new Material(_cachedShader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            return material;
        }
    }

    public sealed class ArtilleryExplosionCleanup : MonoBehaviour
    {
        private Light _light;
        private float _lifetime;
        private float _elapsed;
        private float _initialIntensity;

        /// <summary>
        /// Передає посилання на джерело світла, тривалість ефекту та початкову інтенсивність для згасання.
        /// </summary>
        public void Initialize(Light explosionLight, float lifetime, float initialIntensity)
        {
            _light = explosionLight;
            _lifetime = lifetime;
            _initialIntensity = initialIntensity;
        }

        /// <summary>
        /// Кожен кадр зменшує інтенсивність світла до нуля; після закінчення тривалості знищує об'єкт.
        /// </summary>
        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_light != null)
                _light.intensity = Mathf.Lerp(_initialIntensity, 0f, Mathf.Clamp01(_elapsed / 0.2f));

            if (_elapsed >= _lifetime)
                Destroy(gameObject);
        }
    }
}
