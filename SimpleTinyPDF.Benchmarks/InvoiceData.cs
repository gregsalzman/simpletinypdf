namespace SimpleTinyPDF.Benchmarks;

public static class InvoiceData
{
    public const string CompanyName = "Acme Corp";
    public const string CompanyAddress = "123 Main Street, Springfield";
    public const string CompanyPhone = "Tel: (555) 123-4567";

    public const string BillToName = "John Smith";
    public const string BillToAddress = "456 Oak Avenue";
    public const string BillToCityState = "Shelbyville, IL 62565";

    public static readonly (string Desc, string Qty, string Price, string Amount)[] LineItems =
    {
        ("Web Development Services", "40 hrs", "$75.00", "$3,000.00"),
        ("UI/UX Design", "16 hrs", "$85.00", "$1,360.00"),
        ("Hosting Setup", "1", "$200.00", "$200.00"),
    };

    public const string Subtotal = "$4,560.00";
    public const string Tax = "$364.80";
    public const string Total = "$4,924.80";
    public const string FooterNote = "Payment is due within 30 days. Thank you for your business!";
    public const int BatchSize = 1_000;
}
