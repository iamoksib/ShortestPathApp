using System.Collections.Generic;

namespace ShortestPathApp.Models
{
    public class BellmanFordResult
    {
        public bool HasNegativeCycle { get; set; }
        public Dictionary<int, int> Distances { get; set; } = new();
        public Dictionary<int, int> Predecessors { get; set; } = new();
        public List<string> PathPolicies { get; set; } = new(); // "1 -> 3 -> 4 : 7"
    }
}