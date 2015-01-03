using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class StatePersisterTests
    {
        [Test]
        public void LogPersistenceTest()
        {
            var tempPath = Path.GetTempPath();
            Console.WriteLine(tempPath);
            var log = Path.Combine(tempPath,"log.data");
            if(File.Exists(log))
                File.Delete(log);



            using (var persister = new StatePersister<int>(tempPath))
            {
                persister.Append(new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(2, 3));
            }

            using (var persister = new StatePersister<int>(tempPath))
            {
                Assert.That(persister.GetLog(), Is.EquivalentTo(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2), new LogEntry<int>(2, 3) }),"Log was not restored correctly");
            }

            using (var persister = new StatePersister<int>(tempPath))
            {
                persister.RemoveAfter(1);
            }

            using (var persister = new StatePersister<int>(tempPath))
            {
                Assert.That(persister.GetLog(), Is.EquivalentTo(new[] { new LogEntry<int>(1, 1), new LogEntry<int>(1, 2)}), "Truncated log was not restored correctly");
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



            using (var persister = new StatePersister<int>(tempPath))
            {
                persister.SaveState(10,"some node");
            }

            using (var persister = new StatePersister<int>(tempPath))
            {
                var state = persister.GetState();
                Assert.That(state.Item1, Is.EqualTo(10),"Term was not restored correctly");
                Assert.That(state.Item2, Is.EqualTo("some node"), "Term was not restored correctly");
            }
             
        }
    }
}