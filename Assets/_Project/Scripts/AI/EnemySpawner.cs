using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.AI
{
    // Server-side scene component. When the world scene loads on the server, it
    // spawns N enemies in a ring around its own position, snapped to the
    // nearest NavMesh point so they can navigate immediately.
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int count = 3;
        [SerializeField] private float radius = 12f;
        [SerializeField] private float navMeshSampleDistance = 5f;

        private void Start()
        {
            if (!NetworkServer.active) return;
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[Steading] EnemySpawner has no prefab assigned.", this);
                return;
            }

            for (int i = 0; i < count; i++) Spawn(i);
        }

        private void Spawn(int index)
        {
            var angle = index / (float)count * Mathf.PI * 2f;
            var basePos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            if (NavMesh.SamplePosition(basePos, out var hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                basePos = hit.position;
            }

            var instance = Instantiate(enemyPrefab, basePos, Quaternion.identity);
            NetworkServer.Spawn(instance);
        }
    }
}
