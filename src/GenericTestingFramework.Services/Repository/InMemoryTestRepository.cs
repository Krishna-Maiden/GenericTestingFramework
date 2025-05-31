using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using System.Collections.Concurrent;

namespace GenericTestingFramework.Services.Repository;

/// <summary>
/// In-memory implementation of test repository for development and testing
/// </summary>
public class InMemoryTestRepository : ITestRepository
{
    private readonly ConcurrentDictionary<string, TestScenario> _scenarios = new();
    private readonly ConcurrentDictionary<string, List<TestResult>> _results = new();

    public Task<string> SaveScenario(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        scenario.UpdatedAt = DateTime.UtcNow;
        _scenarios[scenario.Id] = scenario;
        return Task.FromResult(scenario.Id);
    }

    public Task<TestScenario?> GetScenario(string id, CancellationToken cancellationToken = default)
    {
        _scenarios.TryGetValue(id, out var scenario);
        return Task.FromResult(scenario);
    }

    public Task<List<TestScenario>> GetScenariosByProject(string projectId, CancellationToken cancellationToken = default)
    {
        var scenarios = _scenarios.Values
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.CreatedAt)
            .ToList();
        return Task.FromResult(scenarios);
    }

    public Task<List<TestScenario>> SearchScenarios(TestSearchCriteria searchCriteria, CancellationToken cancellationToken = default)
    {
        var query = _scenarios.Values.AsQueryable();

        if (!string.IsNullOrEmpty(searchCriteria.ProjectId))
            query = query.Where(s => s.ProjectId == searchCriteria.ProjectId);

        if (searchCriteria.Type.HasValue)
            query = query.Where(s => s.Type == searchCriteria.Type.Value);

        if (searchCriteria.Status.HasValue)
            query = query.Where(s => s.Status == searchCriteria.Status.Value);

        if (searchCriteria.Priority.HasValue)
            query = query.Where(s => s.Priority == searchCriteria.Priority.Value);

        if (!string.IsNullOrEmpty(searchCriteria.SearchText))
        {
            query = query.Where(s => s.Title.Contains(searchCriteria.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   s.Description.Contains(searchCriteria.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (searchCriteria.Tags.Any())
        {
            query = query.Where(s => s.Tags.Any(tag => searchCriteria.Tags.Contains(tag)));
        }

        if (!string.IsNullOrEmpty(searchCriteria.CreatedBy))
        {
            query = query.Where(s => s.CreatedBy.Contains(searchCriteria.CreatedBy, StringComparison.OrdinalIgnoreCase));
        }

        if (searchCriteria.CreatedFrom.HasValue)
            query = query.Where(s => s.CreatedAt >= searchCriteria.CreatedFrom.Value);

        if (searchCriteria.CreatedTo.HasValue)
            query = query.Where(s => s.CreatedAt <= searchCriteria.CreatedTo.Value);

        // Apply sorting
        query = searchCriteria.SortDescending
            ? query.OrderByDescending(s => GetSortValue(s, searchCriteria.SortBy))
            : query.OrderBy(s => GetSortValue(s, searchCriteria.SortBy));

        // Apply pagination
        var results = query
            .Skip((searchCriteria.PageNumber - 1) * searchCriteria.PageSize)
            .Take(searchCriteria.PageSize)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> UpdateScenario(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        if (_scenarios.ContainsKey(scenario.Id))
        {
            scenario.UpdatedAt = DateTime.UtcNow;
            _scenarios[scenario.Id] = scenario;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteScenario(string id, CancellationToken cancellationToken = default)
    {
        var removed = _scenarios.TryRemove(id, out _);
        if (removed)
        {
            _results.TryRemove(id, out _); // Also remove related results
        }
        return Task.FromResult(removed);
    }

    public Task<string> SaveResult(TestResult result, CancellationToken cancellationToken = default)
    {
        if (!_results.ContainsKey(result.ScenarioId))
        {
            _results[result.ScenarioId] = new List<TestResult>();
        }
        
        _results[result.ScenarioId].Add(result);
        return Task.FromResult(result.Id);
    }

    public Task<List<TestResult>> GetResults(string scenarioId, CancellationToken cancellationToken = default)
    {
        _results.TryGetValue(scenarioId, out var results);
        var sortedResults = results?.OrderByDescending(r => r.StartedAt).ToList() ?? new List<TestResult>();
        return Task.FromResult(sortedResults);
    }

    public Task<List<TestResult>> SearchResults(ResultSearchCriteria searchCriteria, CancellationToken cancellationToken = default)
    {
        var allResults = _results.Values.SelectMany(r => r).AsQueryable();

        if (!string.IsNullOrEmpty(searchCriteria.ScenarioId))
            allResults = allResults.Where(r => r.ScenarioId == searchCriteria.ScenarioId);

        if (!string.IsNullOrEmpty(searchCriteria.ProjectId))
        {
            var projectScenarios = _scenarios.Values.Where(s => s.ProjectId == searchCriteria.ProjectId).Select(s => s.Id);
            allResults = allResults.Where(r => projectScenarios.Contains(r.ScenarioId));
        }

        if (searchCriteria.Passed.HasValue)
            allResults = allResults.Where(r => r.Passed == searchCriteria.Passed.Value);

        if (searchCriteria.Environment.HasValue)
            allResults = allResults.Where(r => r.Environment == searchCriteria.Environment.Value);

        if (!string.IsNullOrEmpty(searchCriteria.ExecutedBy))
            allResults = allResults.Where(r => r.ExecutedBy.Contains(searchCriteria.ExecutedBy, StringComparison.OrdinalIgnoreCase));

        if (searchCriteria.ExecutedFrom.HasValue)
            allResults = allResults.Where(r => r.StartedAt >= searchCriteria.ExecutedFrom.Value);

        if (searchCriteria.ExecutedTo.HasValue)
            allResults = allResults.Where(r => r.StartedAt <= searchCriteria.ExecutedTo.Value);

        if (searchCriteria.MinDuration.HasValue)
            allResults = allResults.Where(r => r.Duration >= searchCriteria.MinDuration.Value);

        if (searchCriteria.MaxDuration.HasValue)
            allResults = allResults.Where(r => r.Duration <= searchCriteria.MaxDuration.Value);

        if (searchCriteria.ExecutionTags.Any())
        {
            allResults = allResults.Where(r => r.ExecutionTags.Any(tag => searchCriteria.ExecutionTags.Contains(tag)));
        }

        // Apply sorting
        allResults = searchCriteria.SortDescending
            ? allResults.OrderByDescending(r => GetResultSortValue(r, searchCriteria.SortBy))
            : allResults.OrderBy(r => GetResultSortValue(r, searchCriteria.SortBy));

        // Apply pagination
        var results = allResults
            .Skip((searchCriteria.PageNumber - 1) * searchCriteria.PageSize)
            .Take(searchCriteria.PageSize)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<TestStatistics> GetTestStatistics(string projectId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var projectScenarios = _scenarios.Values.Where(s => s.ProjectId == projectId).ToList();
        var allResults = _results.Values.SelectMany(r => r)
            .Where(r => r.StartedAt >= fromDate && r.StartedAt <= toDate)
            .ToList();

        // Filter results to only include those from project scenarios
        var projectScenarioIds = projectScenarios.Select(s => s.Id).ToHashSet();
        var projectResults = allResults.Where(r => projectScenarioIds.Contains(r.ScenarioId)).ToList();

        var stats = new TestStatistics
        {
            TotalScenarios = projectScenarios.Count,
            TotalExecutions = projectResults.Count,
            PassedExecutions = projectResults.Count(r => r.Passed),
            FailedExecutions = projectResults.Count(r => !r.Passed)
        };

        stats.PassRate = stats.TotalExecutions > 0 
            ? (double)stats.PassedExecutions / stats.TotalExecutions * 100 
            : 0;

        stats.AverageDuration = projectResults.Any() 
            ? TimeSpan.FromTicks((long)projectResults.Average(r => r.Duration.Ticks))
            : TimeSpan.Zero;

        // Statistics by test type
        var scenariosByType = projectScenarios.GroupBy(s => s.Type);
        foreach (var group in scenariosByType)
        {
            var typeScenarioIds = group.Select(s => s.Id).ToHashSet();
            var typeResults = projectResults.Where(r => typeScenarioIds.Contains(r.ScenarioId)).ToList();
            
            stats.StatsByType[group.Key] = new TypeStatistics
            {
                ScenarioCount = group.Count(),
                ExecutionCount = typeResults.Count,
                PassRate = typeResults.Any() ? (double)typeResults.Count(r => r.Passed) / typeResults.Count * 100 : 0,
                AverageDuration = typeResults.Any() ? TimeSpan.FromTicks((long)typeResults.Average(r => r.Duration.Ticks)) : TimeSpan.Zero
            };
        }

        // Statistics by environment
        var resultsByEnvironment = projectResults.GroupBy(r => r.Environment);
        foreach (var group in resultsByEnvironment)
        {
            stats.StatsByEnvironment[group.Key] = new EnvironmentStatistics
            {
                ExecutionCount = group.Count(),
                PassRate = (double)group.Count(r => r.Passed) / group.Count() * 100,
                AverageDuration = TimeSpan.FromTicks((long)group.Average(r => r.Duration.Ticks))
            };
        }

        // Daily trends
        var dailyGroups = projectResults.GroupBy(r => r.StartedAt.Date).OrderBy(g => g.Key);
        foreach (var group in dailyGroups)
        {
            stats.DailyTrends.Add(new DailyStatistics
            {
                Date = group.Key,
                ExecutionCount = group.Count(),
                PassRate = (double)group.Count(r => r.Passed) / group.Count() * 100,
                AverageDuration = TimeSpan.FromTicks((long)group.Average(r => r.Duration.Ticks))
            });
        }

        return Task.FromResult(stats);
    }

    public Task<int> ArchiveOldResults(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var archivedCount = 0;
        var keysToUpdate = new List<string>();

        foreach (var kvp in _results)
        {
            var oldResults = kvp.Value.Where(r => r.StartedAt < olderThan).ToList();
            if (oldResults.Any())
            {
                keysToUpdate.Add(kvp.Key);
                archivedCount += oldResults.Count;
            }
        }

        foreach (var key in keysToUpdate)
        {
            _results[key] = _results[key].Where(r => r.StartedAt >= olderThan).ToList();
            if (!_results[key].Any())
            {
                _results.TryRemove(key, out _);
            }
        }

        return Task.FromResult(archivedCount);
    }

    private object GetSortValue(TestScenario scenario, string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "title" => scenario.Title,
        "type" => scenario.Type.ToString(),
        "status" => scenario.Status.ToString(),
        "priority" => scenario.Priority.ToString(),
        "updatedat" => scenario.UpdatedAt,
        "createdby" => scenario.CreatedBy,
        _ => scenario.CreatedAt
    };

    private object GetResultSortValue(TestResult result, string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "duration" => result.Duration,
        "passed" => result.Passed,
        "environment" => result.Environment.ToString(),
        "completedat" => result.CompletedAt,
        "executedby" => result.ExecutedBy,
        _ => result.StartedAt
    };
}