using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Test()
        {
            try
            {
                var inMemoryTransport = new InMemoryTransport();
                var knownNodes = new List<Guid>
                {
                    Guid.Parse("AE34F270-A72B-4D23-9BBE-C660403690E0"),
                    Guid.Parse("DAA588C4-26DD-451F-865C-5591E78994FB"),
                    Guid.Parse("27AFDE7B-FD8E-4F8E-BA42-7DA24F8EE2E5"),
                    Guid.Parse("DEE86807-E9BC-4927-B748-89C8101D826E"),
                    Guid.Parse("1DF25C51-29DD-4A00-AD26-0198B09DA036")
                };

                knownNodes = Enumerable.Range(1, 101).Select(z => Guid.NewGuid()).ToList();

                var nodes = knownNodes.Select(
                    id => new Node(new PersistentState(), new NodeConfiguration(id, knownNodes.ToArray()) {ElectionTimeout = 300}, inMemoryTransport))
                    .ToArray();

                foreach (var node in nodes)
                {
                    node.Start();
                }

                Thread.Sleep(60000);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Node.m_Log);
            }
        }
    }
}
