using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnitController;

public class BuildingProduction : MonoBehaviour
{
   [SerializeField] private Transform _pointPosition;
   private Queue<UnitData> _queue = new Queue<UnitData>();
   private TeamComponent _teamComponent;
   private bool _isProducing;
   
   private void Awake()
   {
      _teamComponent = GetComponent<TeamComponent>();
   }
   
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
         
         GameObject spawnedUnit = Instantiate(unit.Prefab, _pointPosition.position + Vector3.forward * 2, Quaternion.identity);
         
         TeamComponent unitTeam =
            spawnedUnit.GetComponent<TeamComponent>();

         if(unitTeam != null)
         {
            unitTeam.SetTeam(_teamComponent.Team);
            spawnedUnit.layer =
               _teamComponent.Team == TeamType.Player
                  ? LayerMask.NameToLayer("PlayerUnit")
                  : LayerMask.NameToLayer("EnemyUnit");
         }
      }

      _isProducing = false;
   }
}
