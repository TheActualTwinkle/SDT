﻿using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace SDT;

/// <summary>
/// Handler of SnaP servers.
/// </summary>
public class ServersHandler(string address, int port)
{
    private const uint BufferSize = 512;
    
    private readonly Semaphore _clientsListSemaphore = new(1, 1);

    public const string GetStatusCommand = "get-status";
    public const string GetStatusCommandResponse = "ok";

    public const string CloseCommand = "close";

    // Local list of connected SnaP Servers.
    private readonly List<Guid> _servers = [];
    
    private TcpListener? _server;

    public async void Start()
    { 
        try
        {
            IPAddress ipAddress = IPAddress.Parse(address);

            // TcpListener is used to wait for a connection from a client.
            _server = new TcpListener(ipAddress, port);

            // Start listening for client requests.
            _server.Start();

            Console.WriteLine($"[SH] Server for SnaP SERVERS started at {address}:{port}. Waiting for connections...");

            while (true)
            {
                // Blocks until a client has connected to the server.
                TcpClient client = await _server.AcceptTcpClientAsync();

                Task.Run(() => Handle(client));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[SH] Exception: " + e.Message);
        }
        finally
        {
            // Stop listening for new clients.
            Stop();
        }
        
        Console.WriteLine("[SH] Server closing...");
    }
    
    private async void Handle(object? obj)
    {
        Guid guid = Guid.NewGuid();

        TcpClient tcpClient;
        try
        {
            tcpClient = (TcpClient)obj!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[SH/{guid}] Error on parse obj to TcpClient. {e}");
            throw;
        }
        
        NetworkStream clientStream = tcpClient.GetStream();

        Console.WriteLine($"[SH/{guid}] Client connected!");
        
        _clientsListSemaphore.WaitOne();
        _servers.Add(guid);
        _clientsListSemaphore.Release();
        
        // Buffer to store the response bytes.
        var message = new byte[BufferSize];

        LobbyInfo lobbyInfo = new(string.Empty, 0, 0, 0, "Initializing...");

        while (true)
        {
            int bytesRead;

            // Read the incoming message. Expecting json lobby info.
            try
            {
                // Read the incoming message.
                bytesRead = await clientStream.ReadAsync(message.AsMemory(0, (int)BufferSize));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                break;
            }
            
            if (bytesRead <= 0)
            {
                Console.WriteLine($"[SH/{guid}] Client closed connection.");
                break;
            }

            // Convert bytes to a string and print it.
            string clientMessage = Encoding.ASCII.GetString(message, 0, bytesRead);
            clientMessage = clientMessage.Replace("\n", string.Empty);
            clientMessage = clientMessage.Replace("\r", string.Empty);
            
            Console.WriteLine($"[SH/{guid}] Received: {clientMessage}");

            if (clientMessage == CloseCommand)
            {
                break;
            }

            if (clientMessage == GetStatusCommand)
            {
                await SendStatusAsync(tcpClient.GetStream());
                continue;
            }
            
            // Parsing json to LobbyInfo.
            try
            {
                LobbyInfo lobbyInfoCopy = JsonConvert.DeserializeObject<LobbyInfo>(clientMessage)!;
                lobbyInfoCopy.Deconstruct(ref lobbyInfo); // Update info.
            }
            catch (Exception e)
            {
                Console.WriteLine($"[SH/{guid}] Can`t deserialize json to LobbyInfo. " + e);
                continue;
            }

            if (Program.LobbyInfos.ContainsKey(guid) == true)
            {
                continue;
            }
            
            Console.WriteLine($"[SH/{guid}] Added new lobby info.");
            Program.LobbyInfos.TryAdd(guid, lobbyInfo);
        }

        Program.LobbyInfos.TryRemove(guid, out _);
        
        _clientsListSemaphore.WaitOne();
        _servers.Remove(guid);
        _clientsListSemaphore.Release();
        
        Console.WriteLine($"[SH/{guid}] Closing connection.");
        tcpClient.Close();
    }

    public void Stop()
    {
        _server?.Stop();
    }
    
    public bool HasServers()
    {
        _clientsListSemaphore.WaitOne();
        bool any = _servers.Count != 0;
        _clientsListSemaphore.Release();

        return any;
    }
    
    private async Task SendStatusAsync(NetworkStream clientStream)
    {
        byte[] response = Encoding.ASCII.GetBytes(GetStatusCommandResponse);
        await clientStream.WriteAsync(response.AsMemory(0, response.Length));
    }
}