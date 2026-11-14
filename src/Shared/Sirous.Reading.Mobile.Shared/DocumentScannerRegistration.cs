using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared;

/// <summary>
/// Slice 273 part C: wraps the platform's registered <see cref="IDocumentScanner"/> in <see cref="DocumentScannerReporting"/>. The original registration moves to a keyed slot
/// unchanged (same lifetime, so the container still builds, shares and disposes it as before) and the wrapper takes its place with the same lifetime.
/// </summary>
public static class DocumentScannerRegistration
{
    private const string InnerKey = "Slice273.InnerDocumentScanner";

    public static void ReportFallbacks(IServiceCollection services)
    {
        ServiceDescriptor? scanner = services.LastOrDefault(d => d.ServiceType == typeof(IDocumentScanner) && !d.IsKeyedService);
        ServiceDescriptor? inner = scanner is null ? null : Keyed(scanner);
        if (scanner is null || inner is null)
        {
            return;
        }

        services.Remove(scanner);
        services.Add(inner);
        services.Add(ServiceDescriptor.Describe(typeof(IDocumentScanner),
            sp => new DocumentScannerReporting(sp.GetRequiredKeyedService<IDocumentScanner>(InnerKey), sp.GetService<IFallbackSink>()), scanner.Lifetime));
    }

    // Every way a platform can register the scanner (by type, by factory, by instance), so the wrapper never silently skips one.
    private static ServiceDescriptor? Keyed(ServiceDescriptor scanner) => scanner switch
    {
        { ImplementationType: { } type } => new ServiceDescriptor(typeof(IDocumentScanner), InnerKey, type, scanner.Lifetime),
        { ImplementationFactory: { } factory } => new ServiceDescriptor(typeof(IDocumentScanner), InnerKey, (sp, _) => factory(sp), scanner.Lifetime),
        { ImplementationInstance: { } instance } => new ServiceDescriptor(typeof(IDocumentScanner), InnerKey, instance),
        _ => null,
    };
}
