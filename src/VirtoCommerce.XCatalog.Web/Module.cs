using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCatalog.Core;
using VirtoCommerce.XCatalog.Data;
using VirtoCommerce.XCatalog.Data.Extensions;

namespace VirtoCommerce.XCatalog.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        var graphQlBuilder = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(CoreAssemblyMarker), typeof(DataAssemblyMarker));
        });
        serviceCollection.AddXCatalog(graphQlBuilder);
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        appBuilder.UseScopedSchema<DataAssemblyMarker>("catalog");
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
