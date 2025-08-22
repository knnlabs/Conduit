namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
        #region Health Management Tests

        // UpdateModelHealth tests removed - provider health monitoring has been removed

        #endregion

        #region GetAvailableModelDetailsAsync Tests

        [Fact]
        public async Task GetAvailableModelDetailsAsync_ReturnsModelInfo()
        {
            // Arrange
            InitializeRouterWithModels();

            // Act
            var models = await _router.GetAvailableModelDetailsAsync();

            // Assert
            Assert.Equal(2, models.Count);
            var gpt4 = models.FirstOrDefault(m => m.Id == "gpt-4");
            Assert.NotNull(gpt4);
            Assert.Equal("openai/gpt-4", gpt4.OwnedBy);
        }

        #endregion
    }
}