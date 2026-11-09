using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>Turns API failures and unexpected errors into friendly pages instead of raw error output.</summary>
public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, ITempDataDictionaryFactory tempFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiUnauthorizedException:
                context.HttpContext.Session.Clear();
                tempFactory.GetTempData(context.HttpContext)["Error"] = "Your session has expired. Please log in again.";
                context.Result = new RedirectToActionResult("Login", "Account", null);
                break;

            case ApiUnavailableException ex:
                logger.LogError(ex, "RaceDay API unreachable");
                context.Result = ErrorPage(context, "Service unavailable",
                    "We couldn't reach the RaceDay service. Please try again in a moment.", 503);
                break;

            default:
                logger.LogError(context.Exception, "Unhandled error");
                context.Result = ErrorPage(context, "Something went wrong",
                    "An unexpected problem occurred. Please try again.", 500);
                break;
        }
        context.ExceptionHandled = true;
    }

    private static ViewResult ErrorPage(ExceptionContext ctx, string title, string message, int status) => new()
    {
        ViewName = "Error",
        StatusCode = status,
        ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                                          new ModelStateDictionary())
        {
            Model = new ErrorViewModel(title, message)
        }
    };
}
