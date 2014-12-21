using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class InvestigationTests
    {
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
            Console.WriteLine(sw.ElapsedMilliseconds * 1.0 / 100000);

        }
        [Test, Ignore]
        public void GuidBasedRandom2Test()
        {

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                var buf = Guid.NewGuid().ToByteArray();
                var i1 = BitConverter.ToInt32(buf, 4) % 150;
                Console.WriteLine(i1);
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(sw.ElapsedMilliseconds * 1.0 / 100000);

        }
        [Test, Ignore]
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
            Console.WriteLine(sw.ElapsedMilliseconds * 1.0 / 100000);



        } 
    }
}