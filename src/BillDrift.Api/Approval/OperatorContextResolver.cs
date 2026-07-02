using System.Security.Claims;
using BillDrift.Application.Approval;

namespace BillDrift.Api.Approval;

/// <summary>
/// Resolves operator identity and write permission entirely server-side.
/// <para>
/// Authenticated <see cref="ClaimsPrincipal"/> claims are authoritative. The
/// <c>X-Operator-Id</c> / <c>X-Operator-ReadOnly</c> request headers are a local convenience
/// honored <b>only</b> in the Development environment; they are never trusted otherwise, so a
/// caller cannot spoof operator identity or approval rights in a deployed environment.
/// </para>
/// </summary>
public static class OperatorContextResolver
{
    /// <summary>Role that grants approval write access once real authentication is wired in.</summary>
    public const string ApproverRole = "ApprovalOperator";

    private const string OperatorIdHeader = "X-Operator-Id";
    private const string ReadOnlyHeader = "X-Operator-ReadOnly";

    /// <summary>Resolves the operator context for the current request.</summary>
    public static IOperatorContext Resolve(HttpContext? httpContext, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var operatorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.Identity.Name;

            // Authorization is a server-side decision based on the authenticated principal's role,
            // never on request-supplied data.
            return new OperatorContext(operatorId, canApprove: user.IsInRole(ApproverRole));
        }

        if (httpContext is not null && environment.IsDevelopment())
        {
            var headerOperatorId = httpContext.Request.Headers[OperatorIdHeader].FirstOrDefault();
            var readOnly = string.Equals(
                httpContext.Request.Headers[ReadOnlyHeader].FirstOrDefault(),
                "true",
                StringComparison.OrdinalIgnoreCase);

            return new OperatorContext(headerOperatorId, canApprove: !readOnly);
        }

        // No authenticated identity outside Development: read-only, cannot mutate.
        return new OperatorContext(operatorId: null, canApprove: false);
    }
}
