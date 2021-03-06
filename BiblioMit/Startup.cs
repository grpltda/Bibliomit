﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BiblioMit.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BiblioMit.Models;
using BiblioMit.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using BiblioMit.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO;
using AspNetCore.IServiceCollection.AddIUrlHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Threading.Tasks;
//using Schema.NET;
//using WebMarkupMin.AspNetCore2;
//using Microsoft.AspNetCore.CookiePolicy;
//using System.Diagnostics.CodeAnalysis;

namespace BiblioMit
{
    public class Startup
    {
        private readonly string _os;
        private const string defaultCulture = "en";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            _os = Environment.OSVersion.Platform.ToString();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString($"{_os}Connection"),
                    sqlServerOptions => sqlServerOptions.CommandTimeout(10000)));

            services.AddHostedService<SeedBackground>();
            services.AddScoped<ISeed, SeedService>();
            services.AddScoped<INodeService, NodeService>();
            services.AddScoped<IImport, Import>();

            services.AddTransient<IEmailSender, EmailSender>();

            services.AddScoped<IForum, ForumService>();
            services.AddScoped<IPost, PostService>();
            services.AddScoped<IAppUser, AppUserService>();

            // Authorization handlers.
            services.AddScoped<IAuthorizationHandler, ContactIsOwnerAuthorizationHandler>();

            services.AddScoped<IAuthorizationHandler, ContactAdministratorsAuthorizationHandler>();

            services.AddScoped<IAuthorizationHandler, ContactManagerAuthorizationHandler>();

            services.AddScoped<IViewRenderService, ViewRenderService>();

            //services.AddRecaptcha(new RecaptchaOptions
            //{
            //    SiteKey = Configuration["6Ld3SGcUAAAAANRVkFCci9QZMf5fSbRzROLXEyZk"],
            //    SecretKey = Configuration["6Ld3SGcUAAAAAJdww1OzASUrSve3O8oZfLpfmGjy"]
            //});

            services.Configure<CookiePolicyOptions>(options =>
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true);
            services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.Cookie.Name = "BiblioMit";
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.LoginPath = "/Identity/Account/Login";
                // ReturnUrlParameter requires 
                //using Microsoft.AspNetCore.Authentication.Cookies;
                options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
                options.SlidingExpiration = true;
            });

            services.AddIdentity<AppUser, AppRole>(options =>
                options.SignIn.RequireConfirmedEmail = true)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultUI()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o =>
                {
                    o.LoginPath = new PathString("/login");
                    o.AccessDeniedPath = new PathString("/Account/AccessDenied");
                    o.LogoutPath = new PathString("/logout");
                });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo(defaultCulture),
                    new CultureInfo("es")
                };
                options.DefaultRequestCulture = new RequestCulture(culture: defaultCulture, uiCulture: defaultCulture);
                // Formatting numbers, dates, etc.
                options.SupportedCultures = supportedCultures;
                // UI strings that we have localized.
                options.SupportedUICultures = supportedCultures;

                options.AddInitialRequestCultureProvider(
                    new CustomRequestCultureProvider(
                        async context => 
                        {
                            return new ProviderCultureResult(defaultCulture);
                        }));
            });

            //services.AddScoped<ILibs, LibService>();

            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddViewLocalization(
                LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization()
                .AddNewtonsoftJson();

            services.ConfigureNonBreakingSameSiteCookies();

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(60);
            });

            services.AddHttpsRedirection(options =>
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect);

            services.Configure<AuthMessageSenderOptions>(Configuration);

            services.AddAuthorization(options =>
            {
                foreach (var item in ClaimData.UserClaims)
                {
                    options.AddPolicy(item, policy => policy.RequireClaim(item, item));
                }
            });

            services.AddUrlHelper();

            services.AddCors();

            services.AddSignalR(options => {
                options.EnableDetailedErrors = true;
            });

            //services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

            //services.AddWebOptimizer(pipeline =>
            //{
            //    pipeline.MinifyJsFiles();
            //    pipeline.MinifyCssFiles();
            //    pipeline.MinifyHtmlFiles();
            //});

            //services.AddWebMarkupMin()
            //    .AddHtmlMinification(
            //        options =>
            //        {
            //            options.MinificationSettings.RemoveRedundantAttributes = true;
            //            options.MinificationSettings.RemoveHttpProtocolFromAttributes = true;
            //            options.MinificationSettings.RemoveHttpsProtocolFromAttributes = true;
            //        })
            //    .AddXmlMinification()
            //    .AddHttpCompression();

            Libman.LoadJson();
            Bundler.LoadJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (app == null || env == null) return;
            if (env.IsDevelopment())
            {
                //app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
                //{
                //    ProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "ClientApp"),
                //    HotModuleReplacement = true
                //});
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            var supportedCultures = new[]
            {
                new CultureInfo(defaultCulture),
                new CultureInfo("es"),
            };

            app.UseSitemapMiddleware();
            app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

            app.UseDefaultFiles();

            var path = new List<string> { "lib", "cldr-data", "main" };

            var ch = _os == "Win32NT" ? @"\" : "/";

            var di = new DirectoryInfo(Path.Combine(env?.WebRootPath, string.Join(ch, path)));

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                // Formatting numbers, dates, etc.
                SupportedCultures = supportedCultures,
                // UI strings that we have localized.
                SupportedUICultures = supportedCultures
            });

            app.UseHttpsRedirection();

            //app.UseWebOptimizer();

            app.UseDefaultFiles();

            app.UseStaticFiles();

            app.UseCookiePolicy();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            //app.UseWebMarkupMin();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages().RequireAuthorization();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                .RequireAuthorization();
                endpoints.MapHub<ChatHub>("/chatHub");
                endpoints.MapHub<EntryHub>("/entryHub").RequireAuthorization();
                endpoints.MapHub<ProgressHub>("/progressHub").RequireAuthorization();
            });
        }
    }
}
