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
        public BellmanFordResult Result { get; set; }
        public List<int> ShortestPathVertices { get; set; } = new();
        public string ErrorMessage { get; set; }
        public string GraphSvg { get; set; } = "";
    }
}