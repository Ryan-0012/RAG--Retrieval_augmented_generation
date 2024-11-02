using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

public class unstructuredPdf
{
    public string unstructured(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        
        using (PdfReader reader = new PdfReader(filePath))
        {
            var text = string.Empty;
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                // Extração de texto da página atual
                text += PdfTextExtractor.GetTextFromPage(reader, i);
            }
            return text;
        }
    }
}

