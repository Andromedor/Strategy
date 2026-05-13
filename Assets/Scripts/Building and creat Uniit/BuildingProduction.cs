using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BuildingProduction : MonoBehaviour
{
   [SerializeField] private Transform _pointPosition;
   private Queue<UnitData> _queue = new Queue<UnitData>();
   private bool _isProducing;
   
   public void AddToQueue(UnitData unitData)
   {
      _queue.Enqueue(unitData);
      
      if (!_isProducing)
         StartCoroutine(ProcessQueue());
   }
   
   private IEnumerator ProcessQueue()
   {
      _isProducing = true;

      while (_queue.Count > 0)
      {
         UnitData unit = _queue.Dequeue();

         yield return new WaitForSeconds(unit.ProductionTime);

         Instantiate(unit.Prefab, _pointPosition.position + Vector3.forward * 2, Quaternion.identity);
      }

      _isProducing = false;
   }
}
