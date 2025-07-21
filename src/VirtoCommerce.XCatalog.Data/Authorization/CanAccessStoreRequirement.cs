using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using StoreSetting = VirtoCommerce.StoreModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.XCatalog.Core.Authorization;

public class CanAccessStoreAuthorizationRequirement : IAuthorizationRequirement
{
}

public class CanAccessStoreAuthorizationHandler : AuthorizationHandler<CanAccessStoreAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CanAccessStoreAuthorizationRequirement requirement)
    {
        var result = context.User.Identity.IsAuthenticated;

        if (!result && context.Resource is Store store)
        {
            result = store.Settings?.GetValue<bool>(StoreSetting.AllowAnonymousUsers) == true;
        }

        if (result)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
