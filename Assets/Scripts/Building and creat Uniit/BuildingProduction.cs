using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Data;
using UnitController;

public class BuildingProduction : MonoBehaviour
{
   [SerializeField] private Transform _pointPosition;
   [Header("Production")]
   [SerializeField] private ProductionConfig _productionConfig;
   
   private Queue<ProductionItemData> _queue = new Queue<ProductionItemData>();
   private TeamComponent _teamComponent;
   private bool _isProducing;
   
   public List<ProductionItemData> Items =>
      _productionConfig.Items;
   
   private void Awake()
   {
      _teamComponent = GetComponent<TeamComponent>();
   }
   
   public void AddToQueue(ProductionItemData item)
   {
      _queue.Enqueue(item);
      
      if (!_isProducing)
         StartCoroutine(ProcessQueue());
   }
   
   private IEnumerator ProcessQueue()
   {
      _isProducing = true;

      while (_queue.Count > 0)
      {
         ProductionItemData item = _queue.Dequeue();

         yield return new WaitForSeconds(item.ProductionTime);
         
         UnitData unit = item.UnitData;
         
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
