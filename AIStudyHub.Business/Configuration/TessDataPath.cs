public class OcrOptions
{
    public string TessDataPath { get; set; } = @"C:\Program Files\Tesseract-OCR\tessdata";
    public string Languages { get; set; } = "eng+vie";
    public int Dpi { get; set; } = 300;
}