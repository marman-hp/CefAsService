using System;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class AdminAuthEndpoints
    {
        private const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        private const string StampClaim = "pwstamp";

        public static void AddAdminAuth(this IServiceCollection services)
        {
            services.TryAddSingleton<AdminSettingsStore>();
            services.AddSingleton<AdminAuth>();
            services.AddAuthentication(Scheme).AddCookie(options =>
            {
                options.Cookie.Name = "CefGlue.Admin.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.LoginPath = "/login";
                options.Events.OnValidatePrincipal = context =>
                {
                    var auth = context.HttpContext.RequestServices.GetRequiredService<AdminAuth>();
                    var stamp = context.Principal?.FindFirst(StampClaim)?.Value;
                    if (stamp == null || stamp != auth.PasswordStamp)
                    {
                        context.RejectPrincipal();
                        return context.HttpContext.SignOutAsync(Scheme);
                    }

                    return Task.CompletedTask;
                };
            });
        }

        public static void UseRequireLogin(this WebApplication app)
        {
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/login") || path.StartsWithSegments("/setup") || path.StartsWithSegments("/logout")
                    || context.User.Identity?.IsAuthenticated == true)
                {
                    await next();
                    return;
                }

                var auth = context.RequestServices.GetRequiredService<AdminAuth>();
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    context.Response.Redirect(auth.HasPassword ? "/login" : "/setup");
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
            });
        }

        public static void MapAdminAuthEndpoints(this WebApplication app)
        {
            app.MapGet("/", () => Results.Redirect("/admin"));

            app.MapGet("/setup", (HttpContext context, AdminAuth auth, IAntiforgery antiforgery) =>
                auth.HasPassword
                    ? Results.Redirect("/login")
                    : Page(context, antiforgery, "Create admin password", "/setup", null,
                        $"First run: choose the password for this Admin page (at least {AdminAuth.MinPasswordLength} characters).",
                        confirmField: true));

            app.MapPost("/setup", async (HttpContext context, AdminAuth auth, IAntiforgery antiforgery) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    return Results.BadRequest();
                }

                if (auth.HasPassword)
                {
                    return Results.Redirect("/login");
                }

                var form = await context.Request.ReadFormAsync();
                var error = AdminAuth.ValidateNewPassword(form["password"], form["confirm"]);
                if (error != null)
                {
                    return Page(context, antiforgery, "Create admin password", "/setup", error, null, confirmField: true);
                }

                auth.SetPassword(form["password"]);
                Console.WriteLine("[Admin] Admin password created.");
                await SignInAsync(context, auth);
                return Results.Redirect("/admin");
            });

            app.MapGet("/login", (HttpContext context, AdminAuth auth, IAntiforgery antiforgery) =>
                !auth.HasPassword
                    ? Results.Redirect("/setup")
                    : context.User.Identity?.IsAuthenticated == true
                        ? Results.Redirect("/admin")
                        : Page(context, antiforgery, "Admin login", "/login", null, null, confirmField: false));

            app.MapPost("/login", async (HttpContext context, AdminAuth auth, IAntiforgery antiforgery) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    return Results.BadRequest();
                }

                if (auth.LockoutRemaining is { } remaining)
                {
                    return Page(context, antiforgery, "Admin login", "/login",
                        $"Too many failed attempts - try again in {Math.Ceiling(remaining.TotalSeconds)}s.", null, confirmField: false);
                }

                var form = await context.Request.ReadFormAsync();
                if (!auth.TryLogin(form["password"]))
                {
                    return Page(context, antiforgery, "Admin login", "/login", "Wrong password.", null, confirmField: false);
                }

                await SignInAsync(context, auth);
                return Results.Redirect("/admin");
            });

            app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    return Results.BadRequest();
                }

                await context.SignOutAsync(Scheme);
                return Results.Redirect("/login");
            });
        }

        private static Task SignInAsync(HttpContext context, AdminAuth auth)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(StampClaim, auth.PasswordStamp),
            }, Scheme);

            return context.SignInAsync(Scheme, new ClaimsPrincipal(identity));
        }

        private static IResult Page(HttpContext context, IAntiforgery antiforgery, string title, string action,
            string error, string hint, bool confirmField)
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            var enc = HtmlEncoder.Default;
            var html = new StringBuilder();
            html.Append($$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>CefGlue Broker - {{enc.Encode(title)}}</title>
                <style>
                  /* Same dark palette as Admin.razor.css, so login -> admin doesn't flash a different look. */
                  :root { --bg:#14181f; --card:#1c2129; --text:#e6e9ee; --muted:#8b96a3; --border:#2a323d; --accent:#e0975a; --error:#e0665a; color-scheme:dark; }
                  * { box-sizing:border-box; }
                  body { margin:0; min-height:100vh; display:grid; place-items:center; background:var(--bg); color:var(--text); font:15px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; padding:16px; }
                  form { width:100%; max-width:360px; background:var(--card); border:1px solid var(--border); border-radius:10px; padding:28px; display:grid; gap:14px; }
                  h1 { margin:0; font-size:20px; }
                  p { margin:0; color:var(--muted); font-size:14px; }
                  label { display:grid; gap:6px; font-size:14px; }
                  input[type=password] { width:100%; padding:9px 11px; border:1px solid var(--border); border-radius:6px; background:#191e25; color:var(--text); font-size:15px; }
                  input[type=password]:focus { outline:1px solid var(--accent); border-color:var(--accent); }
                  button { padding:10px; border:0; border-radius:6px; background:var(--accent); color:#1c1210; font-size:15px; font-weight:600; cursor:pointer; }
                  .error { color:var(--error); }
                </style>
                </head>
                <body>
                <form method="post" action="{{action}}">
                  <h1>{{enc.Encode(title)}}</h1>
                """);

            if (hint != null)
            {
                html.Append($"<p>{enc.Encode(hint)}</p>");
            }

            if (error != null)
            {
                html.Append($"<p class=\"error\" role=\"alert\">{enc.Encode(error)}</p>");
            }

            html.Append($"<input type=\"hidden\" name=\"{enc.Encode(tokens.FormFieldName)}\" value=\"{enc.Encode(tokens.RequestToken)}\" />");
            html.Append("<label>Password<input type=\"password\" name=\"password\" autocomplete=\"" + (confirmField ? "new-password" : "current-password") + "\" autofocus required /></label>");

            if (confirmField)
            {
                html.Append("<label>Confirm password<input type=\"password\" name=\"confirm\" autocomplete=\"new-password\" required /></label>");
            }

            html.Append($"<button type=\"submit\">{(confirmField ? "Create password" : "Log in")}</button></form></body></html>");

            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Cache-Control"] = "no-store";
            return Results.Content(html.ToString(), "text/html; charset=utf-8");
        }
    }
}
