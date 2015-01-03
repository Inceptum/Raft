using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class FilePersistentStateTests
    {
        [Test]
        public void LogPersistenceTest()
        {
            var tempPath = Path.GetTempPath();
            Console.WriteLine(tempPath);
            var log = Path.Combine(tempPath,"log.data");
            if(File.Exists(log))
                File.Delete(log);



            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                persistentState.Append(new []{new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(2, 3)});
            }

            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                Assert.That(persistentState.Log, Is.EquivalentTo(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(2, 3) }),"Log was not restored correctly");
            }

            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                persistentState.DeleteEntriesAfter(1);
            }

            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                Assert.That(persistentState.Log, Is.EquivalentTo(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2)}), "Truncated log was not restored correctly");
            }

        }
        [Test]
        public void StatePersistenceTest()
        {
            var tempPath = Path.GetTempPath();
            Console.WriteLine(tempPath);
            var stateFile = Path.Combine(tempPath,"state.data");
            if(File.Exists(stateFile))
                File.Delete(stateFile);



            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                persistentState.CurrentTerm=10;
                persistentState.VotedFor="some node";
            }

            using (var persistentState = new FilePersistentState<int>(tempPath))
            {
                Assert.That(persistentState.CurrentTerm, Is.EqualTo(10), "Term was not restored correctly");
                Assert.That(persistentState.VotedFor, Is.EqualTo("some node"), "Term was not restored correctly");
            }
             
        }
    }
}