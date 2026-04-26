using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShortestPathApp.Models;

namespace ShortestPathApp.Services
{
    public class ShortestPathService
    {
        // ==========================================
        // 1. КЛАССИЧЕСКИЙ (ПОСЛЕДОВАТЕЛЬНЫЙ) АЛГОРИТМ
        // ==========================================
        public BellmanFordResult RunBellmanFord(List<Edge> inputEdges, int source)
        {
            var sw = Stopwatch.StartNew();
            var result = new BellmanFordResult();

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
            if (!vertices.Contains(source)) throw new ArgumentException("Стартовая вершина не найдена.");

            int vertexCount = vertices.Count;
            var dist = new Dictionary<int, int>(vertexCount);
            var pred = new Dictionary<int, int>(vertexCount);

            foreach (var v in vertices) { dist[v] = int.MaxValue; pred[v] = -1; }
            dist[source] = 0;

            int edgeChecks = 0;
            int successfulRelaxations = 0;
            int iterationsCount = 0;

            for (int i = 0; i < vertexCount - 1; i++)
            {
                iterationsCount++;
                bool changed = false;

                foreach (var e in edges)
                {
                    edgeChecks++;
                    if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                    {
                        dist[e.To] = dist[e.From] + e.Weight;
                        pred[e.To] = e.From;
                        successfulRelaxations++;
                        changed = true;
                    }
                }
                if (!changed) break;
            }

            // Проверка на отрицательный цикл
            foreach (var e in edges)
            {
                edgeChecks++;
                if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                {
                    result.HasNegativeCycle = true;
                    sw.Stop();
                    FillMetrics(result, edgeChecks, successfulRelaxations, iterationsCount, sw);
                    return result;
                }
            }

            // Восстановление путей
            foreach (var v in vertices.OrderBy(v => v))
            {
                if (v == source) continue;
                if (dist[v] == int.MaxValue) { result.PathPolicies.Add($"{source} -> ... -> {v} : недостижимо"); continue; }

                var path = new List<int>();
                int curr = v;
                while (curr != -1) { path.Add(curr); if (curr == source) break; curr = pred[curr]; }
                path.Reverse();
                result.PathPolicies.Add($"{string.Join(" -> ", path)} : {dist[v]}");
            }

            sw.Stop();
            result.Distances = dist;
            result.Predecessors = pred;
            FillMetrics(result, edgeChecks, successfulRelaxations, iterationsCount, sw);
            return result;
        }

        // ==========================================
        // 2. ПАРАЛЛЕЛЬНЫЙ АЛГОРИТМ (ОПТИМИЗИРОВАННЫЙ)
        // ==========================================
        public BellmanFordResult RunBellmanFordParallel(List<Edge> inputEdges, int source)
        {
            var sw = Stopwatch.StartNew();
            var result = new BellmanFordResult();

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
            if (!vertices.Contains(source)) throw new ArgumentException("Стартовая вершина не найдена.");

            // --- Маппинг вершин в индексы 0..N-1 для работы с массивами (быстрее Dictionary) ---
            int vertexCount = vertices.Count;
            var vertexList = vertices.OrderBy(v => v).ToList();
            var idToIndex = new Dictionary<int, int>(vertexCount);
            for (int i = 0; i < vertexCount; i++) idToIndex[vertexList[i]] = i;

            var edgesIndexed = edges.Select(e => (
                u: idToIndex[e.From], 
                v: idToIndex[e.To], 
                w: e.Weight
            )).ToList();

            int[] dist = new int[vertexCount];
            int[] pred = new int[vertexCount];
            Array.Fill(dist, int.MaxValue);
            Array.Fill(pred, -1);
            dist[idToIndex[source]] = 0;

            long edgeChecks = 0;
            long successfulRelaxations = 0;
            int iterationsCount = 0;
            
            // Используем Interlocked для потокобезопасного флага изменения
            int changedFlag = 0; 

            // --- Параллельный цикл ---
            for (int i = 0; i < vertexCount - 1; i++)
            {
                iterationsCount++;
                changedFlag = 0; // Сброс флага перед итерацией

                Parallel.ForEach(edgesIndexed, e =>
                {
                    Interlocked.Increment(ref edgeChecks);

                    // Проверка дистанции
                    if (dist[e.u] != int.MaxValue && dist[e.u] + e.w < dist[e.v])
                    {
                        int oldDist = dist[e.v];
                        int newDist = dist[e.u] + e.w;

                        // Атомарное обновление: записываем только если значение не изменилось другим потоком
                        if (Interlocked.CompareExchange(ref dist[e.v], newDist, oldDist) == oldDist)
                        {
                            pred[e.v] = e.u;
                            Interlocked.Increment(ref successfulRelaxations);
                            // Ставим флаг, что изменения были
                            Interlocked.Exchange(ref changedFlag, 1);
                        }
                    }
                });

                // Если за полный проход никто ничего не изменил, выходим
                if (changedFlag == 0) break;
            }

            // Проверка на отрицательный цикл
            foreach (var e in edgesIndexed)
            {
                if (dist[e.u] != int.MaxValue && dist[e.u] + e.w < dist[e.v])
                {
                    result.HasNegativeCycle = true;
                    sw.Stop();
                    FillMetrics(result, edgeChecks, successfulRelaxations, iterationsCount, sw);
                    return result;
                }
            }

            // Восстановление путей
            var distDict = new Dictionary<int, int>(vertexCount);
            var predDict = new Dictionary<int, int>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                distDict[vertexList[i]] = dist[i];
                predDict[vertexList[i]] = dist[i] != int.MaxValue && pred[i] != -1 ? vertexList[pred[i]] : -1;
            }

            foreach (var v in vertexList)
            {
                if (v == source) continue;
                if (distDict[v] == int.MaxValue) { result.PathPolicies.Add($"{source} -> ... -> {v} : недостижимо"); continue; }

                var path = new List<int>();
                int curr = v;
                while (curr != -1) { path.Add(curr); if (curr == source) break; curr = predDict[curr]; }
                path.Reverse();
                result.PathPolicies.Add($"{string.Join(" -> ", path)} : {distDict[v]}");
            }

            sw.Stop();
            result.Distances = distDict;
            result.Predecessors = predDict;
            FillMetrics(result, edgeChecks, successfulRelaxations, iterationsCount, sw);
            return result;
        }

        private void FillMetrics(BellmanFordResult res, long checks, long relaxations, int iterations, Stopwatch sw)
        {
            res.TotalEdgeChecks = (int)checks;
            res.SuccessfulRelaxations = (int)relaxations;
            res.IterationsCount = iterations;
            res.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
        }
    }
}