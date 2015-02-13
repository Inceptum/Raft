/*using Inceptum.Raft.Http;
using Inceptum.Raft.Logging;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class FluentConfigurationTests
    {
        [Test]
        public void Test()
        {
            var configurator = new Configurator(new NodeConfiguration());
            configurator
                .Named("node1")
                .ElectionTimeout(300)
                .WithHttpCluster()
                    .Node("node2", "http://localhost:1234")
                    .Node("node3", "http://localhost:1234")
                .WithLoggerFactory(type => new ConsoleLogger(type.Name))
                .WithTransport(new HttpTransport())
                .WithStateMachine(new object());
        }
    }
}*/