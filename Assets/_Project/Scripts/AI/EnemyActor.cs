using Mirror;
using Steading.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyActor : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.isStatic = false;
            foreach (var child in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.isStatic = false;
            }
        }
    }
}
