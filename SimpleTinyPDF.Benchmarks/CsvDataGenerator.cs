using System.Text;

namespace SimpleTinyPDF.Benchmarks;

public static class CsvDataGenerator
{
    public static string Generate(int rowCount = 10_000)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ID,Product,Category,Quantity,Unit Price,Total");

        var random = new Random(42);
        string[] products = { "Widget A", "Widget B", "Gadget Pro", "Sensor X",
                              "Module Y", "Adapter Z", "Cable Kit", "Power Supply" };
        string[] categories = { "Electronics", "Hardware", "Accessories", "Components" };

        for (int i = 1; i <= rowCount; i++)
        {
            var product = products[random.Next(products.Length)];
            var category = categories[random.Next(categories.Length)];
            var qty = random.Next(1, 100);
            var price = Math.Round(random.NextDouble() * 500 + 1, 2);
            var total = Math.Round(qty * price, 2);
            sb.AppendLine($"{i},{product},{category},{qty},{price:F2},{total:F2}");
        }

        return sb.ToString();
    }
}
