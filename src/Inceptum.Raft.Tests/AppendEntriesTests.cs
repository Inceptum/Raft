using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class LogReplicationTests
    {

        [Test(Description = "Reply false if term < currentTerm (§5.1)")]
        public void NodeRepliesFalseIfThereIsNoOnAppendEntriesWithOlderTermTestt()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 10 };
            var appendEntriesRequest = new AppendEntriesRequest { Entries = new LogEntry[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = -1, PrevLogTerm = -1, Term = 1 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.False, "Successful response was sent for request with old term");
        }


        [Test(Description = "Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)")]
        public void NodeRepliesFalseOnAppendEntriesIfDoesNotHaveLogEntryAtPrevLogIndexMatchingPrevLogTermTes()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 2 };
            persistentState.Append(new[] { new LogEntry(2, 2), });
            var appendEntriesRequest = new AppendEntriesRequest { Entries = new LogEntry[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 1, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.False, "Successful response was sent for request with missing log entries");
        }


        [Test(Description = "If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it (§5.3)")]
        public void ConflictingLogRemoveTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            persistentState.Append(new[] { new LogEntry(1, 1), new LogEntry(1, 2) });
            var appendEntriesRequest = new AppendEntriesRequest { Entries = new LogEntry[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 0, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.True, "Successful response was not sent for request");
            Assert.That(persistentState.CurrentTerm, Is.EqualTo(2), "Term was not updated to received from leader");
            Assert.That(persistentState.Log, Is.EqualTo(new LogEntry[] { new LogEntry(1, 1) }), "Conflicting log entries were not removed");

        }


        [Test(Description = "Append any new entries not already in the log")]
        public void AppendAnyNewEntriesNotAlredyInLogTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            persistentState.Append(new[] { new LogEntry(1, 1), new LogEntry(1, 2) });
            var appendEntriesRequest = new AppendEntriesRequest { Entries = new [] { new LogEntry(1, 3), new LogEntry(2, 4) }, LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 1, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.True, "Successful response was not sent for request");
            Assert.That(persistentState.CurrentTerm, Is.EqualTo(2), "Term was not updated to received from leader");
            Assert.That(persistentState.Log, Is.EqualTo(new LogEntry[] { new LogEntry(1, 1), new LogEntry(1, 2), new LogEntry(1, 3), new LogEntry(2, 4) }), "Log entries were not appended");
        }

        [Test(Description = "Append any new entries not already in the log")]
        public void CommitUpToLeaderCommitIndexTest()
        {
            var logEntries = new LogEntry[] { new LogEntry(1, 1), new LogEntry(1, 2), new LogEntry(1, 3), new LogEntry(2, 4) };
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            persistentState.Append(logEntries);
            var appendEntriesRequest = new AppendEntriesRequest { Entries = new LogEntry[0], LeaderCommit = 1, LeaderId = "nodeA", PrevLogIndex = 3, PrevLogTerm = 2, Term = 2 };
            ManualResetEvent applied=new ManualResetEvent(false);
            int counter =0;
            var res = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest, i => { if (++counter == 2) applied.Set(); },doNotDisposeNode:true);
            var response = res.Item1;
            using (var node = res.Item2)
            {
                Assert.That(response.Success, Is.True, "Successful response was not sent for request");
                Assert.That(node.CommitIndex, Is.EqualTo(1), "Log entries were not commited");

                Assert.That(applied.WaitOne(1000), Is.True, "Commited log entries were not applied to state machine");
                Assert.That(counter, Is.EqualTo(2), "Not all commited log entries were not applied to state machine");
                Assert.That(node.LastApplied, Is.EqualTo(1), "LastApplied is wrong");
            }
        }


        [Test(Description = "Upon election: Leader sends initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts")]
        public void LeaderHbTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 100 };
        
            var bus = mockTransport();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeA"), Arg<AppendEntriesRequest>.Is.Anything)).Repeat.Twice();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeB"), Arg<AppendEntriesRequest>.Is.Anything)).Repeat.Twice();

            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode",bus), ()=>new Object()))
            {
                node.Start();
                node.SwitchToLeader();
                Thread.Sleep(150);
                bus.VerifyAllExpectations();//send AppendEntriesRequest to all nodes twice - on election amd after timeout elapsed
            }
        }        
        [Test(Description = "If last log index ≥ nextIndex for a follower: send AppendEntries RPC with log entries starting at nextIndex")]
        public void LogReplicationTest()
        {
            var logEntries = new LogEntry[] { new LogEntry(1, 1), new LogEntry(1, 2)  };
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            persistentState.Append(logEntries);
            //TODO: send AppendEntriesRequest immediately on not success response
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 100  };
            var bus = mockTransport();
            var requests = new List<AppendEntriesRequest>();
            var requestSent = new ManualResetEvent(false);
            int cnt=0;
            Node node = null;
            Func<string, string, AppendEntriesRequest, Task<object>> send = (from, to, r) =>
            {
                requests.Add(r);
                if (++cnt < 4)
                {
                    //Return false 3 times (emulating that log is 2 entries beghind the leader's)
                    node.Handle(new AppendEntriesResponse { NodeId = to, Success = false, Term = 1 });
                    return Task.FromResult(default(object));
                }
              
                node.Handle(new AppendEntriesResponse { NodeId = to, Success = true, Term = 1 });

               
                requestSent.Set();
                return Task.FromResult(default(object));
            };

            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeA"), Arg<AppendEntriesRequest>.Is.Anything)).Repeat.Once();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeB"), Arg<AppendEntriesRequest>.Is.Anything)).Repeat.Times(4).Do(send);

            using (node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new StateMachineMock(null)))
            {
                node.Start();
                node.SwitchToLeader();
               
                Assert.That(requestSent.WaitOne(2000), "AppendEntriesResponse with reduced index was not sent");
                Assert.That(requests[0].Entries, Is.EqualTo(new LogEntry[] { }), "Initial AppendEntriesRequestwas not empty ");
                Assert.That(requests[1].Entries, Is.EqualTo(new LogEntry[] { new LogEntry(1, 2) }),"Leader did not repeated last entry when got first unsuccessful response");
                Assert.That(requests[2].Entries, Is.EqualTo(new LogEntry[] { new LogEntry(1, 1), new LogEntry(1, 2) }), "Leader did not repeated 2 last entries when got first unsuccessful response");
                Assert.That(requests[3].Entries, Is.EqualTo(new LogEntry[] { new LogEntry(1, 1), new LogEntry(1, 2) }), "Leader did not repeated all entries when got more unsuccessful responses then entries count in the log");
                bus.VerifyAllExpectations();//send AppendEntriesRequest to all nodes twice - on election amd after timeout elapsed
            }
        }

        private Tuple<AppendEntriesResponse, Node> createFollowerAndHandleAppendEntriesRequest(InMemoryPersistentState persistentState, AppendEntriesRequest appendEntriesRequest, Action<int> apply=null, bool doNotDisposeNode=false)
        {
             
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 100000 };
            var stateMachine = new StateMachineMock(apply);
            AppendEntriesResponse response = null;
            var responseSent = new ManualResetEvent(false);
            Func<string, string, AppendEntriesResponse,Task<object>> send = (from, to, r) =>
            {
                response = r;
                responseSent.Set();
                return Task.FromResult(default(object));
            }; 
            var bus = mockTransport();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeA"), Arg<AppendEntriesResponse>.Is.Anything)).Do(send);
            var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => stateMachine);
            using (doNotDisposeNode?ActionDisposable.Create(() => { }):node)
            {
                node.Start();
                node.Handle(appendEntriesRequest);
                Assert.That(responseSent.WaitOne(1000), Is.True, "Response was not sent");
                bus.VerifyAllExpectations();
                return Tuple.Create(response, node);
            }

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

    class StateMachineMock
    {
        private readonly Action<int> m_Apply;

        public StateMachineMock(Action<int> apply)
        {
            m_Apply = apply??(i => { });
        }

        public void Apply(int i)
        {
            m_Apply(i);
        }
    }
}