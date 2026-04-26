using Microsoft.AspNetCore.Mvc;
using ShortestPathApp.Models;
using ShortestPathApp.Services;
using ShortestPathApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShortestPathApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ShortestPathService _pathService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ShortestPathService pathService, ILogger<HomeController> logger)
        {
            _pathService = pathService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new GraphViewModel { Target = 100 });
        }

        [HttpPost]
        public IActionResult Calculate(GraphViewModel model, string action)
        {
            if (action == "Generate")
            {
                if (model.VertexCount == null || model.VertexCount < 2)
                {
                    model.ErrorMessage = "Количество вершин должно быть ≥ 2";
                    return View(model);
                }

                int n = model.VertexCount.Value;
                model.InputEdges = GenerateRandomGraph(n);
                model.Source = 1;
                model.Target = n;
                model.RawEdges = string.Join("\n", model.InputEdges.Select(e => $"{e.From},{e.To},{e.Weight}"));
                model.GraphSvg = GenerateSvg(model.InputEdges, model.ShortestPathVertices);
                return View(model);
            }

            // Парсинг рёбер из textarea
            if (!string.IsNullOrWhiteSpace(model.RawEdges))
            {
                var lines = model.RawEdges.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 3 && 
                        int.TryParse(parts[0].Trim(), out int from) && 
                        int.TryParse(parts[1].Trim(), out int to) && 
                        int.TryParse(parts[2].Trim(), out int weight))
                    {
                        model.InputEdges.Add(new Edge { From = from, To = to, Weight = weight });
                    }
                }
            }

            if (!model.InputEdges.Any())
            {
                model.ErrorMessage = "Граф пуст. Введите рёбра или сгенерируйте граф.";
                return View(model);
            }

            try
            {
                var result = _pathService.RunBellmanFord(model.InputEdges, model.Source);
                if (result.HasNegativeCycle)
                {
                    model.ErrorMessage = "⚠️ Обнаружен отрицательный цикл. Наикратчайший путь не определён.";
                    model.Result = result;
                    model.GraphSvg = GenerateSvg(model.InputEdges, model.ShortestPathVertices);
                    return View(model);
                }

                model.Result = result;

                // Восстановление пути от Start до Target
                if (result.Distances.TryGetValue(model.Target, out int dist) && dist != int.MaxValue)
                {
                    var path = new List<int>();
                    int curr = model.Target;
                    while (curr != -1)
                    {
                        path.Add(curr);
                        if (curr == model.Source) break;
                        curr = result.Predecessors[curr];
                    }
                    path.Reverse();
                    model.ShortestPathVertices = path;
                }

                model.GraphSvg = GenerateSvg(model.InputEdges, model.ShortestPathVertices);
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"❌ Ошибка: {ex.Message}";
                _logger.LogError(ex, "Ошибка вычисления пути");
            }

            return View(model);
        }

        private List<Edge> GenerateRandomGraph(int n)
        {
            var edges = new List<Edge>();
            var rand = new Random();

            // 1. Гарантируем связность: цепочка 1→2→3...→n
            for (int i = 1; i < n; i++)
                edges.Add(new Edge { From = i, To = i + 1, Weight = rand.Next(1, 10) });

            // 2. Добавляем случайные рёбра (плотность ~15%)
            int extra = n / 6;
            for (int i = 0; i < extra; i++)
            {
                int u = rand.Next(1, n + 1);
                int v = rand.Next(1, n + 1);
                if (u != v)
                    edges.Add(new Edge { From = u, To = v, Weight = rand.Next(1, 10) });
            }
            return edges;
        }

        private string GenerateSvg(List<Edge> edges, List<int> pathVertices)
        {
            var vertices = new HashSet<int>();
            foreach (var e in edges) { vertices.Add(e.From); vertices.Add(e.To); }
            int n = vertices.Count;
            if (n == 0) return "";

            // Сетка для раскладки
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            double spacing = Math.Max(40, Math.Min(80, 900.0 / cols));
            double offsetX = 30, offsetY = 30;

            var pos = new Dictionary<int, (double, double)>();
            int i = 0;
            foreach (var v in vertices.OrderBy(x => x))
            {
                pos[v] = (offsetX + (i % cols) * spacing, offsetY + (i / cols) * spacing);
                i++;
            }

            var pathSet = new HashSet<(int, int)>();
            if (pathVertices.Count > 1)
            {
                for (int k = 0; k < pathVertices.Count - 1; k++)
                {
                    pathSet.Add((pathVertices[k], pathVertices[k + 1]));
                    pathSet.Add((pathVertices[k + 1], pathVertices[k]));
                }
            }

            var sb = new StringBuilder();
            double width = cols * spacing + offsetX * 2;
            double height = ((n - 1) / cols + 1) * spacing + offsetY * 2;
            sb.Append($"<svg viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background:#000\">");

            // Рёбра
            foreach (var e in edges)
            {
                if (pos.ContainsKey(e.From) && pos.ContainsKey(e.To))
                {
                    var (x1, y1) = pos[e.From];
                    var (x2, y2) = pos[e.To];
                    bool isPath = pathSet.Contains((e.From, e.To));
                    string stroke = isPath ? "#fff" : "#444";
                    string strokeWidth = isPath ? "3" : "1";
                    string strokeDash = isPath ? "stroke-dasharray:8,4;" : "";
                    sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" style=\"{strokeDash}\" />");
                }
            }

            // Вершины
            foreach (var v in vertices.OrderBy(x => x))
            {
                var (x, y) = pos[v];
                bool onPath = pathVertices.Contains(v);
                string fill = onPath ? "#fff" : "#1a1a1a";
                string stroke = onPath ? "#fff" : "#666";
                double r = n > 500 ? 6 : 10;
                string textColor = fill == "#fff" ? "#000" : "#fff";
                sb.Append($"<circle cx=\"{x}\" cy=\"{y}\" r=\"{r}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{(onPath ? 3 : 1)}\" />");
                if (n <= 500)
                    sb.Append($"<text x=\"{x}\" y=\"{y}\" fill=\"{textColor}\" font-size=\"9\" font-family=\"monospace\" text-anchor=\"middle\" dominant-baseline=\"central\">{v}</text>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }
    }
}