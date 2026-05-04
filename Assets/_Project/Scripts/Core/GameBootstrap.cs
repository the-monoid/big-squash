using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steading.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string worldSceneName = "World_Test";

        private static GameBootstrap _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            var mode = DetectLaunchMode();
            Debug.Log($"[Steading] Boot mode: {mode}");

            if (SceneManager.GetActiveScene().name != worldSceneName)
            {
                SceneManager.LoadScene(worldSceneName, LoadSceneMode.Single);
            }
        }

        public enum LaunchMode { Client, Host, DedicatedServer }

        private static LaunchMode DetectLaunchMode()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-server") return LaunchMode.DedicatedServer;
                if (args[i] == "-host") return LaunchMode.Host;
            }
#if UNITY_SERVER
            return LaunchMode.DedicatedServer;
#else
            return LaunchMode.Client;
#endif
        }
    }
}
