﻿using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class SynchronizationClient : IDisposable
{

    private const int startByte = 0x01;
    private Keystore clientKeystore;
    private ITcpClient? tcpClient;
    private ITransportLayer? transportLayer;
    private X509Certificate2 certificate;


    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public byte[] PublicKeyBytes => certificate.Export(X509ContentType.Cert);

    public SynchronizationClient(string ipString, int port) : this(ipString, port, new Keystore()) { }

    internal SynchronizationClient(string ipString, int port, Keystore keystore)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;
        clientKeystore = keystore;

        certificate = clientKeystore.GetCertificateByCommonName("testclient");
    }

    public void ImportServerCertificate(string base64EncodedCertificate)
    {
        clientKeystore.SetAcceptedRemoteCertificate(base64EncodedCertificate);
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(clientKeystore.AcceptedCertificateHost);
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
        transportLayer = new TcpTransportLayer(tcpClient);
    }

    public async Task Send(byte[] dataBuffer)
    {
        if (transportLayer == null) throw new InvalidOperationException();
        var message = new TransportLayerMessage(startByte, dataBuffer.Length, new ReadOnlyMemory<byte>(dataBuffer));
        await transportLayer.SendMessage(message);
    }

    public async Task<TransportLayerMessage> Receive()
    {
        if (transportLayer == null) throw new InvalidOperationException();
        return await transportLayer.ReceiveMessage();
    }



    public void Disconnect()
    {
        tcpClient.Close();
    }

    public void Dispose()
    {
        Disconnect();
        transportLayer?.CancelRunningOperations();
        tcpClient?.Dispose();
    }

    private ITcpClient BuildClient(string targetHost, bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(new TcpClient());
        return new TlsTcpClientAdapter(new TcpClient(), new System.Net.Security.RemoteCertificateValidationCallback(clientKeystore.ValidateServerCertificate), targetHost, certificate);
    }
}

