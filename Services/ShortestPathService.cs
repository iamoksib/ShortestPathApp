using System;
using System.Collections.Generic;
using System.Linq;
using ShortestPathApp.Models;

namespace ShortestPathApp.Services
{
    public class ShortestPathService
    {
        public BellmanFordResult RunBellmanFord(List<Edge> inputEdges, int source)
        {
            var result = new BellmanFordResult();

            // 1. Формируем неориентированный граф: каждое ребро добавляем в обе стороны
            var edges = new List<Edge>();
            var vertices = new HashSet<int>();
            foreach (var e in inputEdges)
            {
                edges.Add(new Edge { From = e.From, To = e.To, Weight = e.Weight });
                edges.Add(new Edge { From = e.To, To = e.From, Weight = e.Weight });
                vertices.Add(e.From);
                vertices.Add(e.To);
            }

            if (!vertices.Contains(source))
                throw new ArgumentException($"Стартовая вершина {source} отсутствует в графе.");

            int vertexCount = vertices.Count;
            var dist = new Dictionary<int, int>();
            var pred = new Dictionary<int, int>();

            foreach (var v in vertices)
            {
                dist[v] = int.MaxValue;
                pred[v] = -1;
            }
            dist[source] = 0;

            // 2. Релаксация ребер |V|-1 раз
            for (int i = 0; i < vertexCount - 1; i++)
            {
                foreach (var e in edges)
                {
                    if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                    {
                        dist[e.To] = dist[e.From] + e.Weight;
                        pred[e.To] = e.From;
                    }
                }
            }

            // 3. Проверка на отрицательный цикл
            foreach (var e in edges)
            {
                if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                {
                    result.HasNegativeCycle = true;
                    return result;
                }
            }

            // 4. Формирование политик путей
            foreach (var v in vertices.OrderBy(v => v))
            {
                if (v == source) continue;
                if (dist[v] == int.MaxValue)
                {
                    result.PathPolicies.Add($"{source} -> ... -> {v} : unreachable");
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

            result.Distances = dist;
            result.Predecessors = pred;
            return result;
        }
    }
}