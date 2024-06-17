using AutoMapper;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Mapping
{
    public class CategoryMappingProfile : Profile
    {
        public CategoryMappingProfile()
        {
            CreateMap<SearchDocument, ExpCategory>().ConvertUsing(src => new GenericModelBinder<ExpCategory>().BindModel(src) as ExpCategory);
        }
    }
}
