using System.Data;

namespace tms_template_net8.Services;

public interface IReportService
{
    /// <summary>
    /// Executes a sample stored procedure and returns tabular results.
    /// </summary>
    Task<DataTable> RunReportAsync(string region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a sample SQL query using Dapper mapping.
    /// </summary>
    Task<List<ActiveEmployeeRow>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default);
}

public sealed record ActiveEmployeeRow(int Id, string Name);
