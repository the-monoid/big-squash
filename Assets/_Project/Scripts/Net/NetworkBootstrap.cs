using kcp2k;
using Mirror;
using UnityEngine;

namespace Steading.Net
{
    public class NetworkBootstrap : NetworkManager
    {
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
            if (transport is KcpTransport kcp)
            {
                kcp.Port = port;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[Steading.Net] Server started, tickrate {sendRate}Hz, max {maxConnections} players");
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
}
