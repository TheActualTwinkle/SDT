﻿using System.Net;
using System.Net.Sockets;
using SLS.TcpIp;
using SLS.TcpIp.Commands;

namespace SLS.Tests.TcpIp;

public class ServerTests
{
    private ServersHandler? _serversHandler;
    
    private NetworkStream NetworkStream => _tcpClient.GetStream();
    private TcpClient _tcpClient;

    private const ushort Port = 47920;

    [SetUp]
    public async Task Setup()
    {
        _serversHandler = new ServersHandler(IPAddress.Parse("127.0.0.1"), Port);
#pragma warning disable CS4014
        _serversHandler.Run();        
#pragma warning restore CS4014
        
        _tcpClient = await Tools.Connect(IPAddress.Parse("127.0.0.1"), Port);
    }
    
    [Test]
    public void Connect()
    {
        Assert.That(_tcpClient.Connected == true && _serversHandler?.HasServers() == true, Is.True);
    }
    
    // Handle Close command.
    [Test]
    public async Task Disconnect()
    {
        await Tools.WriteCommandAsync(new Command(CommandType.Close), NetworkStream);
        
        Assert.That(_serversHandler?.HasServers(), Is.False);
    }

    [Test]
    public async Task DropConnection()
    {
        await Tools.Disconnect(_tcpClient);
        
        Assert.That(_serversHandler?.HasServers(), Is.False);
    }
    
    [Test]
    public async Task GetStatus()
    {
        await Tools.WriteCommandAsync(new Command(CommandType.GetStatus), NetworkStream);
        
        string response = await Tools.ReadAsync(NetworkStream, new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

        if (_serversHandler?.HasServers() == false)
        {
            Assert.Fail();
        }

        if (Program.LobbyInfos.IsEmpty == false)
        {
            Assert.Fail();
        }
        
        Assert.That(response, Is.EqualTo(ServersHandler.GetStatusSuccessResponse));
    }
    
    [Test]
    public async Task UnknownCommand()
    {
        await Tools.WriteCommandAsync(new Command(), NetworkStream);
        
        string response = await Tools.ReadAsync(NetworkStream, new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        
        Assert.That(response, Is.EqualTo(ServersHandler.UnknownCommandResponse));
    }
    
    [Test]
    public async Task PostLobbyInfo_LobbyInfoAsJson_LobbyInfoArrayContainsEntry()
    {
        LobbyDto randomLobbyDto = await PostRandomLobbyInfo();

        LobbyDto lobbyDto = Program.LobbyInfos.Values.First();

        Assert.That(Tools.LobbyInfoValuesEquals(lobbyDto, randomLobbyDto), Is.True);
    }

    [Test]
    public async Task PostLobbyInfo_CorruptedLobbyInfo_ClientDropped()
    {
        await Tools.WriteCommandAsync(new Command(CommandType.PostLobbyInfo, "corrupted...lobby/info"), NetworkStream);
        
        Assert.That(Program.LobbyInfos.IsEmpty && _serversHandler!.HasServers() == false, Is.True);
    }

    [Test]
    public async Task EditLobbyInfo_LobbyInfoAsJson_LobbyInfoArrayChangesEntry()
    {
        LobbyDto randomLobbyInfo1 = await PostRandomLobbyInfo();

        LobbyDto lobbyDto = Program.LobbyInfos.Values.First();

        if (Tools.LobbyInfoValuesEquals(lobbyDto, randomLobbyInfo1) == false)
        {
            Assert.Fail();
        }

        LobbyDto randomLobbyInfo2 = await PostRandomLobbyInfo();

        Assert.That(Tools.LobbyInfoValuesEquals(lobbyDto, randomLobbyInfo2), Is.True);
    }
    
    [Test]
    public async Task UnsupportedCommand()
    {
        await Tools.WriteCommandAsync(new Command(CommandType.GetLobbyGuids), NetworkStream);
        
        Assert.That(_serversHandler!.HasServers() == true && Program.LobbyInfos.IsEmpty, Is.True);
    }

    [TearDown]
    public async Task Cleanup()
    {
        _serversHandler?.Stop();
        Program.LobbyInfos.Clear();
        await Tools.Disconnect(_tcpClient);
    }

    private async Task<LobbyDto> PostRandomLobbyInfo()
    {
        LobbyDto randomLobbyDto = Tools.GetRandomLobbyInfo();
        await Tools.WriteCommandAsync(new Command(CommandType.PostLobbyInfo, randomLobbyDto), NetworkStream);
        return randomLobbyDto;
    }
}