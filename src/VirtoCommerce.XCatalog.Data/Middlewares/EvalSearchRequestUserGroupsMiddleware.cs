using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Extensions;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalSearchRequestUserGroupsMiddleware : IAsyncMiddleware<IndexSearchRequestBuilder>
    {
        private readonly IMemberResolver _memberResolver;
        private readonly IMemberService _memberService;
        private readonly IModuleCatalog _moduleCatalog;

        public EvalSearchRequestUserGroupsMiddleware(IMemberResolver memberResolver, IMemberService memberService, IModuleCatalog moduleCatalog)
        {
            _memberResolver = memberResolver;
            _memberService = memberService;
            _moduleCatalog = moduleCatalog;
        }

        public virtual async Task Run(IndexSearchRequestBuilder parameter, Func<IndexSearchRequestBuilder, Task> next)
        {
            if (_moduleCatalog.IsModuleInstalled("VirtoCommerce.CatalogPersonalization"))
            {
                var userGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__any" };

                if (!string.IsNullOrEmpty(parameter?.UserId) && !ModuleConstants.AnonymousUser.UserName.EqualsIgnoreCase(parameter.UserId))
                {
                    var member = await _memberResolver.ResolveMemberByIdAsync(parameter.UserId);

                    if (member is Contact contact)
                    {
                        GetUserGroupsFromContact(contact, userGroups);
                    }

                    await GetUserGroupsFromOrganization(parameter.OrganizationId, userGroups);
                }

                parameter?.AddTermFilter("user_groups", userGroups);
            }

            await next(parameter);
        }

        private static void GetUserGroupsFromContact(Contact contact, HashSet<string> userGroups)
        {
            if (!contact.Groups.IsNullOrEmpty())
            {
                userGroups.AddRange(contact.Groups);
            }
        }

        private async Task GetUserGroupsFromOrganization(string organizationId, HashSet<string> userGroups)
        {
            if (!organizationId.IsNullOrEmpty())
            {
                var organizations = await _memberService.GetByIdsAsync([organizationId], nameof(MemberResponseGroup.WithGroups));
                userGroups.AddRange(organizations.OfType<Organization>().SelectMany(x => x.Groups));
            }
        }
    }
}
