using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Weather
{
    public class Rain : MonoBehaviour
    {
        [SerializeField] private float _rainIntervalFrom = 5f;
        [SerializeField] private float _rainIntervalTo = 10f;
        [SerializeField] private float _rainIntensity = 0.25f;
        [SerializeField] private float _normalIntensity = 2f;
        [SerializeField] private float _transitionDuration = 2f;
        [SerializeField] private Light _light;

        private ParticleSystem _particleSystem;
       private bool _isRaining;

        private void Start()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _isRaining = false;

            StartCoroutine(RainCycle());

        }

        private IEnumerator RainCycle()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_rainIntervalFrom, _rainIntervalTo));
             

                if (_isRaining)
                {
                    _particleSystem.Stop();
                    _isRaining = false;
                    yield return StartCoroutine(ChangeLight(_rainIntensity));
                }
                else
                {
                    _particleSystem.Play();
                    _isRaining = true;
                    yield return StartCoroutine(ChangeLight(_normalIntensity));
                }

               //_isRaining = !_isRaining; 
            }
        }

        private IEnumerator ChangeLight(float targetIntensity)
        {
            float start = _light.intensity;
            float time = 0f;
            
            while (time < _transitionDuration)  
            {
                time += Time.deltaTime;

                _light.intensity = Mathf.Lerp(start, targetIntensity, time / _transitionDuration);

                yield return null;
            }

            _light.intensity = targetIntensity;
        }
    }
}
