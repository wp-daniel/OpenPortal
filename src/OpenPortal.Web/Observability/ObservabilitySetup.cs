using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenPortal.Web.Observability;

/// <summary>Bound from the <c>Observability</c> section.</summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// Where traces, metrics and logs go (an OpenTelemetry collector, or a backend that speaks OTLP: Grafana, Jaeger,
    /// Honeycomb, Datadog, Azure Monitor…). Empty uses <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>; with neither, nothing is
    /// exported and OpenTelemetry is not started.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary><c>grpc</c> (port 4317, the default) or <c>http</c> (http/protobuf, port 4318).</summary>
    public string Protocol { get; set; } = "grpc";

    /// <summary>The service name the telemetry carries (<c>service.name</c>).</summary>
    public string ServiceName { get; set; } = "openportal";

    /// <summary>Also send logs over OTLP (they keep going to the console either way).</summary>
    public bool ExportLogs { get; set; } = true;
}

/// <summary>The portal's own telemetry sources, exported with the framework's.</summary>
public static class PortalTelemetry
{
    /// <summary>Meters of the portal and its modules (each module creates its own; the names start with this).</summary>
    public const string MeterPrefix = "OpenPortal.";

    public const string OidcMeterName = "OpenPortal.Oidc";

    private static readonly Meter OidcMeter = new(OidcMeterName);

    /// <summary>Tokens issued at the token endpoint, by grant type.</summary>
    public static readonly Counter<long> TokensIssued =
        OidcMeter.CreateCounter<long>("openportal.oidc.tokens_issued", description: "Tokens issued by the OpenID Connect provider.");

    /// <summary>Token requests refused (access withdrawn, account inactive…), by reason.</summary>
    public static readonly Counter<long> TokensRefused =
        OidcMeter.CreateCounter<long>("openportal.oidc.tokens_refused", description: "Token requests refused by the access check.");

    /// <summary>Sign-ins to applications through the authorization endpoint, by outcome.</summary>
    public static readonly Counter<long> ApplicationSignIns =
        OidcMeter.CreateCounter<long>("openportal.oidc.sign_ins", description: "Sign-ins to applications through the portal.");

    public static readonly ActivitySource ActivitySource = new("OpenPortal");
}

/// <summary>
/// OpenTelemetry: traces (HTTP requests in and out, PostgreSQL commands), metrics (ASP.NET Core, HTTP client, .NET
/// runtime and the portal's own counters: sign-ins, tokens, delivery queue) and logs, exported over OTLP. Off until
/// an endpoint is configured, so a plain deployment pays nothing for it.
/// </summary>
public static class ObservabilitySetup
{
    public static WebApplicationBuilder AddOpenPortalObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        var endpoint = string.IsNullOrWhiteSpace(options.OtlpEndpoint)
            ? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            : options.OtlpEndpoint;

        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return builder;
        }

        var protocol = string.Equals(options.Protocol, "http", StringComparison.OrdinalIgnoreCase)
            ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
            : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

        void Exporter(OpenTelemetry.Exporter.OtlpExporterOptions exporter, string signal)
        {
            exporter.Protocol = protocol;

            // With http/protobuf each signal has its own path under the base address.
            exporter.Endpoint = protocol == OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                ? new Uri(endpointUri, $"v1/{signal}")
                : endpointUri;
        }

        var resource = ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, serviceVersion: typeof(ObservabilitySetup).Assembly.GetName().Version?.ToString())
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]);

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resource)
                .AddSource(PortalTelemetry.ActivitySource.Name)
                .AddAspNetCoreInstrumentation(aspNetCore =>
                {
                    // Probes would drown everything else.
                    aspNetCore.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                    aspNetCore.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(exporter => Exporter(exporter, "traces")))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(PortalTelemetry.OidcMeterName, "OpenPortal.Identity", "OpenPortal.Outbox", "Microsoft.AspNetCore.RateLimiting")
                .AddOtlpExporter(exporter => Exporter(exporter, "metrics")));

        if (options.ExportLogs)
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resource);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.AddOtlpExporter(exporter => Exporter(exporter, "logs"));
            });
        }

        return builder;
    }
}
