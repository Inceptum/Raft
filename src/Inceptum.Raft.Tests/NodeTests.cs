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
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodaA", "nodaB") { ElectionTimeout = 10000 };
            var transport = mockTransport();
            var stateMachine = MockRepository.GenerateMock<IStateMachine<int>>();
            using (var node = new Node<int>(new PersistentState<int>(), nodeConfiguration, transport, stateMachine))
            {
                node.Start();
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Node state after start is not follower");
            }
        }


        [Test(Description = "If RPC request or response contains term T > currentTerm: set currentTerm = T, convert to follower (§5.1)")]
        public void UpdateTermFromIncommingMessagesTest()
        {
            using (var node = createFollower(new PersistentState<int>(){CurrentTerm = 0}))
            {
                node.Start();
                node.SwitchToLeader();
                node.Handle(new AppendEntriesRequest<int>() { Term = 1 });
                Assert.That(node.CurrentTerm, Is.EqualTo(1), "Term was not updated from AppendEntriesRequest");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on AppendEntriesRequest with newer term");
                node.SwitchToLeader();
                node.Handle(new AppendEntriesResponse { Term = 2 });
                Assert.That(node.CurrentTerm, Is.EqualTo(2), "Term was not updated from AppendEntriesResponse");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on AppendEntriesResponse with newer term");
                node.SwitchToLeader();
                node.Handle(new VoteRequest { Term = 3 });
                Assert.That(node.CurrentTerm, Is.EqualTo(3), "Term was not updated from VoteRequest");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on VoteRequest with newer term");
                node.SwitchToLeader();
                node.Handle(new VoteResponse{ Term = 4 });
                Assert.That(node.CurrentTerm, Is.EqualTo(4), "Term was not updated from VoteResponse");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "Has not converted to Follower on VoteResponse with newer term");
            }
        }

        private Node<int> createFollower(PersistentState<int> persistentState)
        {
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodaA", "nodaB") { ElectionTimeout = 100000 };
            var stateMachine = MockRepository.GenerateMock<IStateMachine<int>>();
            var transport = mockTransport();
            return new Node<int>(persistentState, nodeConfiguration, transport, stateMachine);

        }
        

        private static ITransport mockTransport()
        {
            var transport = MockRepository.GenerateMock<ITransport>();
            transport.Expect(t => t.Subscribe<VoteRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<VoteResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesRequest<int>>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            return transport;
        }

    }
}