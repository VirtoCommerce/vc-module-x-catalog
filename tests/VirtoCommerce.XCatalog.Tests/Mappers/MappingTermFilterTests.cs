using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Data.Mapping;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Mappers
{
    public class MappingTermFilterTests
    {
        [Fact]
        public void FacetMappingProfileTest()
        {
            var mapperCfg = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new FacetMappingProfile());
            });

            var mapper = mapperCfg.CreateMapper();

            var source = new Aggregation
            {
                AggregationType = "attr",
                Items =
                [
                    new AggregationItem
                    {
                        Count = 1,
                        Value = "value",
                        IsApplied = true,
                    }
                ]
            };

            var destination = mapper.Map<FacetResult>(source, options => { options.Items["cultureName"] = "en-US"; });

            var result = destination as TermFacetResult;

            Assert.NotEmpty(result.Terms);
        }

    }
}
