using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IGB.Web.Security;

// Dynamic policy provider so you can use [Authorize(Policy = "perm:users.read")]
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var perm = policyName.Substring(Prefix.Length);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(perm))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}


