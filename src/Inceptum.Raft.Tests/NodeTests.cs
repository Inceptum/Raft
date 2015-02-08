using System;
using System.Threading;
using Inceptum.Raft.Rpc;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class NodeTests
    {
        [Test]
        public void NodeStartsAsFollowerTest()
        {
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 10000 };
            var bus = mockTransport();
            using (var node = new Node(new InMemoryPersistentState(), nodeConfiguration, new InMemoryTransport("testedNode", bus), new object()))
            {
                node.Start();
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Node state after start is not follower");
            }
        }


        [Test(Description = "If RPC request or response contains term T > currentTerm: set currentTerm = T, convert to follower (§5.1)")]
        public void UpdateTermFromIncommingMessagesTest()
        {
            using (var node = createFollower(new InMemoryPersistentState(){CurrentTerm = 0}))
            {
                node.Start();
                node.SwitchToLeader();
                node.Handle(new AppendEntriesRequest() { Term = 1 ,LeaderId = "nodeA"});
                Assert.That(node.CurrentTerm, Is.EqualTo(1), "Term was not updated from AppendEntriesRequest");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on AppendEntriesRequest with newer term");
                node.SwitchToLeader();
                node.Handle(new AppendEntriesResponse { Term = 2,NodeId = "nodeA"});
                Assert.That(node.CurrentTerm, Is.EqualTo(2), "Term was not updated from AppendEntriesResponse");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on AppendEntriesResponse with newer term");
                node.SwitchToLeader();
                node.Handle(new VoteRequest { Term = 3, CandidateId = "nodeA" });
                Assert.That(node.CurrentTerm, Is.EqualTo(3), "Term was not updated from VoteRequest");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on VoteRequest with newer term");
                node.SwitchToLeader();
                node.Handle(new VoteResponse { Term = 4, NodeId = "nodeA" });
                Assert.That(node.CurrentTerm, Is.EqualTo(4), "Term was not updated from VoteResponse");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on VoteResponse with newer term");
            }
        }

        private Node createFollower(InMemoryPersistentState persistentState)
        {
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 100000 };
            var bus = mockTransport();
            return new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), new object());

        }


        private static IInMemoryBus mockTransport()
        {
            var transport = MockRepository.GenerateMock<IInMemoryBus>();
            transport.Expect(t => t.Subscribe<VoteRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<VoteResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<ApplyCommadRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            return transport;
        }

    }
}