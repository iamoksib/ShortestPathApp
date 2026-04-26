using System.Collections.Generic;

namespace ShortestPathApp.Models
{
    public class BellmanFordResult
    {
        public bool HasNegativeCycle { get; set; }
        public Dictionary<int, int> Distances { get; set; } = new();
        public Dictionary<int, int> Predecessors { get; set; } = new();
        public List<string> PathPolicies { get; set; } = new();
        
        //  Метрики производительности
        public int VertexCount { get; set; }
        public int OriginalEdgeCount { get; set; }
        public int TotalEdgeChecks { get; set; }
        public int SuccessfulRelaxations { get; set; }
        public double ExecutionTimeMs { get; set; }
    }
}