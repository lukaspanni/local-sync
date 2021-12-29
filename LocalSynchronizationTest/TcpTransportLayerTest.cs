using LocalSynchronization;
using Moq;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LocalSynchronizationTest
{
    public class TcpTransportLayerTest
    {
        [Fact]
        public void ConstructWithNotConnectedClientThrowsError()
        {
            var clientMock = new Mock<ITcpClient>();
            clientMock.SetupGet(x => x.Connected).Returns(false);
            Assert.Throws<ArgumentException>(() => new TcpTransportLayer(clientMock.Object));
        }

        [Fact]
        public void SendMessageTest()
        {
            ReadOnlyMemory<byte> calledMemory = new();

            var clientMock = new Mock<ITcpClient>();
            clientMock.SetupGet(client => client.Connected).Returns(true);
            clientMock.Setup(client => client.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Callback<ReadOnlyMemory<byte>, CancellationToken>((memory, token) => calledMemory = memory)
                .Returns(ValueTask.CompletedTask);
            Assert.True(clientMock.Object.Connected);
            var transportLayer = new TcpTransportLayer(clientMock.Object);

            var message = new TransportLayerMessage(MessageType.Standard, 4, new ReadOnlyMemory<byte>(new byte[] { 0, 1, 2, 3 }));
            transportLayer.SendMessage(message).Wait();

            var expectedBytes = new byte[9] { 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03 };

            Assert.Equal(expectedBytes, calledMemory.ToArray());
        }

        [Fact]
        public void ReceiveMessageUnfragmentedTest()
        {
            var receiveBytes = new byte[9] { 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03 };
            var clientMock = new Mock<ITcpClient>();
            clientMock.SetupGet(client => client.Connected).Returns(true);
            clientMock.Setup(client => client.ReceiveAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((readBuffer, _, _, _) => receiveBytes.CopyTo(readBuffer, 0))
                .Returns(Task.FromResult(9));
            var transportLayer = new TcpTransportLayer(clientMock.Object);

            TransportLayerMessage message = transportLayer.ReceiveMessage().GetAwaiter().GetResult();

            var expectedMessage = new TransportLayerMessage(MessageType.Standard, 4, new ReadOnlyMemory<byte>(new byte[] { 0, 1, 2, 3 }));

            Assert.Equal(expectedMessage.StartByte, message.StartByte);
            Assert.Equal(expectedMessage.Length, message.Length);
            Assert.Equal(expectedMessage.Payload.ToArray(), message.Payload.ToArray());

        }

        [Fact]
        public void ReceiveMessageFragmentedTest()
        {
            var dataBytes = new byte[2048];
            for (int i = 0; i < dataBytes.Length; i++)
            {
                dataBytes[i] = (byte)i;
            }
            var receiveBytes = new byte[2053];
            receiveBytes[0] = 0x01;
            BitConverter.GetBytes(2048).CopyTo(receiveBytes, 1);
            dataBytes.CopyTo(receiveBytes, 5);

            var clientMock = new Mock<ITcpClient>();
            clientMock.SetupGet(client => client.Connected).Returns(true);
            var callCount = 0;
            clientMock.Setup(client => client.ReceiveAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((readBuffer, _, _, _) =>
                {
                    Array.Copy(receiveBytes, callCount == 0 ? 0 : 2048, readBuffer, 0, callCount == 0? 2048 : 5);
                    callCount++;
                }).Returns(Task.FromResult(9));
            var transportLayer = new TcpTransportLayer(clientMock.Object);

            TransportLayerMessage message = transportLayer.ReceiveMessage().GetAwaiter().GetResult();

            var expectedMessage = new TransportLayerMessage(MessageType.Standard, 2048, new ReadOnlyMemory<byte>(dataBytes));

            Assert.Equal(expectedMessage.StartByte, message.StartByte);
            Assert.Equal(expectedMessage.Length, message.Length);
            Assert.Equal(expectedMessage.Payload.ToArray(), message.Payload.ToArray());
        }


    }
}