using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShortestPathApp.Models;

namespace ShortestPathApp.Services
{
    public class DijkstraService
    {
        public BellmanFordResult RunDijkstra(List<Edge> inputEdges, int source)
        {
            var sw = Stopwatch.StartNew();
            var result = new BellmanFordResult(); // Используем ту же модель для единообразия метрик

            // 1. Подготовка графа (неориентированный)
            var adj = new Dictionary<int, List<(int neighbor, int weight)>>();
            var vertices = new HashSet<int>();

            foreach (var e in inputEdges)
            {
                // Проверка на отрицательные веса (Дейкстра не поддерживает)
                if (e.Weight < 0)
                    throw new ArgumentException("Алгоритм Дейкстры не поддерживает отрицательные веса рёбер.");

                if (!adj.ContainsKey(e.From)) adj[e.From] = new List<(int, int)>();
                if (!adj.ContainsKey(e.To)) adj[e.To] = new List<(int, int)>();

                adj[e.From].Add((e.To, e.Weight));
                adj[e.To].Add((e.From, e.Weight));
                vertices.Add(e.From);
                vertices.Add(e.To);
            }

            result.VertexCount = vertices.Count;
            result.OriginalEdgeCount = inputEdges.Count;

            if (!vertices.Contains(source))
                throw new ArgumentException($"Стартовая вершина {source} отсутствует в графе.");

            // 2. Инициализация
            var dist = new Dictionary<int, int>();
            var pred = new Dictionary<int, int>();
            var visited = new HashSet<int>();

            foreach (var v in vertices)
            {
                dist[v] = int.MaxValue;
                pred[v] = -1;
            }
            dist[source] = 0;

            // Используем PriorityQueue для эффективности (.NET 6+)
            // Хранит пары (Вершина, Расстояние), сортировка по Расстоянию
            var pq = new PriorityQueue<int, int>();
            pq.Enqueue(source, 0);

            int iterationsCount = 0;
            int edgeChecks = 0;
            int successfulRelaxations = 0;

            // 3. Основной цикл
            while (pq.Count > 0)
            {
                pq.TryDequeue(out int u, out int currentDist);
                iterationsCount++; // Считаем извлечение вершины как итерацию

                if (visited.Contains(u)) continue;
                visited.Add(u);

                if (adj.ContainsKey(u))
                {
                    foreach (var (v, weight) in adj[u])
                    {
                        edgeChecks++;
                        if (!visited.Contains(v))
                        {
                            if (dist[u] != int.MaxValue && dist[u] + weight < dist[v])
                            {
                                dist[v] = dist[u] + weight;
                                pred[v] = u;
                                successfulRelaxations++;
                                pq.Enqueue(v, dist[v]);
                            }
                        }
                    }
                }
            }

            // 4. Восстановление путей
            foreach (var v in vertices.OrderBy(v => v))
            {
                if (v == source) continue;
                if (dist[v] == int.MaxValue)
                {
                    result.PathPolicies.Add($"{source} -> ... -> {v} : недостижимо");
                    continue;
                }

                var path = new List<int>();
                int curr = v;
                while (curr != -1)
                {
                    path.Add(curr);
                    if (curr == source) break;
                    curr = pred[curr];
                }
                path.Reverse();
                result.PathPolicies.Add($"{string.Join(" -> ", path)} : {dist[v]}");
            }

            sw.Stop();
            result.Distances = dist;
            result.Predecessors = pred;
            result.IterationsCount = iterationsCount;
            result.TotalEdgeChecks = edgeChecks;
            result.SuccessfulRelaxations = successfulRelaxations;
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;

            return result;
        }
    }
}