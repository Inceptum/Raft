using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Http;
using Inceptum.Raft.Rpc;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class HttpTransportTests
    {
        private HttpTransport m_Transport1;
        private HttpTransport m_Transport2;
        private IDisposable m_Host1;
        private IDisposable m_Host2;

        [TestFixtureSetUp]
        public void Setup()
        {
            var endpoints = new Dictionary<string, Uri> { { "node1", new Uri("http://localhost:9001") }, { "node2", new Uri("http://localhost:9002") } };
            m_Transport1 = new HttpTransport(endpoints);
            m_Transport2 = new HttpTransport(endpoints);
            m_Host1 = m_Transport1.RunHost("http://localhost:9001/");
            m_Host2 = m_Transport2.RunHost("http://localhost:9002/");
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            m_Transport1.Dispose();
            m_Transport2.Dispose();
            m_Host1.Dispose();
            m_Host2.Dispose();
        }


        [Test]
        public void AppendEntriesRequestTest()
        {

            AppendEntriesRequest delivered = null;
            var received = new ManualResetEvent(false);
            var sw = Stopwatch.StartNew();
            m_Transport2.Subscribe<AppendEntriesRequest>(request =>
            {
                delivered = request;
                received.Set();
                return Task.FromResult((object)null);
            });
            var sent = new AppendEntriesRequest { LeaderCommit = 1, LeaderId = "node1", PrevLogIndex = 1, PrevLogTerm = 1, Term = 1, Entries = new[] { new LogEntry(1, 1), new LogEntry(1, 2) } };
            m_Transport1.Send("node2", sent);
            Assert.That(received.WaitOne(20000), Is.True, "AppendEntriesRequest was not delivered");
            Assert.That(delivered.LeaderCommit, Is.EqualTo(sent.LeaderCommit), "Wrong value of AppendEntriesRequest.LeaderCommit was delivered");
            Assert.That(delivered.LeaderId, Is.EqualTo(sent.LeaderId), "Wrong value of AppendEntriesRequest.LeaderId was delivered");
            Assert.That(delivered.PrevLogIndex, Is.EqualTo(sent.PrevLogIndex), "Wrong value of AppendEntriesRequest.PrevLogIndex was delivered");
            Assert.That(delivered.PrevLogTerm, Is.EqualTo(sent.PrevLogTerm), "Wrong value of AppendEntriesRequest.PrevLogTerm was delivered");
            Assert.That(delivered.Term, Is.EqualTo(sent.Term), "Wrong value of AppendEntriesRequest.Term was delivered");
            Assert.That(delivered.Entries, Is.EquivalentTo(sent.Entries), "Wrong value of AppendEntriesRequest.Entries was delivered");
            Console.WriteLine(sw.ElapsedMilliseconds);
        }


        [Test]
        public void AppendEntriesResponseTest()
        {

            AppendEntriesResponse delivered = null;
            var received = new ManualResetEvent(false);
            var sw = Stopwatch.StartNew();
            m_Transport2.Subscribe<AppendEntriesResponse>(request =>
            {
                delivered = request;
                received.Set();
                return Task.FromResult((object)null);
            });
            var sent = new AppendEntriesResponse{NodeId = "node1",Success = true,Term = 2};
            m_Transport1.Send("node2", sent);
            Assert.That(received.WaitOne(20000), Is.True, "AppendEntriesResponse was not delivered");
            Assert.That(delivered.NodeId, Is.EqualTo(sent.NodeId), "Wrong value of AppendEntriesResponse.NodeId was delivered");
            Assert.That(delivered.Success, Is.EqualTo(sent.Success), "Wrong value of AppendEntriesResponse.Success was delivered");
            Assert.That(delivered.Term, Is.EqualTo(sent.Term), "Wrong value of AppendEntriesResponse.Term was delivered");
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [Test]
        public void VoteRequestTest()
        {

            VoteRequest delivered = null;
            var received = new ManualResetEvent(false);
            var sw = Stopwatch.StartNew();
            m_Transport2.Subscribe<VoteRequest>(request =>
            {
                delivered = request;
                received.Set();
                return Task.FromResult((object)null);
            });
            var sent = new VoteRequest{CandidateId = "node1",LastLogIndex = 1,LastLogTerm = 1,Term = 2};
            m_Transport1.Send("node2", sent);
            Assert.That(received.WaitOne(20000), Is.True, "VoteRequest was not delivered");
            Assert.That(delivered.CandidateId, Is.EqualTo(sent.CandidateId), "Wrong value of VoteRequest.CandidateId was delivered");
            Assert.That(delivered.LastLogIndex, Is.EqualTo(sent.LastLogIndex), "Wrong value of VoteRequest.LastLogIndex was delivered");
            Assert.That(delivered.LastLogTerm, Is.EqualTo(sent.LastLogTerm), "Wrong value of VoteRequest.LastLogTerm was delivered");
            Assert.That(delivered.Term, Is.EqualTo(sent.Term), "Wrong value of VoteRequest.Term was delivered");
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
        [Test]
        public void VoteResponseTest()
        {

            VoteResponse delivered = null;
            var received = new ManualResetEvent(false);
            var sw = Stopwatch.StartNew();
            m_Transport2.Subscribe<VoteResponse>(request =>
            {
                delivered = request;
                received.Set();
                return Task.FromResult((object)null);
            });
            var sent = new VoteResponse { NodeId = "node1", Term = 2,VoteGranted = true};
            m_Transport1.Send("node2", sent);
            Assert.That(received.WaitOne(20000), Is.True, "VoteResponse was not delivered");
            Assert.That(delivered.NodeId, Is.EqualTo(sent.NodeId), "Wrong value of VoteResponse.NodeId was delivered");
            Assert.That(delivered.VoteGranted, Is.EqualTo(sent.VoteGranted), "Wrong value of VoteResponse.VoteGranted was delivered");
            Assert.That(delivered.Term, Is.EqualTo(sent.Term), "Wrong value of VoteResponse.Term was delivered");
            Console.WriteLine(sw.ElapsedMilliseconds);
        }  
        
        [Test]
        public  void ApplyCommadRequestTest()
        {

            ApplyCommadRequest delivered = null;
            var received = new ManualResetEvent(false);
            var processed = new ManualResetEvent(false);
            var acknowledged = new ManualResetEvent(false);
            var sw = Stopwatch.StartNew();
            m_Transport2.Subscribe<ApplyCommadRequest>(request =>
            {
                delivered = request;
                received.Set();
                processed.WaitOne();
                return Task.FromResult((object)null);
            });
            var sent = new ApplyCommadRequest {Command = 1};
            var task = m_Transport1.Send("node2", sent);

            

            Assert.That(received.WaitOne(200000), Is.True, "ApplyCommadRequest was not delivered");
            Assert.That(task.Wait(300), Is.False, "Send command task finished before command was processed");
            processed.Set();
            Assert.That(task.Wait(2000), Is.True, "Send command task has not finished on command processing complete");
            Assert.That(delivered.Command, Is.EqualTo(sent.Command), "Wrong value of ApplyCommadRequest.Command was delivered");
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}