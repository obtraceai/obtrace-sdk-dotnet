using Xunit;

namespace Obtrace.Sdk.Tests;

public class SemanticMetricsTests
{
    [Fact]
    public void ExposesCanonicalMetricNames()
    {
        Assert.Equal("runtime.cpu.utilization", SemanticMetrics.RuntimeCpuUtilization);
        Assert.Equal("db.operation.latency", SemanticMetrics.DbOperationLatency);
        Assert.Equal("web.vital.inp", SemanticMetrics.WebVitalInp);
    }
}
