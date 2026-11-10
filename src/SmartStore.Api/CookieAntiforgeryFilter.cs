using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;


internal sealed class CookieAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // AuthController keeps its explicit MVC antiforgery policy.
        if (context.ActionDescriptor.FilterDescriptors.Any(descriptor =>
            descriptor.Filter is AutoValidateAntiforgeryTokenAttribute)) return;

        var http = context.HttpContext;
        var method = http.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)) return;

        var authentication = await http.AuthenticateAsync();
        if (authentication.Ticket?.AuthenticationScheme == JwtBearerDefaults.AuthenticationScheme) return;

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new AntiforgeryValidationFailedResult();
        }
    }
}
