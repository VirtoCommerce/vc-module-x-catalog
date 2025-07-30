using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalSearchRequestUserGroupsMiddleware : IAsyncMiddleware<IndexSearchRequestBuilder>
    {
        protected readonly IMemberResolver _memberResolver;
        protected readonly IMemberService _memberService;
        protected readonly IModuleCatalog _moduleCatalog;

        public EvalSearchRequestUserGroupsMiddleware(IMemberResolver memberResolver, IMemberService memberService, IModuleCatalog moduleCatalog)
        {
            _memberResolver = memberResolver;
            _memberService = memberService;
            _moduleCatalog = moduleCatalog;
        }

        public virtual async Task Run(IndexSearchRequestBuilder parameter, Func<IndexSearchRequestBuilder, Task> next)
        {
            // Please note that this solution is temporary. In the upcoming release, we are actively working on resolving this issue by introducing optional dependencies.
            // With optional dependencies, the XAPI will seamlessly integrate with the Catalog Personalization Module if it is installed, and gracefully handle scenarios where the module is not present.
            // This approach will provide a more robust and flexible solution, enabling smoother interactions between the XAPI and the Catalog Personalization Module.
            if (IsCatalogPersonalizationModuleInstalled())
            {
                var userGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__any" };

                if (!string.IsNullOrEmpty(parameter?.UserId) && !ModuleConstants.AnonymousUser.UserName.EqualsIgnoreCase(parameter.UserId))
                {
                    var member = await _memberResolver.ResolveMemberByIdAsync(parameter.UserId);

                    if (member is Contact contact)
                    {
                        await GetUserGroupsInheritedAsync(contact, userGroups);
                    }
                }

                parameter?.AddTermFilter("user_groups", userGroups);
            }

            await next(parameter);
        }


        /// <summary>
        /// Checks if the Catalog Personalization Module is installed.
        /// </summary>
        /// <returns></returns>
        private bool IsCatalogPersonalizationModuleInstalled()
        {
            return _moduleCatalog.Modules.Any(m => m.ModuleName == "VirtoCommerce.CatalogPersonalization");
        }

        private async Task GetUserGroupsInheritedAsync(Contact contact, HashSet<string> userGroups)
        {
            if (!contact.Groups.IsNullOrEmpty())
            {
                userGroups.AddRange(contact.Groups);
            }

            if (!contact.Organizations.IsNullOrEmpty())
            {
                var organizations = await _memberService.GetByIdsAsync(contact.Organizations.ToArray(), nameof(MemberResponseGroup.WithGroups));
                userGroups.AddRange(organizations.OfType<Organization>().SelectMany(x => x.Groups));
            }
        }
    }
}
