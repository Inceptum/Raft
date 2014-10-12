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
        [Repeat(300)]
        public void Test()
        {
            Node<object>.m_Log.Clear();
            var sb=new StringBuilder();
            try
            {
                var inMemoryTransport = new InMemoryTransport<object>();
                var knownNodes = new List<Guid>
                {
                    Guid.Parse("AE34F270-A72B-4D23-9BBE-C660403690E0"),
                    Guid.Parse("DAA588C4-26DD-451F-865C-5591E78994FB"),
                    Guid.Parse("27AFDE7B-FD8E-4F8E-BA42-7DA24F8EE2E5"),
                    Guid.Parse("DEE86807-E9BC-4927-B748-89C8101D826E"),
                    Guid.Parse("1DF25C51-29DD-4A00-AD26-0198B09DA036")
                };

                //   knownNodes = Enumerable.Range(1, 101).Select(z => Guid.NewGuid()).ToList();

                var nodes = knownNodes.Select(
                    id =>
                        new Node<object>(new PersistentState<object>(), new NodeConfiguration(id, knownNodes.ToArray()) {ElectionTimeout = 300},
                            inMemoryTransport))
                    .ToArray();

                foreach (var node in nodes)
                {
                    node.Start();
                }

                Thread.Sleep(3000);

                var nodeStates = nodes.Select(node => new {node.Id, node.State, node.LeaderId, node.Configuration}).ToArray();
                foreach (var node in nodes)
                {
                    node.Dispose();
                }
                foreach (var node in nodeStates)
                {
                    sb.AppendLine(string.Format("{0}: {1}\tLeader:{2}", node.Id, node.State, node.LeaderId));
                    foreach (var knownNode in node.Configuration.KnownNodes)
                    {
                        sb.AppendLine(string.Format("\t{0}", knownNode));
                    }
                }

                Assert.That(nodeStates.Count(n => n.State == "Leader"), Is.LessThan(2), "There are more then one Leader after election");
                Assert.That(nodeStates.Count(n => n.State == "Leader"), Is.GreaterThan(0), "There is no Leader after election");
                Assert.That(nodeStates.Count(n => n.State == "Candidate"), Is.EqualTo(0), "There are Candidates  after election");
                Assert.That(nodeStates.Select(n => n.LeaderId).Distinct().Count(), Is.EqualTo(1), "LeaderId is not the same for all nodes");
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine(sb.ToString());
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Node<object>.m_Log);

                throw;
            }

            finally
            {
                         /*       Console.WriteLine(".");
Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Node<object>.m_Log);*/
            }
        }
    }
}
