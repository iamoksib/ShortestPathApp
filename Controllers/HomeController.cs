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
        private readonly ShortestPathService _bellmanService;
        private readonly DijkstraService _dijkstraService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ShortestPathService bellmanService, DijkstraService dijkstraService, ILogger<HomeController> logger)
        {
            _bellmanService = bellmanService;
            _dijkstraService = dijkstraService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View("Index", new GraphViewModel { Source = 1, Target = 100 });
        }

        [HttpPost]
        public IActionResult Calculate(GraphViewModel model, string action)
        {
            // ========================================================================
            // ЛОГИКА ГЕНЕРАЦИИ ГРАФА
            // ========================================================================
            if (action == "Generate")
            {
                if (model.VertexCount == null || model.VertexCount < 2)
                {
                    model.ErrorMessage = "Ошибка: Количество вершин должно быть не менее 2.";
                    return View("Index", model);
                }

                int n = model.VertexCount.Value;
                model.InputEdges = GenerateRandomGraph(n);
                model.Source = 1;
                model.Target = n;
                model.RawEdges = string.Join("\n", model.InputEdges.Select(e => $"{e.From},{e.To},{e.Weight}"));

                // При генерации показываем граф без выделенного пути
                model.BellmanSvg = GenerateSvg(model.InputEdges, new List<int>());
                model.DijkstraSvg = GenerateSvg(model.InputEdges, new List<int>());
                return View("Index", model);
            }

            // ========================================================================
            // ЛОГИКА ПАРСИНГА РУЧНОГО ВВОДА
            // ========================================================================
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
                model.ErrorMessage = "Ошибка: Данные графа пусты. Сгенерируйте граф или введите рёбра вручную.";
                return View("Index", model);
            }

            // ========================================================================
            // ЛОГИКА ЗАПУСКА АЛГОРИТМОВ
            // ========================================================================
            try
            {
                // 1️⃣ Беллман-Форд (универсален, работает с отрицательными весами)
                var bellmanRes = _bellmanService.RunBellmanFord(model.InputEdges, model.Source);
                model.BellmanResult = bellmanRes;

                if (bellmanRes.Distances.TryGetValue(model.Target, out int bDist) && bDist != int.MaxValue)
                {
                    model.BellmanPath = ReconstructPath(bellmanRes.Predecessors, model.Source, model.Target);
                }
                model.BellmanSvg = GenerateSvg(model.InputEdges, model.BellmanPath);

                // 2️⃣ Дейкстра (требует неотрицательных весов)
                bool hasNegativeWeights = model.InputEdges.Any(e => e.Weight < 0);
                if (hasNegativeWeights)
                {
                    model.ErrorMessage = "Внимание: Алгоритм Дейкстры не поддерживает отрицательные веса. Расчёт пропущен.";
                    model.DijkstraResult = null;
                    model.DijkstraSvg = "";
                }
                else
                {
                    var dijkstraRes = _dijkstraService.RunDijkstra(model.InputEdges, model.Source);
                    model.DijkstraResult = dijkstraRes;

                    if (dijkstraRes.Distances.TryGetValue(model.Target, out int dDist) && dDist != int.MaxValue)
                    {
                        model.DijkstraPath = ReconstructPath(dijkstraRes.Predecessors, model.Source, model.Target);
                    }
                    model.DijkstraSvg = GenerateSvg(model.InputEdges, model.DijkstraPath);
                }
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"Критическая ошибка при расчёте: {ex.Message}";
                _logger.LogError(ex, "Ошибка выполнения алгоритмов поиска пути.");
            }

            return View("Index", model);
        }

        /// <summary>
        /// Восстанавливает полный путь от стартовой до целевой вершины 
        /// используя словарь предшественников (backtracking).
        /// </summary>
        private List<int> ReconstructPath(Dictionary<int, int> predecessors, int source, int target)
        {
            var path = new List<int>();
            int curr = target;
            while (curr != -1)
            {
                path.Add(curr);
                if (curr == source) break;
                if (!predecessors.ContainsKey(curr)) break;
                curr = predecessors[curr];
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Генерирует случайный связный неориентированный граф с положительными весами.
        /// Гарантирует наличие пути от 1 до N через цепочку.
        /// </summary>
        private List<Edge> GenerateRandomGraph(int n)
        {
            var edges = new List<Edge>();
            var rand = new Random();

            // 1. Гарантируем связность цепочкой: 1→2→3→...→n
            for (int i = 1; i < n; i++)
                edges.Add(new Edge { From = i, To = i + 1, Weight = rand.Next(1, 10) });

            // 2. Добавляем случайные рёбра для создания альтернативных маршрутов
            int extraEdges = (n / 5) + 10;
            for (int i = 0; i < extraEdges; i++)
            {
                int u = rand.Next(1, n + 1);
                int v = rand.Next(1, n + 1);
                if (u != v)
                    edges.Add(new Edge { From = u, To = v, Weight = rand.Next(1, 20) });
            }
            return edges;
        }

        /// <summary>
        /// Генерирует SVG-разметку для визуализации графа.
        /// Автоматически масштабирует радиусы и шрифты для больших графов (до 10 000 вершин).
        /// Подсвечивает кратчайший путь пунктирной линией и цветом.
        /// </summary>
        private string GenerateSvg(List<Edge> edges, List<int> pathVertices)
        {
            var vertices = new HashSet<int>();
            foreach (var e in edges) { vertices.Add(e.From); vertices.Add(e.To); }
            int n = vertices.Count;
            if (n == 0) return "";

            // Расчёт сетки для равномерного расположения вершин
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            double spacing = Math.Max(40, Math.Min(80, 1000.0 / cols));
            double offsetX = 30, offsetY = 30;

            var pos = new Dictionary<int, (double, double)>();
            int i = 0;
            foreach (var v in vertices.OrderBy(x => x))
            {
                pos[v] = (offsetX + (i % cols) * spacing, offsetY + (i / cols) * spacing);
                i++;
            }

            // Формируем множество рёбер, входящих в кратчайший путь (в обе стороны)
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

            // Белый фон для корректной печати и скриншотов
            sb.Append($"<svg viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background:#fff; border:1px solid #ccc;\">");

            // Отрисовка рёбер
            foreach (var e in edges)
            {
                if (pos.ContainsKey(e.From) && pos.ContainsKey(e.To))
                {
                    var (x1, y1) = pos[e.From];
                    var (x2, y2) = pos[e.To];
                    bool isPath = pathSet.Contains((e.From, e.To));

                    string strokeColor = isPath ? "#0056b3" : "#cccccc";
                    string strokeWidth = isPath ? "3" : "1";
                    string dashArray = isPath ? "6,3" : "";

                    sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" stroke-dasharray=\"{dashArray}\" />");
                }
            }

            // Отрисовка вершин и подписей
            foreach (var v in vertices.OrderBy(x => x))
            {
                var (x, y) = pos[v];
                bool onPath = pathVertices.Contains(v);

                // Адаптивные размеры для сохранения читаемости на больших графах
                double radius = n > 2000 ? 3 : (n > 500 ? 4 : (n > 100 ? 6 : 8));
                double fontSize = n > 2000 ? 5 : (n > 500 ? 6 : (n > 100 ? 7 : 9));

                string fillColor = onPath ? "#0056b3" : "#ffffff";
                string strokeColor = onPath ? "#003d80" : "#666666";
                string textColor = onPath ? "#ffffff" : "#000000";

                sb.Append($"<circle cx=\"{x}\" cy=\"{y}\" r=\"{radius}\" fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"1.5\" />");
                sb.Append($"<text x=\"{x}\" y=\"{y}\" fill=\"{textColor}\" font-size=\"{fontSize}\" font-family=\"sans-serif\" text-anchor=\"middle\" dominant-baseline=\"central\">{v}</text>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }
    }
}