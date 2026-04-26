using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShortestPathApp.Models;

namespace ShortestPathApp.Services
{
    public class ShortestPathService
    {
        public BellmanFordResult RunBellmanFord(List<Edge> inputEdges, int source)
        {
            // ⏱️ Запускаем точный таймер для замера времени выполнения
            var sw = Stopwatch.StartNew();
            var result = new BellmanFordResult();

            // 1. Формируем неориентированный граф (дублируем рёбра в обе стороны)
            var edges = new List<Edge>();
            var vertices = new HashSet<int>();
            foreach (var e in inputEdges)
            {
                edges.Add(new Edge { From = e.From, To = e.To, Weight = e.Weight });
                edges.Add(new Edge { From = e.To, To = e.From, Weight = e.Weight });
                vertices.Add(e.From);
                vertices.Add(e.To);
            }

            result.VertexCount = vertices.Count;
            result.OriginalEdgeCount = inputEdges.Count;

            if (!vertices.Contains(source))
                throw new ArgumentException($"Стартовая вершина {source} отсутствует в графе.");

            int vertexCount = vertices.Count;
            var dist = new Dictionary<int, int>(vertexCount);
            var pred = new Dictionary<int, int>(vertexCount);

            foreach (var v in vertices)
            {
                dist[v] = int.MaxValue;
                pred[v] = -1;
            }
            dist[source] = 0;

            int edgeChecks = 0;
            int successfulRelaxations = 0;

            // 2. Релаксация рёбер (максимум |V|-1 итераций)
            for (int i = 0; i < vertexCount - 1; i++)
            {
                bool changed = false;
                foreach (var e in edges)
                {
                    edgeChecks++; // Считаем каждую проверку ребра
                    if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                    {
                        dist[e.To] = dist[e.From] + e.Weight;
                        pred[e.To] = e.From;
                        successfulRelaxations++;
                        changed = true;
                    }
                }
                
                // 🚀 Оптимизация: если за полный проход ни одно расстояние не обновилось,
                // значит кратчайшие пути уже найдены. Прерываем цикл досрочно.
                if (!changed) break;
            }

            // 3. Проверка на отрицательный цикл
            foreach (var e in edges)
            {
                edgeChecks++;
                if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                {
                    result.HasNegativeCycle = true;
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    result.TotalEdgeChecks = edgeChecks;
                    result.SuccessfulRelaxations = successfulRelaxations;
                    return result;
                }
            }

            // 4. Формирование политик путей (восстановление маршрутов)
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
                string pathStr = string.Join(" -> ", path);
                result.PathPolicies.Add($"{pathStr} : {dist[v]}");
            }

            sw.Stop();
            result.Distances = dist;
            result.Predecessors = pred;
            result.TotalEdgeChecks = edgeChecks;
            result.SuccessfulRelaxations = successfulRelaxations;
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;

            return result;
        }
    }
}