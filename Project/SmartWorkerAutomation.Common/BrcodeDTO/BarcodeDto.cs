using SmartWorkerAutomation.Common.Enum;
using System.Text.Json.Serialization;

namespace SmartWorkerAutomation.Common.BrcodeDTO;

public class CodeResponse
{
    public string BarcodeId { get; set; }
    public string BarcodeImage { get; set; }
    public byte[] BarImageBytes { get; set; }

    public string QrCodeId { get; set; }
    public string QrCodeImage { get; set; }
    public byte[] QrImageBytes { get; set; }
}
public interface ICodeRequest
{
    int PurchaseItemId { get; }
    decimal SellingPrice { get; }
    string SalesTaxId { get; }
    string ColorName { get; }
    string SizeName { get; }
}
public class CodeRequest : ICodeRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CodeType CodeType { get; set; }
    public int PurchaseItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string SalesTaxId { get; set; }
    public string ColorName { get; set; }
    public string SizeName { get; set; }
}

public class CustomCodeRequest : ICodeRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CodeType CodeType { get; set; }
    public int PurchaseItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string SalesTaxId { get; set; }
    public string ColorName { get; set; }
    public string SizeName { get; set; }
    public string Supplier { get; set; }
}
public class BulkCodeRequest : ICodeRequest
{
    public int PurchaseItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string SalesTaxId { get; set; }
    public string ColorName { get; set; }
    public string SizeName { get; set; }
    public int Count { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CodeType CodeType { get; set; } // 1=Barcode, 2=QR, 3=Both
}

public class BarcodeResponse
{
    public string BarcodeId { get; set; }
    public string BarcodeImage { get; set; }
}
public class BarcodeBulkResponse
{
    public string BarcodeId { get; set; }
    public string BarcodeImages { get; set; }
}

public class BarcodeGenerateRequest
{
    public string Key { get; set; }
    public string BarcodeText { get; set; }
}

public class BarcodeImageResponse
{
    public int Index { get; set; }
    public string Image { get; set; }   // base64 string
}