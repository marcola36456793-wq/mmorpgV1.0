using UnityEngine;
using NativeWebSocket;
using System;
using System.Threading.Tasks;

public class ClientManager : MonoBehaviour
{
    public static ClientManager Instance { get; private set; }

    private WebSocket websocket;
    public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;
    
    public string PlayerId { get; private set; }

    public event Action<string> OnMessageReceived;

    private bool isClosing = false;
    private bool isQuitting = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async void Connect(string url = "ws://25.22.58.214:8080/game")
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            Debug.LogWarning("Already connected to server!");
            return;
        }

        try
        {
            websocket = new WebSocket(url);

            websocket.OnOpen += () =>
            {
                Debug.Log("✅ Connected to server!");
                isClosing = false;
            };

            websocket.OnError += (e) =>
            {
                Debug.LogError($"❌ WebSocket Error: {e}");
            };

            websocket.OnClose += (e) =>
            {
                Debug.Log($"🔌 Connection closed: {e}");
                isClosing = false;
            };

            websocket.OnMessage += (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log($"📨 Received: {message.Substring(0, Math.Min(100, message.Length))}...");
                
                OnMessageReceived?.Invoke(message);
            };

            await websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Connection failed: {e.Message}");
        }
    }

    // ✅ CORREÇÃO: Usa 'new' para esconder SendMessage herdado
    public new async void SendMessage(string message)
    {
        if (websocket == null)
        {
            Debug.LogWarning("⚠️ WebSocket is null!");
            return;
        }

        if (isClosing || isQuitting)
        {
            Debug.LogWarning("⚠️ WebSocket is closing, message not sent");
            return;
        }

        if (websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning($"⚠️ WebSocket is not open (State: {websocket.State})");
            return;
        }

        try
        {
            await websocket.SendText(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error sending message: {e.Message}");
        }
    }

    private void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null && !isClosing && !isQuitting)
        {
            try
            {
                websocket.DispatchMessageQueue();
            }
            catch (Exception e)
            {
                if (!isQuitting)
                {
                    Debug.LogError($"❌ Error dispatching messages: {e.Message}");
                }
            }
        }
        #endif
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        CloseConnection();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            CloseConnection();
        }
    }

    private void CloseConnection()
    {
        if (websocket == null || isClosing)
        {
            return;
        }

        isClosing = true;

        try
        {
            if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.Connecting)
            {
                Debug.Log("🔌 Closing WebSocket connection...");
                
                Task.Run(async () =>
                {
                    try
                    {
                        await websocket.Close();
                        Debug.Log("✅ WebSocket closed successfully");
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"⚠️ Error closing WebSocket (ignored): {e.Message}");
                    }
                }).Wait(1000);
            }
            else
            {
                Debug.Log($"⚠️ WebSocket already closed (State: {websocket.State})");
            }
        }
        catch (Exception e)
        {
            Debug.Log($"⚠️ Exception during close (ignored): {e.Message}");
        }
        finally
        {
            websocket = null;
        }
    }

    public void Disconnect()
    {
        CloseConnection();
    }

    public void SetPlayerId(string id)
    {
        PlayerId = id;
        Debug.Log($"🆔 Player ID set: {id.Substring(0, Math.Min(8, id.Length))}...");
    }

    public async void Reconnect(string url = "ws://localhost:8080/game")
    {
        Debug.Log("🔄 Reconnecting...");
        
        CloseConnection();
        await Task.Delay(500);
        Connect(url);
    }

    public bool IsHealthy()
    {
        return websocket != null && 
               websocket.State == WebSocketState.Open && 
               !isClosing && 
               !isQuitting;
    }
}