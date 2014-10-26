using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Raft
{
    public class NodeConfiguration
    {
        public int ElectionTimeout { get; set; } 
        public List<Guid> KnownNodes { get; set; } 
        public Guid NodeId { get; set; }


        public NodeConfiguration(Guid nodeId, params Guid[] knownNodes)
        {
            NodeId = nodeId;
            KnownNodes = knownNodes.Where(n=>n!=nodeId).ToList();
        }

        public int Majority
        {
            get { return KnownNodes == null ? 0 : KnownNodes.Count / 2 + 1; }
        }
    }
}