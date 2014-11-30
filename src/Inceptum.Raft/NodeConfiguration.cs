using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Raft
{
    public class NodeConfiguration
    {
        public int ElectionTimeout { get; set; } 
        public List<string> KnownNodes { get; set; }
        public string NodeId { get; set; }


        public NodeConfiguration(string nodeId, params string[] knownNodes)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("nodeId should be not empty string","nodeId");
            if (knownNodes.Any(string.IsNullOrEmpty))
                throw new ArgumentException("knownNodes should conatin only not empty string", "knownNodes");
            if (knownNodes.Distinct().Count() != knownNodes.Count())
                throw new ArgumentException("knownNodes should conatin unique node names", "knownNodes");

            NodeId = nodeId;
            KnownNodes = knownNodes.Where(n=>n!=nodeId).Distinct().ToList();
        }

        public int Majority
        {
            get { return KnownNodes == null ? 0 : KnownNodes.Count / 2 + 1; }
        }
    }
}