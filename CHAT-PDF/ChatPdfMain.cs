using System.Diagnostics;
using System.Text;

namespace CHAT_PDF
{
    class ChatPdfMain
    {
        internal static string? _apiKey = null;
        private static string apiKeyFilePath = Path.Combine(Directory.GetCurrentDirectory(), "api_key.secret");
        private static string docsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "docs");

        // --- Método Principal ---
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8; // Para mostrar correctamente caracteres especiales
            Console.WriteLine("--- Chatbot de PDFs con Google Gemini ---");

            APIKEY_PATH();
            DOCS_PATH();

            List<string> pdfFiles = FileManager.FindPdfFiles(docsDirectory);
            FileManager.PrintPDFs(pdfFiles, docsDirectory);

            GET_API();
            string combinedText = await GET_TEXT(pdfFiles);

            Console.WriteLine($"Extracción completada. Total de caracteres: {combinedText.Length}");
            Console.WriteLine("\n--- ¡Listo para chatear! ---");
            Console.WriteLine("Escribe tu pregunta sobre el contenido de los PDFs o escribe 'salir' para terminar.");

            // Bucle del Chatbot
            await Gemini.RunChatbotLoop(combinedText);

            Console.WriteLine("\n¡Hasta luego!");
            Environment.Exit(0);    // Salir del programa
        }

        private static void APIKEY_PATH()
        {
            if (!File.Exists(apiKeyFilePath))
            {
                Console.WriteLine("El archivo 'api_key.secret' no existe. Creándolo...");
                try
                {
                    File.WriteAllText(apiKeyFilePath, string.Empty); // Crea un archivo vacío
                    Console.WriteLine("Archivo 'api_key.secret' creado exitosamente. Por favor, agrega tu API Key en este archivo.");
                    Environment.Exit(0);    // Salir del programa
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al crear el archivo 'api_key.secret': {ex.Message}");
                    Environment.Exit(-1);    // Salir del programa
                }
            }
        }

        private static void DOCS_PATH()
        {
            if (!Directory.Exists(docsDirectory))
            {
                Directory.CreateDirectory(docsDirectory);
                Console.WriteLine($"Directorio 'docs' creado en: {docsDirectory}, copia los documentos al directorio.");
                Environment.Exit(0);    // Salir del programa
            }
        }

        private static void GET_API()
        {
            if (File.Exists(apiKeyFilePath))
            {
                try
                {
                    _apiKey = File.ReadAllText(apiKeyFilePath).Trim();
                    if (string.IsNullOrWhiteSpace(_apiKey))
                    {
                        Console.WriteLine("El archivo 'api_key.secret' está vacío. Por favor, agrega tu API Key.");
                        Environment.Exit(0);    // Salir del programa
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al leer el archivo 'api_key.secret': {ex.Message}");
                    Environment.Exit(-2);    // Salir del programa
                }
            }
        }

        private static async Task<string> GET_TEXT(List<string> pdfFiles)
        {
            string combinedText = await FileManager.ExtractTextFromPdfsAsync(pdfFiles);
            Console.WriteLine("\nExtrayendo texto de los archivos PDF...");

            if (string.IsNullOrWhiteSpace(combinedText))
            {
                Console.WriteLine("No se pudo extraer texto de ningún PDF.");
                Environment.Exit(-3);    // Salir del programa
            }
            return combinedText;
        }
    }
}