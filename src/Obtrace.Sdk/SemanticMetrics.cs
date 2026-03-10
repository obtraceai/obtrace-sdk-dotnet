namespace Obtrace.Sdk;

public static class SemanticMetrics
{
    public const string Throughput = "http_requests_total";
    public const string ErrorRate = "http_5xx_total";
    public const string LatencyP95 = "latency_p95";
    public const string RuntimeCpuUtilization = "runtime.cpu.utilization";
    public const string RuntimeMemoryUsage = "runtime.memory.usage";
    public const string RuntimeThreadCount = "runtime.thread.count";
    public const string RuntimeGcPause = "runtime.gc.pause";
    public const string RuntimeEventloopLag = "runtime.eventloop.lag";
    public const string ClusterCpuUtilization = "cluster.cpu.utilization";
    public const string ClusterMemoryUsage = "cluster.memory.usage";
    public const string ClusterNodeCount = "cluster.node.count";
    public const string ClusterPodCount = "cluster.pod.count";
    public const string DbOperationLatency = "db.operation.latency";
    public const string DbClientErrors = "db.client.errors";
    public const string DbConnectionsUsage = "db.connections.usage";
    public const string MessagingConsumerLag = "messaging.consumer.lag";
    public const string WebVitalLcp = "web.vital.lcp";
    public const string WebVitalFcp = "web.vital.fcp";
    public const string WebVitalInp = "web.vital.inp";
    public const string WebVitalCls = "web.vital.cls";
    public const string WebVitalTtfb = "web.vital.ttfb";
    public const string UserActions = "obtrace.sim.web.react.actions";
}
