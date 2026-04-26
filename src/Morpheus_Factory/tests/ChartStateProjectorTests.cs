using System.Text.Json;
using Morpheus.UI;

namespace Morpheus.Tests
{
    public class ChartStateProjectorTests
    {
        [Fact]
        public void ProjectTickShouldReturnValidJson()
        {
            ChartStateProjector projector = new ChartStateProjector();
            var json = ChartStateProjector.ProjectTick("NQZ4", 15000.50, 10);

            JsonDocument doc = JsonDocument.Parse(json);
            Assert.Equal("NQZ4", doc.RootElement.GetProperty("symbol").GetString());
            Assert.Equal(15000.50, doc.RootElement.GetProperty("price").GetDouble());
            Assert.Equal(10, doc.RootElement.GetProperty("volume").GetInt32());
        }
    }
}
