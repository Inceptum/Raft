using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Raft.Logging;

namespace Inceptum.Raft
{

    public class Configurator
    {
        private NodeConfiguration m_Configuration;

        public Configurator(NodeConfiguration configuration )
        {
            m_Configuration = configuration;
        }

        public Configurator WithLogerFactory(Func<Type,ILogger> loggerFactory)
        {
            m_Configuration.LoggerFactory = loggerFactory;
            return this;
        }
    }

    public class NodeConfiguration
    {
        public int ElectionTimeout { get; set; } 
        public string NodeId { get; set; }


/*
        public NodeConfiguration(string nodeId, params string[] knownNodes)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("nodeId should be not empty string","nodeId");
            if (knownNodes.Any(string.IsNullOrEmpty))
                throw new ArgumentException("knownNodes should conatin only not empty string", "knownNodes");
            if (knownNodes.Distinct().Count() != knownNodes.Count())
                throw new ArgumentException("knownNodes should conatin unique node names", "knownNodes");
            LoggerFactory=type => new ConsoleLogger(type.Name);
            NodeId = nodeId;
            KnownNodes = knownNodes.Where(n=>n!=nodeId).Distinct().ToList();
        }

*/
        public NodeConfiguration(string nodeId)
        {
            LoggerFactory = type => new ConsoleLogger(type.Name);
            NodeId = nodeId;
        }


        public int? Majority { get; set; }
 
 /*       public int? Majority
        {
            get { return KnownNodes == null ? 0 : KnownNodes.Count / 2 + 1; }
        }
 */
        internal Func<Type, ILogger> LoggerFactory { get; set; }
    }
}