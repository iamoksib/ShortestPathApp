using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShortestPathApp.Models;

namespace ShortestPathApp.Services
{
    /// <summary>
    /// Сервис для расчёта кратчайших путей в графе с использованием алгоритма Беллмана-Форда.
    /// Поддерживает неориентированные графы, обнаружение отрицательных циклов и сбор метрик производительности.
    /// </summary>
    public class ShortestPathService
    {
        /// <summary>
        /// Выполняет алгоритм Беллмана-Форда для поиска кратчайших путей от исходной вершины до всех остальных.
        /// </summary>
        /// <param name="inputEdges">Список исходных рёбер графа.</param>
        /// <param name="source">Номер стартовой вершины.</param>
        /// <returns>Объект результата с расстояниями, предшественниками, путями и метриками.</returns>
        public BellmanFordResult RunBellmanFord(List<Edge> inputEdges, int source)
        {
            // ⏱️ ЗАМЕР ВРЕМЕНИ: Точный учёт времени выполнения алгоритма (без учёта overhead MVC)
            var sw = Stopwatch.StartNew();
            var result = new BellmanFordResult();

            // ========================================================================
            // ШАГ 1: ПРЕДПОДГОТОВКА ГРАФА (Адаптация под неориентированный случай)
            // ========================================================================
            // Алгоритм Беллмана-Форда изначально разработан для ориентированных графов.
            // Поскольку по условию граф неориентированный, каждое входное ребро (u, v)
            // дублируется в обратном направлении (v, u) с тем же весом.
            var edges = new List<Edge>();
            var vertices = new HashSet<int>();
            foreach (var e in inputEdges)
            {
                edges.Add(new Edge { From = e.From, To = e.To, Weight = e.Weight });
                edges.Add(new Edge { From = e.To, To = e.From, Weight = e.Weight }); // Обратное ребро
                vertices.Add(e.From);
                vertices.Add(e.To);
            }

            result.VertexCount = vertices.Count;
            result.OriginalEdgeCount = inputEdges.Count;

            if (!vertices.Contains(source))
                throw new ArgumentException($"Стартовая вершина {source} отсутствует в графе.");

            // ========================================================================
            // ШАГ 2: ИНИЦИАЛИЗАЦИЯ ДИСТАНЦИЙ И ПРЕДШЕСТВЕННИКОВ
            // ========================================================================
            // dist[v] хранит текущую оценку кратчайшего расстояния от source до v.
            // Изначально: dist[source] = 0, для всех остальных v: dist[v] = ∞ (int.MaxValue).
            // pred[v] хранит предыдущую вершину в текущем кратчайшем пути к v (для восстановления пути).
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

            // ========================================================================
            // ШАГ 3: ОСНОВНОЙ ЦИКЛ РЕЛАКСАЦИИ РЁБЕР
            // ========================================================================
            // Теоретическая гарантия: после |V| - 1 итераций все кратчайшие пути
            // (не содержащие отрицательных циклов) будут найдены.
            // На каждой итерации мы пытаемся "улучшить" (релаксировать) оценку dist[v]
            // для каждого ребра (u, v): если dist[u] + w(u,v) < dist[v], обновляем dist[v].
            // for (int i = 0; i < vertexCount - 1; i++)
            // {
            //     bool changed = false; // Флаг для оптимизации раннего выхода

            //     foreach (var e in edges)
            //     {
            //         edgeChecks++; // Учёт каждой проверки ребра для метрик

            //         // Условие релаксации: путь через u короче текущего известного пути до v.
            //         // Проверка dist[e.From] != int.MaxValue защищает от переполнения при сложении.
            //         if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
            //         {
            //             dist[e.To] = dist[e.From] + e.Weight;
            //             pred[e.To] = e.From;
            //             successfulRelaxations++;
            //             changed = true;
            //         }
            //     }

            int iterationsCount = 0; // ✅ Счётчик итераций

            for (int i = 0; i < vertexCount - 1; i++)
            {
                iterationsCount++; // ✅ Увеличиваем счётчик при каждом проходе
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
                //     // 🚀 ОПТИМИЗАЦИЯ: Если за полный проход по всем рёбрам ни одно расстояние
                //     // не было обновлено, значит, кратчайшие пути уже стабилизировались.
                //     // Прерываем цикл досрочно (экономит время на разреженных графах).
                //     if (!changed) break;
                // }
                if (!changed) break;
            }

            // ========================================================================
            // ШАГ 4: ОБНАРУЖЕНИЕ ОТРИЦАТЕЛЬНЫХ ЦИКЛОВ
            // ========================================================================
            // После |V|-1 итераций алгоритм делает одну дополнительную проверку.
            // Если удаётся улучшить расстояние хотя бы для одного ребра, значит,
            // в графе присутствует цикл с отрицательным суммарным весом.
            // В таком случае понятие "кратчайший путь" теряет смысл (можно бесконечно уменьшать вес).
            foreach (var e in edges)
            {
                edgeChecks++;
                if (dist[e.From] != int.MaxValue && dist[e.From] + e.Weight < dist[e.To])
                {
                    result.HasNegativeCycle = true;
                    // Финализируем метрики и возвращаем результат сразу
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    result.IterationsCount = iterationsCount;
                    result.TotalEdgeChecks = edgeChecks;
                    result.SuccessfulRelaxations = successfulRelaxations;
                    return result;
                }
            }

            // ========================================================================
            // ШАГ 5: ВОССТАНОВЛЕНИЕ ПУТЕЙ (ПОЛИТИКИ МАРШРУТИЗАЦИИ)
            // ========================================================================
            // Используем массив предшественников pred[] для восстановления полных путей
            // от стартовой вершины до каждой достижимой вершины.
            // Двигаемся от целевой вершины назад к source, пока не достигнем -1.
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
                path.Reverse(); // Путь восстанавливается в обратном порядке, разворачиваем

                string pathStr = string.Join(" -> ", path);
                result.PathPolicies.Add($"{pathStr} : {dist[v]}");
            }

            // ========================================================================
            // ЗАВЕРШЕНИЕ: Фиксация метрик и возврат результата
            // ========================================================================
            sw.Stop();
            result.Distances = dist;
            result.Predecessors = pred;
            result.TotalEdgeChecks = edgeChecks;
            result.IterationsCount = iterationsCount;
            result.SuccessfulRelaxations = successfulRelaxations;
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;

            return result;
        }
    }
}