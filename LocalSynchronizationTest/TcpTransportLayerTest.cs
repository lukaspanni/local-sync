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
        public void SendMessageSerializationTest()
        {
            ReadOnlyMemory<byte> calledMemory = new();

            var clientMock = new Mock<ITcpClient>();
            clientMock.SetupGet(client => client.Connected).Returns(true);
            clientMock.Setup(streamMock => streamMock.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Callback<ReadOnlyMemory<byte>, CancellationToken>((memory, token) => calledMemory = memory)
                .Returns(ValueTask.CompletedTask);
            Assert.True(clientMock.Object.Connected);
            var transportLayer = new TcpTransportLayer(clientMock.Object);

            var message = new TransportLayerMessage(0x01, 4, new ReadOnlyMemory<byte>(new byte[] { 0, 1, 2, 3 }));
            transportLayer.SendMessage(message).Wait();

            var expectedBytes = new byte[9] { 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03 };

            Assert.Equal(expectedBytes, calledMemory.ToArray());
        }


    }
}