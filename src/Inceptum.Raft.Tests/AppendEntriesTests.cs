using System;
using System.Threading;
using Inceptum.Raft.Rpc;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class AppendEntriesTests
    {

        [Test(Description = "Reply false if term < currentTerm (§5.1)")]
        public void NodeRepliesFalseIfThereIsNoOnAppendEntriesWithOlderTermTestt()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 10 };
            var appendEntriesRequest = new AppendEntriesRequest<int> { Entries = new ILogEntry<int>[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = -1, PrevLogTerm = -1, Term = 1 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.False, "Successful response was sent for request with old term");
        }


        [Test(Description = "Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)")]
        public void NodeRepliesFalseOnAppendEntriesIfDoesNotHaveLogEntryAtPrevLogIndexMatchingPrevLogTermTes()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 2 };
            persistentState.Append(new[] { new LogEntry<int>(2, 2), });
            var appendEntriesRequest = new AppendEntriesRequest<int> { Entries = new ILogEntry<int>[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 1, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.False, "Successful response was sent for request with missing log entries");
        }


        [Test(Description = "If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it (§5.3)")]
        public void ConflictingLogRemoveTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            persistentState.Append(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2) });
            var appendEntriesRequest = new AppendEntriesRequest<int> { Entries = new ILogEntry<int>[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 0, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.True, "Successful response was not sent for request");
            Assert.That(persistentState.CurrentTerm, Is.EqualTo(2), "Term was not updated to received from leader");
            Assert.That(persistentState.Log, Is.EqualTo(new ILogEntry<int>[] { new LogEntry<int>(1, 1) }), "Conflicting log entries were not removed");

        }


        [Test(Description = "Append any new entries not already in the log")]
        public void AppendAnyNewEntriesNotAlredyInLogTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            persistentState.Append(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2) });
            var appendEntriesRequest = new AppendEntriesRequest<int> { Entries = new ILogEntry<int>[] { new LogEntry<int>(1, 3), new LogEntry<int>(2, 4) }, LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = 1, PrevLogTerm = 1, Term = 2 };
            var response = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest).Item1;
            Assert.That(response.Success, Is.True, "Successful response was not sent for request");
            Assert.That(persistentState.CurrentTerm, Is.EqualTo(2), "Term was not updated to received from leader");
            Assert.That(persistentState.Log, Is.EqualTo(new ILogEntry<int>[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(1, 3), new LogEntry<int>(2, 4) }), "Log entries were not appended");
        }

        [Test(Description = "Append any new entries not already in the log")]
        public void CommitUpToLeaderCommitIndexTest()
        {
            var logEntries = new ILogEntry<int>[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(1, 3), new LogEntry<int>(2, 4) };
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            persistentState.Append(logEntries);
            var appendEntriesRequest = new AppendEntriesRequest<int> { Entries = new LogEntry<int>[0], LeaderCommit = 1, LeaderId = "nodeA", PrevLogIndex = 3, PrevLogTerm = 2, Term = 2 };
            var res = createFollowerAndHandleAppendEntriesRequest(persistentState, appendEntriesRequest);
            var response = res.Item1;
            var node = res.Item2;
            Assert.That(response.Success, Is.True, "Successful response was not sent for request");
            Assert.That(node.CommitIndex, Is.EqualTo(1), "Log entries were not commited");
            Assert.That(node.LastApplied, Is.EqualTo(1), "Log entries were not commited");
        }

        private Tuple<AppendEntriesResponse, Node<int>> createFollowerAndHandleAppendEntriesRequest(PersistentState<int> persistentState, AppendEntriesRequest<int> appendEntriesRequest, Action<int> apply=null)
        {
            apply = apply ?? (i => { }); 
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodaA", "nodaB") { ElectionTimeout = 100000 };
            var stateMachine = MockRepository.GenerateMock<IStateMachine<int>>();
            stateMachine.Expect(m => m.Apply(0)).IgnoreArguments().Do(apply);
            AppendEntriesResponse response = null;
            var responseSent = new ManualResetEvent(false);
            Action<string, string, AppendEntriesResponse> send = (from, to, r) => { response = r; responseSent.Set(); };
            var transport = mockTransport();
            transport.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeA"), Arg<AppendEntriesResponse>.Is.Anything)).Do(send);
            using (var node = new Node<int>(persistentState, nodeConfiguration, transport, stateMachine))
            {
                node.Start();
                node.Handle(appendEntriesRequest);
                Assert.That(responseSent.WaitOne(1000), Is.True, "Response was not sent");
                transport.VerifyAllExpectations();
                return Tuple.Create(response, node);
            }

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