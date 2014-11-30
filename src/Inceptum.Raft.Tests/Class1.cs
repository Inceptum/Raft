using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    class NodeStateObserver : IObserver<NodeState>
    {
        public long LeaderCount { get; private set; }
        public long FollowerCount { get; private set; }
        public long CandidateCount { get; private set; }
        object m_SyncRoot=new object();

        public void OnNext(NodeState value)
        {
            switch (value)
            {
                case NodeState.Candidate:
                    CandidateCount++;
                    break;
                case NodeState.Leader:
                    LeaderCount++;
                    break;
                case NodeState.Follower:
                    FollowerCount++;
                    break;
            }
        }

        public void OnError(Exception error)
        {
            
        }

        public void OnCompleted()
        {
             
        }
    }

    [TestFixture]
    public class Class1
    {
        private static int counter = 0;

        [Test, Ignore]
        public void GuidBasedRandom1Test()
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                var rndNum = new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8), NumberStyles.HexNumber));
                rndNum.Next(0, 150);
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(sw.ElapsedMilliseconds*1.0/100000);

        }
        [Test, Ignore]
        public void GuidBasedRandom2Test()
        {
           
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                var buf = Guid.NewGuid().ToByteArray();
                var i1 = BitConverter.ToInt32(buf, 4)%150;
                Console.WriteLine(i1);
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(sw.ElapsedMilliseconds*1.0/100000);

        }
        [Test,Ignore]
        public void CryptographyBasedRandomTest()
        {
            var buf = new byte[4];
            var rand = new RNGCryptoServiceProvider(new CspParameters());
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                rand.GetBytes(buf);     
                BitConverter.ToInt32(buf, 0);
            }
            sw.Stop();
            rand.Dispose();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(sw.ElapsedMilliseconds*1.0/100000);



        }

        [Test]
        public void ConsensusIsReachableWithin5ElectionTimeoutsTest()
        {
            const int electionTimeout = 150;
            Node<object>.m_Log.Clear();
            var sb=new StringBuilder();
            try
            {
                 var knownNodes = new List<Guid>
                {
                    Guid.Parse("AE34F270-A72B-4D23-9BBE-C660403690E0"),
                    Guid.Parse("DAA588C4-26DD-451F-865C-5591E78994FB"),
                    Guid.Parse("27AFDE7B-FD8E-4F8E-BA42-7DA24F8EE2E5"),
                    Guid.Parse("DEE86807-E9BC-4927-B748-89C8101D826E"),
                    Guid.Parse("1DF25C51-29DD-4A00-AD26-0198B09DA036")
                };

                 var inMemoryTransport = new InMemoryTransport();

              //     knownNodes = Enumerable.Range(1, 20).Select(z => Guid.NewGuid()).ToList();

                var nodes = knownNodes.Select(
                    id => new Node<int>(new PersistentState<int>(), new NodeConfiguration(id, knownNodes.ToArray()) {ElectionTimeout = electionTimeout},inMemoryTransport))
                    .ToArray();

                var start = DateTime.Now;
                foreach (var node in nodes)
                {
                    node.Start();
                }

                Thread.Sleep(electionTimeout*5);

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

                Assert.That(nodeStates.Count(n => n.State == NodeState.Leader), Is.LessThan(2), "There are more then one Leader after election");
                Assert.That(nodeStates.Count(n => n.State == NodeState.Leader), Is.GreaterThan(0), "There is no Leader after election");
                Assert.That(nodeStates.Count(n => n.State == NodeState.Candidate), Is.EqualTo(0), "There are Candidates  after election");
                Assert.That(nodes.Select(n=>n.CurrentTerm).Distinct().Count(), Is.EqualTo(1), "Tearm is not the same for all nodes");
                var term = nodes.Select(n=>n.CurrentTerm).First();

                Assert.That(term, Is.LessThan(10), "There are Candidates  after election");
                Assert.That(nodeStates.Select(n => n.LeaderId).Distinct().Count(), Is.EqualTo(1), "LeaderId is not the same for all nodes");
                
                //Debug.WriteLine("{0}\t{1}",(nodes.Single(n=>n.State==NodeState.Leader).CurrentStateEnterTime-start).TotalMilliseconds.ToString().Replace(".",","), term);
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine(sb.ToString());
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Node<int>.m_Log);

                throw;
            }

            finally
            {
                Console.WriteLine(Node<int>.m_Log);
            }
        }
        [Test]
        public void RestoredFollowerGetsAllMissedLogEntriesTest()
        {
            const int electionTimeout = 150;
            Node<object>.m_Log.Clear();
            var sb=new StringBuilder();
            try
            {
                 var knownNodes = new List<Guid>
                {
                    Guid.Parse("AE34F270-A72B-4D23-9BBE-C660403690E0"),
                    Guid.Parse("DAA588C4-26DD-451F-865C-5591E78994FB"),
                    Guid.Parse("27AFDE7B-FD8E-4F8E-BA42-7DA24F8EE2E5"),
                    Guid.Parse("DEE86807-E9BC-4927-B748-89C8101D826E"),
                    Guid.Parse("1DF25C51-29DD-4A00-AD26-0198B09DA036")
                };

                var inMemoryTransport = new InMemoryTransport();
 
                var nodes = knownNodes.Select(
                    id => new Node<int>(new PersistentState<int>(), new NodeConfiguration(id, knownNodes.ToArray()) {ElectionTimeout = electionTimeout},inMemoryTransport))
                    .ToArray();

                foreach (var node in nodes)
                {
                    node.Start();
                }

                Thread.Sleep(electionTimeout*5);
                var follower = nodes.First(n=>n.State==NodeState.Follower);
                Console.WriteLine("Failing the follower" + follower.Id);
                inMemoryTransport.EmulateConnectivityIssue(follower.Id);
                Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Leader).Id);

                var exleader = nodes.First(n => n.State == NodeState.Leader);
                exleader.Apply(1);
                exleader.Apply(2);
                exleader.Apply(3);

                Thread.Sleep(electionTimeout * 5);
                Console.WriteLine("Failing the leader " + exleader.Id);
                inMemoryTransport.EmulateConnectivityIssue(exleader.Id);
                Thread.Sleep(electionTimeout * 5);
                Console.WriteLine("Restoring exleader " + exleader.Id);
                inMemoryTransport.RestoreConnectivity(exleader.Id);
                Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Leader).Id);
                Console.WriteLine("Restoring follower " + follower.Id);
                inMemoryTransport.RestoreConnectivity(follower.Id);
                Thread.Sleep(electionTimeout * 5);

                foreach (var node in nodes)
                {
                    node.Dispose();
                }

                Assert.That(follower.LogEntries.Count(), Is.EqualTo(3), "Missed log entries were not replicated");
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine(sb.ToString());
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Node<int>.m_Log);

                throw;
            }

            finally
            {
                Console.WriteLine(Node<int>.m_Log);
            }
        }
    }
}
