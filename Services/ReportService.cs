using System.Data;
using Microsoft.Data.SqlClient;
using TMS.WebApp.Sdk.Data.Sql;

namespace tms_template_net8.Services;

public sealed class ReportService : IReportService
{
    private readonly ISqlExecutor _sql;

    public ReportService(ISqlExecutor sql)
    {
        _sql = sql;
    }

    public Task<DataTable> RunReportAsync(string region, CancellationToken cancellationToken = default)
    {
        var parameters = new[]
        {
            new SqlParameter("@Region", region)
        };

        return _sql.ExecuteAsync(
            "dbo.PSP_MY_REPORT",
            CommandType.StoredProcedure,
            parameters,
            cancellationToken: cancellationToken);
    }

    public Task<List<ActiveEmployeeRow>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default)
    {
        return _sql.QueryAsync<ActiveEmployeeRow>(
            "SELECT Id, Name FROM dbo.Employee WHERE Active = 1",
            cancellationToken: cancellationToken);
    }
}
