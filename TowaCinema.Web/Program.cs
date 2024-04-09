using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using TowaCinema.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt => { opt.Cookie.Name = "etaCarinaeArchiveCookies"; })
    .AddTwitch(opt =>
    {
        opt.ClientId = ClientVariables.TwitchAppClientId;
        opt.ClientSecret = ClientVariables.TwitchAppClientSecret;

        opt.CallbackPath = "/auth/twitch-token";

        opt.Events = new OAuthEvents
        {
            OnTicketReceived = ticket =>
            {
                var principal = ticket.Principal;

                if (principal is null) return Task.CompletedTask;

                var userId = principal.Claims.FirstOrDefault(w => w.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;

                var claims = new List<Claim>();
                ClaimsIdentity identity;

                var admins = ClientVariables.AdminIds.Split(",").Select(s => s.TrimStart().TrimEnd()).ToList();

                if (admins.Count > 0 && admins.Any(a => a == userId))
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));

                    identity = new ClaimsIdentity(claims);
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, "User"));

                    identity = new ClaimsIdentity(claims);
                }

                principal.AddIdentity(identity);

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddWebOptimizer(optimizer =>
{
    optimizer.AddCssBundle("/css/archive.css", "css/**/*.css");
    optimizer.AddJavaScriptBundle("/js/archive-1.js", "js/scroll.js");
    optimizer.AddJavaScriptBundle("/js/archive-2.js", "js/hls.min.js", "js/player.js");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next(context);
});

app.UseHttpsRedirection();
app.UseWebOptimizer();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapControllers();

app.Run();