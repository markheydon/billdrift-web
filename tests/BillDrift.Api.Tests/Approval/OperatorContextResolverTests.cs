using System.Security.Claims;
using BillDrift.Api.Approval;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace BillDrift.Api.Tests.Approval;

public sealed class OperatorContextResolverTests
{
    [Fact]
    public void Spoofed_headers_are_ignored_outside_development()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Operator-Id"] = "attacker";
        httpContext.Request.Headers["X-Operator-ReadOnly"] = "false";

        var context = OperatorContextResolver.Resolve(httpContext, Env("Production"));

        context.CanApprove.Should().BeFalse();
        context.OperatorId.Should().Be("system");
    }

    [Fact]
    public void Dev_headers_are_honored_only_in_development()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Operator-Id"] = "dev-operator";

        var context = OperatorContextResolver.Resolve(httpContext, Env("Development"));

        context.CanApprove.Should().BeTrue();
        context.OperatorId.Should().Be("dev-operator");
    }

    [Fact]
    public void Read_only_dev_header_disables_approval()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Operator-Id"] = "dev-operator";
        httpContext.Request.Headers["X-Operator-ReadOnly"] = "true";

        var context = OperatorContextResolver.Resolve(httpContext, Env("Development"));

        context.CanApprove.Should().BeFalse();
        context.OperatorId.Should().Be("dev-operator");
    }

    [Fact]
    public void Authenticated_approver_role_grants_write_access_regardless_of_headers()
    {
        var httpContext = new DefaultHttpContext { User = Principal("op-42", OperatorContextResolver.ApproverRole) };
        // A spoofed read-only header must not override the authenticated principal.
        httpContext.Request.Headers["X-Operator-ReadOnly"] = "true";

        var context = OperatorContextResolver.Resolve(httpContext, Env("Production"));

        context.CanApprove.Should().BeTrue();
        context.OperatorId.Should().Be("op-42");
    }

    [Fact]
    public void Authenticated_user_without_approver_role_cannot_approve()
    {
        var httpContext = new DefaultHttpContext { User = Principal("viewer-1") };

        var context = OperatorContextResolver.Resolve(httpContext, Env("Production"));

        context.CanApprove.Should().BeFalse();
        context.OperatorId.Should().Be("viewer-1");
    }

    private static ClaimsPrincipal Principal(string subject, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static IHostEnvironment Env(string environmentName) =>
        new FakeHostEnvironment { EnvironmentName = environmentName };

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "BillDrift.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
