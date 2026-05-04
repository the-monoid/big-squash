using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Steading.Net
{
#if MIRROR
    public class NetworkBootstrap : NetworkManager
    {
        [Header("Steading")]
        [SerializeField] private int defaultTickRate = 30;
        [SerializeField] private ushort defaultPort = 7777;

        public override void Awake()
        {
            base.Awake();
            ApplyCommandLineOverrides();
        }

        private void ApplyCommandLineOverrides()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-port" when ushort.TryParse(args[i + 1], out var p):
                        SetTransportPort(p);
                        break;
                    case "-maxplayers" when int.TryParse(args[i + 1], out var mp):
                        maxConnections = mp;
                        break;
                    case "-tickrate" when int.TryParse(args[i + 1], out var tr):
                        sendRate = tr;
                        break;
                }
            }
        }

        private void SetTransportPort(ushort port)
        {
            if (transport is kcp2k.KcpTransport kcp)
            {
                kcp.Port = port;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[Steading.Net] Server started on port {defaultPort}, tickrate {sendRate}Hz, max {maxConnections} players");
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[Steading.Net] Client connected: {conn.address}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Steading.Net] Client disconnected: {conn.address}");
            base.OnServerDisconnect(conn);
        }
    }
#else
    // Mirror is not yet imported. Once you install Mirror via Package Manager,
    // the MIRROR define is auto-added and this class is replaced by the real
    // implementation above. This stub exists so the project compiles before
    // Mirror is installed.
    public class NetworkBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[Steading.Net] Mirror not installed; NetworkBootstrap is inactive. " +
                             "Install Mirror via Package Manager to enable networking.");
        }
    }
#endif
}
