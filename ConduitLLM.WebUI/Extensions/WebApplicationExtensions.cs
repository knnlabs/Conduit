using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.IO;

namespace ConduitLLM.WebUI.Extensions
{
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Maps static assets for the application
        /// </summary>
        public static IApplicationBuilder MapStaticAssets(this IApplicationBuilder app)
        {
            // This is mostly a no-op since UseStaticFiles is already called in Program.cs
            return app;
        }
    }
}