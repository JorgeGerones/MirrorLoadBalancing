using LiteNetLib;
using LiteNetLib.Utils;
using LoggerEngine;
using Mirror;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Events;

namespace Shared.Network 
{
    [System.Serializable]
    public class Client : INetEventListener
    {
        [SerializeField] private NetManager _client;
        [SerializeField] private NetPeer _serverPeer;

        public bool connected = false;

        public void Connect(string address, int port)
        {
            _client = new NetManager(this);
            _client.Start();

            _serverPeer = _client.Connect(address, port, "ClaveDeConexion");
            if (_serverPeer != null)
            {
                DebugServer.Log($"Succesfully connected NetLib client to {address}", ConsoleColor.DarkYellow);
            }
            else
            {
                DebugServer.LogError($"Error on connecting client to NetLibServer on {address}");
            }
        }

        public void Disconnect()
        {
            _client.Stop();
            _client = null;

            DebugServer.Log($"Succesfully disconnected NetLib client", ConsoleColor.DarkYellow);
        }

        public void SendMessage(string message)
        {
            var writer = new NetDataWriter();
            writer.Put(message);
            _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
            DebugServer.LogError($"Sending message: " + message);
        }

        public void OnPeerConnected(NetPeer peer)
        {
            DebugServer.Log($"Cliente conectado: {peer}", ConsoleColor.DarkYellow);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            DebugServer.Log($"Cliente desconectado: {peer.Id}. Razón: {disconnectInfo.Reason}", ConsoleColor.DarkYellow);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            DebugServer.LogError($"Error de red en {endPoint}: {socketError}");
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            DebugServer.Log($"Mensaje no conectado recibido de {remoteEndPoint}: {reader.GetString()}", ConsoleColor.DarkYellow);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            //DebugServer.Log($"Actualización de latencia para {peer.Id}: {latency}ms", ConsoleColor.DarkYellow);
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            DebugServer.Log($"Solicitud de conexión desde {request.RemoteEndPoint}", ConsoleColor.DarkYellow);
            request.AcceptIfKey("ClaveDeConexion");
        }

        public UnityEvent<string> OnDataReceive = new UnityEvent<string>();

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            string mensaje = reader.GetString();
            DebugServer.Log($"Mensaje recibido del servidor: {mensaje}", ConsoleColor.DarkYellow);
            OnDataReceive?.Invoke(mensaje);
        }

        public void Update()
        {
            _client?.PollEvents();
            connected = _client != null;
        }
        // Implementar métodos de INetEventListener...
    }

    [System.Serializable]
    public struct MasterServerMessage
    {
        public enum MasterServerOperation 
        {
            None,
            Create, 
            CreateJoin,
            Join,
            Leave,
            Cancel,
        }

        public MasterServerOperation operation;

        public Guid matchId;
        public string type;
        public int userId;
    }

    [System.Serializable]
    public struct IdentifierMessage
    {
        public string messageType;
        public bool master;
        public int port;
        public int maxConn;
    }

    [System.Serializable]
    public struct ConnectionsMessage
    {
        public string messageType;
        public string address;
        public int port;
        public int maxConn;
        public int actualConn;
    }

    public class ServerCom : MonoBehaviour
    {
        public Client client;

        private void Awake()
        {
            client = new Client();
            client.Connect("localhost", 7777);

            DebugServer.Log($"is Connected: {client.connected}");
        }

        private void Update()
        {
            if (client != null)
            {
                client.Update();
            }
        }

       

        public void AddServer() 
        {
            var maxC = FindObjectOfType<NetworkManager>().maxConnections;
            var transport = (TelepathyTransport)FindObjectOfType<NetworkManager>().transport;

            var msg = new IdentifierMessage();
            msg.master = true;
            msg.messageType = msg.GetType().ToString();
            msg.port = transport.Port;
            msg.maxConn = maxC;
            

            var json = JsonUtility.ToJson(msg);

            client.SendMessage(json);
        }

        public void SendClientMessage(NetworkConnectionToClient conn) 
        {
            var transport = (TelepathyTransport)FindObjectOfType<NetworkManager>().transport;

            ConnectionsMessage msg = new ConnectionsMessage();
            msg.messageType = msg.GetType().ToString();
            msg.maxConn = FindObjectOfType<NetworkManager>().maxConnections;
            msg.actualConn = NetworkServer.connections.Count;
            msg.port = transport.Port;

            var connData = JsonUtility.ToJson(msg);

            client.SendMessage(connData);
        }

        public void Create(Guid matchId, int userId) 
        {
            MasterServerMessage message = new MasterServerMessage();
            message.type = "MasterServerMessage";
            message.matchId = matchId;
            message.operation = MasterServerMessage.MasterServerOperation.Create;
            message.userId = userId;

            var data = JsonUtility.ToJson(message);

            client.SendMessage(data);

            DebugServer.Log("Sending Message");
        }
    }

}
