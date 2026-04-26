using System.Text.Json;

namespace Morpheus.UI
{
    public class ChartStateProjector
    {
        public static string ProjectTick(string symbol, double price, int volume)
        {
            // Using System.Text.Json to project lock-free
            return JsonSerializer.Serialize(new
            {
                symbol,
                price,
                volume
            });
        }
    }
}
