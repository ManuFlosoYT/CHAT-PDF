using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;

namespace CHAT_PDF
{
    internal class FileManager
    {
        internal static List<string> FindPdfFiles(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error buscando archivos PDF: {ex.Message}");
                return new List<string>();
            }
        }

        internal static void PrintPDFs(List<string> pdfFiles, string docsDirectory)
        {
            Console.WriteLine($"Buscando archivos PDF en: {docsDirectory}");
            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("No se encontraron archivos PDF en este directorio.");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
                Environment.Exit(0);    // Salir del programa
            }
            Console.WriteLine($"Se encontraron {pdfFiles.Count} archivos PDF:");
            pdfFiles.ForEach(f => Console.WriteLine($"- {Path.GetFileName(f)}"));
        }

        // Extrae texto de una lista de archivos PDF de forma asíncrona
        // para no bloquear el hilo principal con la lectura de archivos
        internal static async Task<string> ExtractTextFromPdfsAsync(List<string> pdfFilePaths)
        {
            StringBuilder allText = new StringBuilder();

            foreach (var filePath in pdfFilePaths)
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"- Procesando: {fileName}");
                try
                {
                    string? fileText = await Task.Run(() =>
                    {
                        try
                        {
                            using (PdfDocument document = PdfDocument.Open(filePath))
                            {
                                StringBuilder pdfTextBuilder = new StringBuilder();
                                // Separador para indicar el inicio de un nuevo documento
                                pdfTextBuilder.AppendLine($"--- INICIO DOCUMENTO: {fileName} ---");
                                foreach (Page page in document.GetPages())
                                {
                                    pdfTextBuilder.AppendLine($"--- Página {page.Number} ---");
                                    pdfTextBuilder.AppendLine(page.Text);
                                }
                                pdfTextBuilder.AppendLine($"--- FIN DOCUMENTO: {fileName} ---");
                                return pdfTextBuilder.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Error al procesar '{fileName}': {ex.Message}. Saltando archivo.");
                            return null; // Devuelve null si hay error en este archivo
                        }
                    });

                    if (!string.IsNullOrEmpty(fileText))
                    {
                        allText.Append(fileText);
                        allText.AppendLine("\n"); // Espacio entre textos de diferentes archivos
                    }
                }
                catch (Exception ex) // Captura errores generales por si acaso
                {
                    Console.WriteLine($"  Error inesperado procesando '{fileName}': {ex.Message}");
                }
            }
            return allText.ToString();
        }
    }
}
