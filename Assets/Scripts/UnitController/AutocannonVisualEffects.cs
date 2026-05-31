using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Керує візуальними ефектами для кожного дула Quad Autocannon: спалах, дим та необов'язкове
    /// точкове світло для кожного ствола, плюс єдина спільна анімація відкату гармати. Усі системи
    /// частинок та світла створюються процедурно під час виконання.
    /// </summary>
    public class AutocannonVisualEffects : MonoBehaviour
    {
        [SerializeField] private Transform _gun;
        [SerializeField] private Transform[] _muzzles;
        [SerializeField] private Vector3 _recoilLocalDirection = Vector3.back;
        [SerializeField] private float _recoilDistance = 0.11f;
        [SerializeField] private float _recoilBackDuration = 0.025f;
        [SerializeField] private float _recoilReturnDuration = 0.085f;
        [SerializeField] private int _flashParticles = 8;
        [SerializeField] private int _smokeParticles = 6;
        [SerializeField] private bool _useMuzzleLight = true;

        private readonly List<MuzzleEffectSet> _effectSets = new List<MuzzleEffectSet>();
        private Material _flashMaterial;
        private Material _smokeMaterial;
        private Coroutine _recoilCoroutine;
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

        /// <summary>
        /// Призначає посилання на гармату та дула, якщо вони ще не встановлені, потім викликає EnsureReady
        /// для побудови наборів ефектів для кожного дула.
        /// </summary>
        public void Configure(Transform gun, Transform[] muzzles)
        {
            if (_gun == null)
                _gun = gun;

            if (muzzles != null && muzzles.Length > 0)
                _muzzles = muzzles;

            EnsureReady();
        }

        /// <summary>
        /// Випромінює частинки спалаху та диму для вказаного індексу дула, спалахує його точкове світло,
        /// та запускає спільний відкат гармати. Будь-яка поточна корутина світла або відкату перезапускається.
        /// </summary>
        public void PlayShotEffect(int muzzleIndex)
        {
            EnsureReady();

            if (_effectSets.Count == 0)
                return;

            int index = Mathf.Clamp(muzzleIndex, 0, _effectSets.Count - 1);
            MuzzleEffectSet effectSet = _effectSets[index];

            if (effectSet.Flash != null)
                effectSet.Flash.Emit(_flashParticles);

            if (effectSet.Smoke != null)
                effectSet.Smoke.Emit(_smokeParticles);

            if (_useMuzzleLight && effectSet.Light != null)
            {
                if (effectSet.LightCoroutine != null)
                    StopCoroutine(effectSet.LightCoroutine);

                effectSet.LightCoroutine = StartCoroutine(FlashMuzzleLight(effectSet));
            }

            if (_gun != null)
            {
                if (_recoilCoroutine != null)
                    StopCoroutine(_recoilCoroutine);

                _recoilCoroutine = StartCoroutine(PlayRecoil());
            }
        }

        /// <summary>
        /// Ліниво ініціалізує всі об'єкти під час виконання: записує позицію спокою гармати, створює спільні матеріали,
        /// та будує один MuzzleEffectSet для кожного трансформу дула, коли список не синхронізований.
        /// </summary>
        private void EnsureReady()
        {
            if (_gun != null && !_hasGunRestPosition)
            {
                _gunRestLocalPosition = _gun.localPosition;
                _hasGunRestPosition = true;
            }

            if (_muzzles == null || _muzzles.Length == 0 || EffectsMatchMuzzles())
                return;

            if (_flashMaterial == null)
                _flashMaterial = CreateParticleMaterial(new Color(1f, 0.68f, 0.14f, 0.95f));

            if (_smokeMaterial == null)
                _smokeMaterial = CreateParticleMaterial(new Color(0.38f, 0.36f, 0.32f, 0.42f));

            _effectSets.Clear();

            for (int i = 0; i < _muzzles.Length; i++)
            {
                Transform muzzle = _muzzles[i];

                if (muzzle == null)
                    continue;

                _effectSets.Add(new MuzzleEffectSet(
                    muzzle,
                    CreateMuzzleFlash(muzzle),
                    CreateSmoke(muzzle),
                    _useMuzzleLight ? CreateMuzzleLight(muzzle) : null));
            }
        }

        /// <summary>
        /// Повертає true, коли список наборів ефектів вже відповідає поточному масиву _muzzles за
        /// кількістю та ідентичністю кожного елемента.
        /// </summary>
        private bool EffectsMatchMuzzles()
        {
            if (_muzzles == null || _effectSets.Count != _muzzles.Length)
                return false;

            for (int i = 0; i < _muzzles.Length; i++)
            {
                if (_effectSets[i].Muzzle != _muzzles[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Будує систему частинок спалаху дула у вузькому конусі як дочірній об'єкт заданого трансформу дула.
        /// Використовує спільний _flashMaterial та вибухи на основі Emit.
        /// </summary>
        private ParticleSystem CreateMuzzleFlash(Transform muzzle)
        {
            ParticleSystem particles = CreateParticleSystem(muzzle, "Autocannon Muzzle Flash");

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.6f, 6.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.94f, 0.42f, 0.95f),
                new Color(1f, 0.34f, 0.04f, 0.82f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.025f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _flashMaterial;
            renderer.sortingFudge = 2f;

            return particles;
        }

        /// <summary>
        /// Будує систему частинок диму з ширшим конусом та градієнтом затухання кольору як дочірній об'єкт
        /// заданого трансформу дула, використовуючи спільний _smokeMaterial.
        /// </summary>
        private ParticleSystem CreateSmoke(Transform muzzle)
        {
            ParticleSystem particles = CreateParticleSystem(muzzle, "Autocannon Muzzle Smoke");

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.44f, 0.42f, 0.38f, 0.34f),
                new Color(0.17f, 0.16f, 0.15f, 0.18f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.045f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.48f, 0.46f, 0.42f), 0f),
                    new GradientColorKey(new Color(0.22f, 0.21f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.32f, 0f),
                    new GradientAlphaKey(0.1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _smokeMaterial;
            renderer.sortingFudge = 1f;

            return particles;
        }

        /// <summary>
        /// Створює новий GameObject зі зупиненою ParticleSystem як дочірній об'єкт дула; спільний
        /// допоміжний метод для побудови систем спалаху та диму.
        /// </summary>
        private ParticleSystem CreateParticleSystem(Transform muzzle, string objectName)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.layer = gameObject.layer;
            particleObject.transform.SetParent(muzzle, false);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            return particles;
        }

        /// <summary>
        /// Створює мале помаранчеве точкове світло у позиції дула, спочатку вимкнене, для використання
        /// корутиною FlashMuzzleLight.
        /// </summary>
        private Light CreateMuzzleLight(Transform muzzle)
        {
            GameObject lightObject = new GameObject("Autocannon Muzzle Light");
            lightObject.layer = gameObject.layer;
            lightObject.transform.SetParent(muzzle, false);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.56f, 0.12f);
            light.range = 2.2f;
            light.intensity = 0f;
            light.enabled = false;

            return light;
        }

        /// <summary>
        /// Корутина, що переміщує гармату до позиції відкату за _recoilBackDuration, потім повертає
        /// її до спокою за _recoilReturnDuration, використовуючи інтерполяцію smooth-step.
        /// </summary>
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

        /// <summary>
        /// Корутина, що інтерполює локальну позицію гармати з 'from' в 'to' за duration
        /// секунд з кривою smooth-step.
        /// </summary>
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

        /// <summary>
        /// Корутина, що вмикає задане підсвітку дула на повну інтенсивність на 32 мс, потім вимикає її.
        /// </summary>
        private IEnumerator FlashMuzzleLight(MuzzleEffectSet effectSet)
        {
            effectSet.Light.enabled = true;
            effectSet.Light.intensity = 5f;
            yield return new WaitForSeconds(0.032f);
            effectSet.Light.intensity = 0f;
            effectSet.Light.enabled = false;
            effectSet.LightCoroutine = null;
        }

        /// <summary>
        /// Створює новий матеріал частинок без освітлення, використовуючи найкращий доступний шейдер,
        /// та встановлює властивості кольору для URP та застарілих конвеєрів Standard.
        /// </summary>
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

        /// <summary>
        /// Встановлює _BaseColor та _Color на матеріалі, коли ці властивості існують, охоплюючи як
        /// URP, так і назви властивостей застарілого Standard шейдера.
        /// </summary>
        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        /// <summary>
        /// Безпечно знищує об'єкт UnityEngine.Object під час виконання як у режимі гри, так і в режимі редагування.
        /// </summary>
        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeObject);
            else
                DestroyImmediate(runtimeObject);
        }

        /// <summary>
        /// Групує системи частинок, світло та трансформ дула для одного ствола.
        /// LightCoroutine є змінним, щоб посилання на корутину спалаху можна було перезапустити для кожного пострілу.
        /// </summary>
        private sealed class MuzzleEffectSet
        {
            public MuzzleEffectSet(Transform muzzle, ParticleSystem flash, ParticleSystem smoke, Light light)
            {
                Muzzle = muzzle;
                Flash = flash;
                Smoke = smoke;
                Light = light;
            }

            public Transform Muzzle { get; }
            public ParticleSystem Flash { get; }
            public ParticleSystem Smoke { get; }
            public Light Light { get; }
            public Coroutine LightCoroutine { get; set; }
        }
    }
}
