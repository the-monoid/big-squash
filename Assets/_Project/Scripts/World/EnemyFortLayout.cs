using UnityEngine;

namespace Steading.World
{
    public static class EnemyFortLayout
    {
        private static readonly Vector2[] Centers =
        {
            new Vector2(29f, 24f),
            new Vector2(-34f, 18f),
            new Vector2(22f, -35f)
        };

        public static int Count => Centers.Length;

        public static Vector3 GetCenter(int index)
        {
            var center = Centers[Mathf.Abs(index) % Centers.Length];
            return new Vector3(center.x, 0f, center.y);
        }

        public static Vector3 GetSpawnPoint(int waveIndex, int enemyIndex, float radius)
        {
            var center = GetCenter(waveIndex + enemyIndex);
            var angle = (enemyIndex * 0.37f + waveIndex * 0.19f) * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return center + offset;
        }
    }
}
