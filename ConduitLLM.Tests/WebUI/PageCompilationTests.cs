using System;
using System.Linq;
using System.Reflection;
using ConduitLLM.WebUI.Components.Pages;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ConduitLLM.Tests.WebUI
{
    /// <summary>
    /// Tests to verify that all WebUI pages can be compiled and instantiated without errors.
    /// </summary>
    public class PageCompilationTests
    {
        [Fact]
        public void AllPagesInWebUI_CanBeInstantiated()
        {
            // Get the assembly containing the pages
            var webUIAssembly = typeof(Home).Assembly;
            
            // Find all page components (classes that inherit from ComponentBase and are in Pages namespace)
            var pageTypes = webUIAssembly.GetTypes()
                .Where(t => t.Namespace != null && 
                           t.Namespace.Contains("Pages") &&
                           typeof(ComponentBase).IsAssignableFrom(t) &&
                           !t.IsAbstract &&
                           t.IsPublic)
                .ToList();

            // Verify we found pages
            Assert.NotEmpty(pageTypes);
            
            // Try to create an instance of each page
            foreach (var pageType in pageTypes)
            {
                try
                {
                    // This will throw if the page has constructor issues
                    var instance = Activator.CreateInstance(pageType);
                    Assert.NotNull(instance);
                }
                catch (Exception ex)
                {
                    // If a page can't be instantiated, fail with helpful message
                    Assert.Fail($"Failed to instantiate page {pageType.Name}: {ex.Message}");
                }
            }
        }

        [Theory]
        [InlineData(typeof(Home))]
        [InlineData(typeof(About))]
        [InlineData(typeof(Login))]
        [InlineData(typeof(AccessDenied))]
        [InlineData(typeof(Error))]
        [InlineData(typeof(AdminApiError))]
        [InlineData(typeof(AdminApiAuthError))]
        [InlineData(typeof(Chat))]
        [InlineData(typeof(ConduitLLM.WebUI.Components.Pages.Configuration))]
        [InlineData(typeof(CostDashboard))]
        [InlineData(typeof(VirtualKeys))]
        [InlineData(typeof(RequestLogs))]
        [InlineData(typeof(ModelCosts))]
        [InlineData(typeof(ProviderHealth))]
        [InlineData(typeof(SystemInfo))]
        [InlineData(typeof(IpAccessFiltering))]
        public void Page_CanBeInstantiated(Type pageType)
        {
            // Act
            var instance = Activator.CreateInstance(pageType);
            
            // Assert
            Assert.NotNull(instance);
            Assert.IsAssignableFrom<ComponentBase>(instance);
        }

        [Fact]
        public void AllPages_HaveProperNaming()
        {
            // Get the assembly containing the pages
            var webUIAssembly = typeof(Home).Assembly;
            
            // Find all page components
            var pageTypes = webUIAssembly.GetTypes()
                .Where(t => t.Namespace != null && 
                           t.Namespace.Contains("Pages") &&
                           typeof(ComponentBase).IsAssignableFrom(t) &&
                           !t.IsAbstract &&
                           t.IsPublic)
                .ToList();

            foreach (var pageType in pageTypes)
            {
                // Verify page names don't contain spaces or special characters
                Assert.Matches(@"^[A-Za-z][A-Za-z0-9]*$", pageType.Name);
                
                // Verify pages are in the correct namespace
                Assert.Contains("ConduitLLM.WebUI.Components.Pages", pageType.Namespace);
            }
        }

        [Fact]
        public void AllPages_ArePublicClasses()
        {
            // Get the assembly containing the pages
            var webUIAssembly = typeof(Home).Assembly;
            
            // Find all page components
            var pageTypes = webUIAssembly.GetTypes()
                .Where(t => t.Namespace != null && 
                           t.Namespace.Contains("Pages") &&
                           typeof(ComponentBase).IsAssignableFrom(t) &&
                           !t.IsAbstract)
                .ToList();

            foreach (var pageType in pageTypes)
            {
                Assert.True(pageType.IsPublic, $"Page {pageType.Name} should be public");
                Assert.True(pageType.IsClass, $"Page {pageType.Name} should be a class");
            }
        }
    }
}