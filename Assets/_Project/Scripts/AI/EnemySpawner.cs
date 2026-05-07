using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steading.Combat;
using Steading.World;
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
        [SerializeField] private bool spawnFromForts = true;
        [SerializeField] private float fortSpawnRadius = 7.5f;
        [SerializeField] private float firstWaveDelay = 1f;
        [SerializeField] private float waveInterval = 28f;
        [SerializeField] private int addPerWave = 1;
        [SerializeField] private int maxAlive = 14;
        [SerializeField] private float difficultyHealthPerWave = 8f;
        [SerializeField] private float difficultyDamagePerWave = 1.5f;

        private readonly List<GameObject> _alive = new List<GameObject>();
        private int _waveIndex;
        private Coroutine _waves;

        private void Start()
        {
            if (!NetworkServer.active) return;
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[Steading] EnemySpawner has no prefab assigned.", this);
                return;
            }
            if (enemyPrefab.GetComponent<EnemyController>() == null)
            {
                Debug.LogWarning("[Steading] EnemySpawner prefab is not an EnemyController actor. Assign Draugr.prefab, not camp or terrain geometry.", this);
                return;
            }

            _waves = StartCoroutine(WaveLoop());
        }

        private void OnDestroy()
        {
            if (_waves != null) StopCoroutine(_waves);
        }

        private IEnumerator WaveLoop()
        {
            yield return new WaitForSeconds(firstWaveDelay);

            while (NetworkServer.active)
            {
                CleanupAlive();

                var desiredCount = Mathf.Min(maxAlive, count + _waveIndex * addPerWave);
                var spawnCount = Mathf.Max(0, desiredCount - _alive.Count);
                for (int i = 0; i < spawnCount; i++)
                {
                    Spawn(i, _waveIndex);
                }

                Debug.Log($"[Steading] Enemy wave {_waveIndex + 1}: spawned {spawnCount}, alive {_alive.Count}/{maxAlive}.");
                _waveIndex++;
                yield return new WaitForSeconds(waveInterval);
            }
        }

        private void CleanupAlive()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null) _alive.RemoveAt(i);
            }
        }

        private void Spawn(int index, int waveIndex)
        {
            Vector3 basePos;
            if (spawnFromForts && EnemyFortLayout.Count > 0)
            {
                basePos = EnemyFortLayout.GetSpawnPoint(waveIndex, index, fortSpawnRadius);
            }
            else
            {
                var angle = (index / (float)Mathf.Max(1, count) + waveIndex * 0.137f) * Mathf.PI * 2f;
                var spawnRadius = radius + Mathf.Min(12f, waveIndex * 1.4f);
                basePos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            }

            if (NavMesh.SamplePosition(basePos, out var hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                basePos = hit.position;
            }

            var instance = Instantiate(enemyPrefab, basePos, Quaternion.identity);
            ScaleDifficulty(instance, waveIndex);
            NetworkServer.Spawn(instance);
            _alive.Add(instance);
        }

        private void ScaleDifficulty(GameObject instance, int waveIndex)
        {
            var health = instance.GetComponent<Health>();
            if (health != null)
            {
                var extra = Mathf.RoundToInt(waveIndex * difficultyHealthPerWave);
                if (extra > 0) health.SetMaxHpRuntime(health.MaxHp + extra, refill: true);
            }

            var enemy = instance.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.ApplyWaveDifficulty(waveIndex, difficultyDamagePerWave);
            }
        }
    }
}
