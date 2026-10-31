using Xunit;

// CI runs unit and integration tests in separate steps, selected by this trait: --filter "Category=Unit".
[assembly: Trait("Category", "Unit")]
