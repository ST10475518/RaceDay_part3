using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects pages using the MVC session. No session -> redirect to Login.
/// Wrong role (e.g. a Participant opening an Organiser page) -> Access Denied.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute(params string[] roles) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        if (session.GetInt32(SessionKeys.UserId) is null)
        {
            var request = context.HttpContext.Request;
            context.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl = request.Path + request.QueryString });
            return;
        }

        var role = session.GetString(SessionKeys.Role);
        if (roles.Length > 0 && !roles.Contains(role))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
