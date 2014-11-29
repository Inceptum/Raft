using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft;

namespace TestConsoleApplication
{
    class Program
    {
        private static  PerformanceCounter m_CpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); 
        private static void Main(string[] args)
        {
            Test(150);
        }

        private static void Test(int electionTimeout)
        {
            Node<object>.m_Log.Clear();
            var sb = new StringBuilder();
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
                    id =>
                        new Node<object>(new PersistentState<object>(), new NodeConfiguration(id, knownNodes.ToArray()) { ElectionTimeout = electionTimeout },
                            inMemoryTransport))
                    .ToArray();

                var start = DateTime.Now;
                foreach (var node in nodes)
                {
                    node.Start();
                }

                var tokenSource2 = new CancellationTokenSource();
                CancellationToken ct = tokenSource2.Token;

                StringBuilder log=new StringBuilder();
                Task.Factory.StartNew(() =>
                {
                    long cterm = 0;
                    while (true)
                    {
                        var nodeStates = nodes.Select(node => new {node.Id, node.State, node.LeaderId, node.Configuration, node.CurrentTerm}).ToArray();

                        Console.Write(log.ToString());
                        Console.WriteLine("[{3:000}] {0} - {1} ({2}) CPU: {4:00}%", start, DateTime.Now, DateTime.Now - start, cterm,m_CpuCounter.NextValue());
                        foreach (var node in nodeStates)
                        {
                            if (node.CurrentTerm > cterm)
                            {
                                log.AppendLine(string.Format("[{3:000}] {0} - {1} ({2}) CPU: {4:00}%", start, DateTime.Now, DateTime.Now - start, cterm, m_CpuCounter.NextValue()));
                                cterm = node.CurrentTerm;
                                start = DateTime.Now;
                            }
                            Console.WriteLine("[{3}] {0}: {1}\tLeader:{2}", node.Id, node.State, node.LeaderId, node.CurrentTerm);
                        }
                        Console.WriteLine();
                        Thread.Sleep(500);
                    }
                },ct);
/*
               Thread.Sleep(3000);
                var leaderId = nodes.First().LeaderId.Value;
                Console.WriteLine("Failing the leader" + leaderId);
               inMemoryTransport.EmulateConnectivityIssue(leaderId);
               Thread.Sleep(1000);
               Console.WriteLine("Restoring ex leader" + leaderId);
               inMemoryTransport.RestoreConnectivity(leaderId);
               Thread.Sleep(1000);*/
               Console.ReadLine();
                tokenSource2.Cancel();

                {
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
                }

                var term = nodes.Select(n => n.CurrentTerm).First();
                Debug.WriteLine("{0}\t{1}", (nodes.Single(n => n.State == NodeState.Leader).CurrentStateEnterTime - start).TotalMilliseconds.ToString().Replace(".", ","), term);
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
                System.IO.File.WriteAllText("out.log", Node<object>.m_Log.ToString());
                //Console.WriteLine(Node<object>.m_Log);
            }
            
        }
    }
}
