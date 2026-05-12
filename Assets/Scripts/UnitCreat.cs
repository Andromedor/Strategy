using System;
using UnityEngine;
using System.Collections;
using System.Net;
using NUnit.Framework;
using Random = UnityEngine.Random;

public class UnitCreat : MonoBehaviour
{
  [SerializeField] private GameObject _prefab;
  [SerializeField] private GameObject _spawnPrefab;
  [SerializeField] private float _time = 5f;

  [NonSerialized] public bool IsEnemy = false;
  
  private void Start()
  {
    //  StartCoroutine(SpawnUnit());
  }

  IEnumerator SpawnUnit()
  {
    Vector3 spawnPosition = _spawnPrefab.transform.position;
    
    for (int i = 1; i <= 5; i++)
    {
      yield return new WaitForSeconds(_time);
      Vector3 randomPosition = new Vector3(spawnPosition.x + Random.Range(-3, 3), spawnPosition.y, spawnPosition.z + Random.Range(-3, 0));
     GameObject spawn = Instantiate(_prefab, randomPosition, Quaternion.identity);

     if (IsEnemy)
     {
       spawn.tag = "Enemy";
     }
    }
  }
}
