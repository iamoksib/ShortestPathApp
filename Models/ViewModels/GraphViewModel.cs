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
        
        // Результаты двух алгоритмов
        public BellmanFordResult BellmanResult { get; set; }
        public BellmanFordResult DijkstraResult { get; set; }
        
        public List<int> BellmanPath { get; set; } = new();
        public List<int> DijkstraPath { get; set; } = new();
        
        public string ErrorMessage { get; set; }
        
        // SVG для отрисовки
        public string BellmanSvg { get; set; } = "";
        public string DijkstraSvg { get; set; } = "";
    }
}