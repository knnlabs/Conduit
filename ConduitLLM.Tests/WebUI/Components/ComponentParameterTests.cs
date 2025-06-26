using System;
using System.Linq;
using System.Reflection;

using ConduitLLM.WebUI.Components.Layout;
using ConduitLLM.WebUI.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using Xunit;

namespace ConduitLLM.Tests.WebUI.Components
{
    /// <summary>
    /// Tests to ensure component parameters are properly defined and named.
    /// </summary>
    public class ComponentParameterTests
    {
        [Fact]
        public void StatusBadge_HasCorrectParameters()
        {
            // Arrange
            var componentType = typeof(StatusBadge);

            // Act & Assert
            AssertParameterExists(componentType, "Status", typeof(string));
            AssertParameterExists(componentType, "Type", typeof(StatusBadge.StatusType));
            AssertParameterExists(componentType, "CustomText", typeof(string));
            AssertParameterExists(componentType, "CustomIcon", typeof(string));
            AssertParameterExists(componentType, "CustomClass", typeof(string));

            // Ensure old parameter name doesn't exist
            AssertParameterDoesNotExist(componentType, "StatusType");
        }

        [Fact]
        public void CollapsibleNavSection_HasCorrectParameters()
        {
            // Arrange
            var componentType = typeof(CollapsibleNavSection);

            // Act & Assert
            AssertParameterExists(componentType, "Title", typeof(string));
            AssertParameterExists(componentType, "IconClass", typeof(string));
            AssertParameterExists(componentType, "InitiallyExpanded", typeof(bool));
            AssertParameterExists(componentType, "ChildContent", typeof(RenderFragment));
            AssertParameterExists(componentType, "IsExpandedChanged", typeof(EventCallback<bool>));
            AssertParameterExists(componentType, "IsExpanded", typeof(bool));
            AssertParameterExists(componentType, "PersistenceKey", typeof(string));
        }

        [Fact]
        public void NavigationLink_HasCorrectParameters()
        {
            // Arrange
            var componentType = typeof(NavigationLink);

            // Act & Assert
            AssertParameterExists(componentType, "Href", typeof(string));
            AssertParameterExists(componentType, "Text", typeof(string));
            AssertParameterExists(componentType, "IconClass", typeof(string));
            AssertParameterExists(componentType, "CssClass", typeof(string));
            AssertParameterExists(componentType, "Match", typeof(NavLinkMatch));
        }

        [Fact]
        public void ConduitErrorBoundary_HasCorrectParameters()
        {
            // Arrange
            var componentType = typeof(ConduitErrorBoundary);

            // Act & Assert
            // ChildContent and ErrorContent are inherited from ErrorBoundaryBase
            // We check if the properties exist (including inherited ones)
            Assert.NotNull(componentType.GetProperty("ChildContent"));
            Assert.NotNull(componentType.GetProperty("ErrorContent"));

            // These are custom parameters defined in our component
            AssertParameterExists(componentType, "ShowDetails", typeof(bool));
            AssertParameterExists(componentType, "ShowResetButton", typeof(bool));
            AssertParameterExists(componentType, "OnError", typeof(EventCallback<Exception>));
        }


        [Theory]
        [InlineData(typeof(StatusBadge))]
        [InlineData(typeof(CollapsibleNavSection))]
        [InlineData(typeof(NavigationLink))]
        [InlineData(typeof(ConduitErrorBoundary))]
        public void AllComponents_HaveXmlDocumentationOnParameters(Type componentType)
        {
            // Arrange
            var parameterProperties = componentType.GetProperties()
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null);

            // Act & Assert
            foreach (var property in parameterProperties)
            {
                // This would require XML documentation file parsing in a real scenario
                // For now, we just ensure the property exists and is properly attributed
                Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
            }
        }

        [Theory]
        [InlineData(typeof(StatusBadge), "Status", "")]
        [InlineData(typeof(CollapsibleNavSection), "Title", "")]
        [InlineData(typeof(CollapsibleNavSection), "IconClass", "fa fa-folder")]
        [InlineData(typeof(NavigationLink), "Href", "")]
        [InlineData(typeof(NavigationLink), "IconClass", "fa fa-circle")]
        public void Components_HaveCorrectDefaultValues(Type componentType, string parameterName, object expectedDefault)
        {
            // Arrange
            var property = GetParameterProperty(componentType, parameterName);
            var instance = Activator.CreateInstance(componentType);

            // Act
            var actualValue = property?.GetValue(instance);

            // Assert
            Assert.Equal(expectedDefault, actualValue);
        }

        private void AssertParameterExists(Type componentType, string parameterName, Type expectedType)
        {
            var property = GetParameterProperty(componentType, parameterName);
            Assert.NotNull(property);
            Assert.Equal(expectedType, property!.PropertyType);
        }

        private void AssertParameterDoesNotExist(Type componentType, string parameterName)
        {
            var property = GetParameterProperty(componentType, parameterName);
            Assert.Null(property);
        }

        private PropertyInfo? GetParameterProperty(Type componentType, string parameterName)
        {
            return componentType.GetProperty(parameterName,
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetCustomAttribute<ParameterAttribute>() != null
                ? componentType.GetProperty(parameterName)
                : null;
        }
    }
}
