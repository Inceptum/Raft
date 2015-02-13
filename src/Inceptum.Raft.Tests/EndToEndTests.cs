using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    class StateMachine
    {
        private readonly Action m_BeforeApply;

        public StateMachine():this(() => { })
        {
        }

        public StateMachine(Action beforeApply)
        {
            m_BeforeApply = beforeApply;
        }

        public int Value { get; set; }
        public void Apply(int command)
        {
            m_BeforeApply();
            Value += command;
        }
    }
    [TestFixture]
    public class EndToEndTests
    {
        private readonly List<string> m_KnownNodes = new List<string>(Enumerable.Range(1, 5).Select(i => "node" + i));



      

        [Test]
        public void ConsensusIsReachableWithin5ElectionTimeoutsTest()
        {
            const int electionTimeout = 150;
            var nodes = m_KnownNodes.Select(
                id => new Node(new InMemoryPersistentState(),
                                    new NodeConfiguration(id, m_KnownNodes.ToArray()) { ElectionTimeout = electionTimeout },
                                    new InMemoryTransport(id), () => new StateMachine()))
                .ToList();
            nodes.ForEach(n => n.Start());

            Thread.Sleep(electionTimeout * 5);
            var nodeStates = nodes.Select(node => new { node.Id, node.State, node.LeaderId, node.Configuration }).ToArray();
            foreach (var node in nodes)
            {
                node.Dispose();
            }

            Assert.That(nodeStates.Count(n => n.State == NodeState.Leader), Is.LessThan(2), "There are more then one Leader after election");
            Assert.That(nodeStates.Count(n => n.State == NodeState.Leader), Is.GreaterThan(0), "There is no Leader after election");
            Assert.That(nodeStates.Count(n => n.State == NodeState.Candidate), Is.EqualTo(0), "There are Candidates  after election");
            Assert.That(nodes.Select(n => n.CurrentTerm).Distinct().Count(), Is.EqualTo(1), "Tearm is not the same for all nodes");
            var term = nodes.Select(n => n.CurrentTerm).First();
            Assert.That(term, Is.LessThan(10), "Term is more then 10");
            Assert.That(nodeStates.Select(n => n.LeaderId).Distinct().Count(), Is.EqualTo(1), "LeaderId is not the same for all nodes");
        }

        [Test]
        public void CommandApplyAwaitsForStateMachineToProcessCommandTest()
        {
            //TDOD: not sture if it is right logic - SM holds state in memory , so it makes no sence to finish command processing on dispose since SM would be disposed right after it and state would be lost
            const int electionTimeout = 150;
            var canApply = m_KnownNodes.ToDictionary(k => k, v => new ManualResetEvent(false));
            var stateMachines = m_KnownNodes.ToDictionary(k => k, v => new StateMachine(() =>{canApply[v].WaitOne();}));
            var nodes = m_KnownNodes.Select(
                id =>
                    new Node(new InMemoryPersistentState(), new NodeConfiguration(id, m_KnownNodes.ToArray()) {ElectionTimeout = electionTimeout},
                        new InMemoryTransport(id), () => stateMachines[id]))
                .ToList();
            nodes.ForEach(n => n.Start());

            Thread.Sleep(electionTimeout * 5);
            var leader = nodes.First(n => n.State == NodeState.Leader);

            var exited=new ManualResetEvent(false);
            Task.Factory.StartNew(() => {
                leader.Apply(10);
                exited.Set();
            });
            Assert.That(exited.WaitOne(500),Is.False);
            nodes.ForEach(n => canApply[n.Id].Set());
            Assert.That(exited.WaitOne(500), Is.True);

            nodes.ForEach(n => n.Dispose());
        }


        [Test]
        public void RestoredFollowerGetsAllMissedLogEntriesTest()
        {
            const int electionTimeout = 150;
            var bus = new InMemoryBus();
            var stateMachines = m_KnownNodes.ToDictionary(k=>k,v=>new StateMachine());
            var nodes = m_KnownNodes.Select(
                id =>
                    new Node(new InMemoryPersistentState(), new NodeConfiguration(id, m_KnownNodes.ToArray()) {ElectionTimeout = electionTimeout},
                        new InMemoryTransport(id, bus), () => stateMachines[id]))
                .ToList();
            nodes.ForEach(n => n.Start());


            Thread.Sleep(electionTimeout*5);
            var follower = nodes.First(n => n.State == NodeState.Follower);
            Console.WriteLine("Failing the follower " + follower.Id);
            bus.EmulateConnectivityIssue(follower.Id);
            Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Follower).LeaderId);
 
            var exleader = nodes.First(n => n.State == NodeState.Leader);
            exleader.Apply(1);
            Thread.Sleep(electionTimeout*2);
            exleader.Apply(2);
            Thread.Sleep(electionTimeout*2);
            exleader.Apply(3);
 
            Thread.Sleep(electionTimeout*10);
            Console.WriteLine("Failing the leader " + exleader.Id);
            bus.EmulateConnectivityIssue(exleader.Id);
            Thread.Sleep(electionTimeout*5);
            Console.WriteLine("Restoring exleader " + exleader.Id);
            bus.RestoreConnectivity(exleader.Id);
            Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Follower).LeaderId);
            Thread.Sleep(electionTimeout * 5);
            Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Follower).LeaderId);
            Console.WriteLine("Restoring follower " + follower.Id);
            bus.RestoreConnectivity(follower.Id);
            Thread.Sleep(electionTimeout*10);


            nodes.ForEach(n => n.Dispose());
            var states = stateMachines.Values.Select(m=>m.Value);
            Assert.That(states,Is.EqualTo(m_KnownNodes.Select(n=>6)),"Nodes have wrong states applied by state machines");
            Console.WriteLine(states.First());
            Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Follower).LeaderId);
            Assert.That(follower.LogEntries.Count(), Is.EqualTo(3), "Missed log entries were not replicated");
        }

        [Test]
        public void LongStateMachineCommandProcessingTimeTest()
        {
            const int electionTimeout = 150;
            var stateMachines = m_KnownNodes.ToDictionary(k => k, v => new StateMachine(() => {Thread.Sleep(2000); }));
            var nodes = m_KnownNodes.Select(
                id =>
                    new Node(new InMemoryPersistentState(), new NodeConfiguration(id, m_KnownNodes.ToArray()) { ElectionTimeout = electionTimeout },
                        new InMemoryTransport(id), () => stateMachines[id]))
                .ToList();
            nodes.ForEach(n => n.Start());
            Thread.Sleep(electionTimeout * 5);
            var leader = nodes.First(n => n.State == NodeState.Leader);

            leader.Apply(1);
            leader.Apply(2);
            leader.Apply(3);


            Thread.Sleep(2000);


            nodes.ForEach(n => n.Dispose());
            var states = stateMachines.Values.Select(m=>m.Value);
            Assert.That(states,Is.EqualTo(m_KnownNodes.Select(n=>6)),"Nodes have wrong states applied by state machines");
            Console.WriteLine(states.First());
            Console.WriteLine("Leader is: " + nodes.First(n => n.State == NodeState.Follower).LeaderId);
        }
    }
}
