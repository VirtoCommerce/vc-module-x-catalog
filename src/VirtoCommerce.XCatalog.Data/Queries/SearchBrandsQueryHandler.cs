using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class SearchBrandsQueryHandler : IRequestHandler<SearchBrandQuery, SearchBrandResponse>
{
    public Task<SearchBrandResponse> Handle(SearchBrandQuery request, CancellationToken cancellationToken)
    {
        var brands = GetMockBrands().AsQueryable();

        if (!string.IsNullOrEmpty(request.Query))
        {
            brands = brands.Where(b => b.Name.Contains(request.Query));
        }

        var totalCount = brands.Count();
        var results = brands
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        return Task.FromResult(new SearchBrandResponse
        {
            TotalCount = totalCount,
            Results = results,
        });
    }

    private static IList<Brand> GetMockBrands()
    {
        var brands = new List<Brand>
        {
            new() { Id = "1", Name = "Nike", Description = "Sportswear and shoes", Image = "nike.jpg", Permalink = "nike", Featured = true },
            new() { Id = "2", Name = "Adidas", Description = "Athletic wear and gear", Image = "adidas.jpg", Permalink = "adidas", Featured = true },
            new() { Id = "3", Name = "Apple", Description = "Technology and electronics", Image = "apple.jpg", Permalink = "apple", Featured = true },
            new() { Id = "4", Name = "Samsung", Description = "Electronics and appliances", Image = "samsung.jpg", Permalink = "samsung", Featured = false },
            new() { Id = "5", Name = "Sony", Description = "Consumer electronics", Image = "sony.jpg", Permalink = "sony", Featured = false },
            new() { Id = "6", Name = "Microsoft", Description = "Software and electronics", Image = "microsoft.jpg", Permalink = "microsoft", Featured = true },
            new() { Id = "7", Name = "Google", Description = "Search engine and tech", Image = "google.jpg", Permalink = "google", Featured = true },
            new() { Id = "8", Name = "Amazon", Description = "E-commerce and cloud services", Image = "amazon.jpg", Permalink = "amazon", Featured = true },
            new() { Id = "9", Name = "Coca-Cola", Description = "Beverages and soft drinks", Image = "cocacola.jpg", Permalink = "coca-cola", Featured = false },
            new() { Id = "10", Name = "Pepsi", Description = "Soft drinks and beverages", Image = "pepsi.jpg", Permalink = "pepsi", Featured = false },
            new() { Id = "11", Name = "Toyota", Description = "Automobiles and vehicles", Image = "toyota.jpg", Permalink = "toyota", Featured = true },
            new() { Id = "12", Name = "BMW", Description = "Luxury cars", Image = "bmw.jpg", Permalink = "bmw", Featured = true },
            new() { Id = "13", Name = "Mercedes-Benz", Description = "Luxury automobiles", Image = "mercedes.jpg", Permalink = "mercedes-benz", Featured = true },
            new() { Id = "14", Name = "Intel", Description = "Semiconductors and processors", Image = "intel.jpg", Permalink = "intel", Featured = false },
            new() { Id = "15", Name = "AMD", Description = "Processors and graphics", Image = "amd.jpg", Permalink = "amd", Featured = false },
            new() { Id = "16", Name = "Netflix", Description = "Streaming media", Image = "netflix.jpg", Permalink = "netflix", Featured = true },
            new() { Id = "17", Name = "Spotify", Description = "Music streaming", Image = "spotify.jpg", Permalink = "spotify", Featured = false },
            new() { Id = "18", Name = "Starbucks", Description = "Coffee and beverages", Image = "starbucks.jpg", Permalink = "starbucks", Featured = false },
            new() { Id = "19", Name = "Zara", Description = "Fashion and clothing", Image = "zara.jpg", Permalink = "zara", Featured = false },
            new() { Id = "20", Name = "H&M", Description = "Apparel and accessories", Image = "hm.jpg", Permalink = "h-and-m", Featured = false }
        };

        return brands;
    }
}
