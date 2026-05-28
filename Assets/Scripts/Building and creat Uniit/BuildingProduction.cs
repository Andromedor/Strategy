using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Building_and_creat_Uniit;
using Data;
using DefaultNamespace;
using UnitController;

public class BuildingProduction : MonoBehaviour
{
   [Header("Spawn Points")]
   [SerializeField] private Transform _unitSpawnPoint;
   [SerializeField] private Transform _unitExitPoint;
   // Куди юніт має виїхати після створення.

   [Header("Production")]
   [SerializeField] private ProductionConfig _productionConfig;
   
   [Header("Gate")]
   [SerializeField] private FactoryGate _gate;
   // Компонент воріт заводу.
   
   private Queue<ProductionItemData> _queue = new Queue<ProductionItemData>();
   private TeamComponent _teamComponent;
   private bool _isProducing;
   
   public List<ProductionItemData> Items =>
      _productionConfig.Items;
   
   private void Awake()
   {
      _teamComponent = GetComponent<TeamComponent>();
   }
   
   public bool AddToQueue(ProductionItemData item)
   {
      if (item == null)
         return false;

      if (_teamComponent != null &&
          _teamComponent.Team == TeamType.Player &&
          ResourceManager.Instance != null &&
          !ResourceManager.Instance.Spend(item.Cost))
      {
         return false;
      }

      _queue.Enqueue(item);
      
      if (!_isProducing)
         StartCoroutine(ProcessQueue());

      return true;
   }
   
   private IEnumerator ProcessQueue()
   {
      _isProducing = true;

      while (_queue.Count > 0)
      {
         ProductionItemData item = _queue.Dequeue();

         yield return new WaitForSeconds(item.ProductionTime);

         yield return StartCoroutine(SpawnAndReleaseUnit(item));
      }

      _isProducing = false;
   }
   
   private IEnumerator SpawnAndReleaseUnit(ProductionItemData item)
   {
      UnitData unitData = item.UnitData;
      Transform unitsContainer = RuntimeObjectContainer.Get("Units");

      GameObject spawnedUnit = Instantiate(
         unitData.Prefab,
         _unitSpawnPoint.position,
         _unitSpawnPoint.rotation,
         unitsContainer
      );

      SetupUnitTeam(spawnedUnit);
      DisableUnitBeforeExit(spawnedUnit);

      if (_gate != null)
         yield return StartCoroutine(_gate.Open());

      UnitSpawnActivator activator = spawnedUnit.GetComponent<UnitSpawnActivator>();

      if (activator != null)
         yield return StartCoroutine(activator.MoveOutOfFactory(_unitExitPoint.position));

      if (_gate != null)
         yield return StartCoroutine(_gate.Close());
   }

   private void SetupUnitTeam(GameObject spawnedUnit)
   {
      TeamComponent unitTeam = spawnedUnit.GetComponent<TeamComponent>();

      if (unitTeam != null)
      {
         unitTeam.SetTeam(_teamComponent.Team);

         spawnedUnit.layer =
            _teamComponent.Team == TeamType.Player
               ? LayerMask.NameToLayer("PlayerUnit")
               : LayerMask.NameToLayer("EnemyUnit");
      }
   }

   private void DisableUnitBeforeExit(GameObject spawnedUnit)
   {
      UnitSpawnActivator activator = spawnedUnit.GetComponent<UnitSpawnActivator>();

      if (activator != null)
         activator.SetSpawningState(true);
   }
}
