using System.Collections.Generic;
using ShortestPathApp.Models;

namespace ShortestPathApp.ViewModels
{
    public class GraphViewModel
    {
        public List<Edge> InputEdges { get; set; } = new();
        public string RawEdges { get; set; } = "";
        public int Source { get; set; } = 1;
        public int Target { get; set; } = 100;
        public int? VertexCount { get; set; }
        
        // 1. Беллман-Форд (Последовательный)
        public BellmanFordResult BellmanSeqResult { get; set; }
        public List<int> BellmanSeqPath { get; set; } = new();
        public string BellmanSeqSvg { get; set; } = "";

        // 2. Беллман-Форд (Параллельный)
        public BellmanFordResult BellmanParResult { get; set; }
        public List<int> BellmanParPath { get; set; } = new();
        public string BellmanParSvg { get; set; } = "";

        // 3. Дейкстра
        public BellmanFordResult DijkstraResult { get; set; }
        public List<int> DijkstraPath { get; set; } = new();
        public string DijkstraSvg { get; set; } = "";

        public string ErrorMessage { get; set; }
    }
}