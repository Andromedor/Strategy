using System.Collections;
using UnityEngine;

public class GenerateEnemy : MonoBehaviour
{
    [SerializeField] private Transform[] _points;
    [SerializeField] private GameObject _factory;
    
    void Start()
    {
        
      //  StartCoroutine(SpawnFactory());
    }

    private IEnumerator SpawnFactory()
    {
        for (int i = 0; i < _points.Length; i++)
        {
            yield return new WaitForSeconds(10f);
          GameObject spawn = Instantiate(_factory);
          Destroy(spawn.GetComponent<BuildingPlacementManager>());
          spawn.transform.position = _points[i].position;
          spawn.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
          spawn.GetComponent<UnitCreat>().enabled = true;
          spawn.GetComponent<UnitCreat>().IsEnemy = true;
          
        }
    }
}
