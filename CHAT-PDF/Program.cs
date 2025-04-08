using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfChatbotGemini
{
    class Program
    {
        // --- Configuración ---
        //gemini-1.5-flash-latest
        private const string GeminiModel = "gemini-1.5-flash-latest";
        private static string? _apiKey = null; // Se pedirá al usuario
        private static HttpClient? _httpClient;

        // --- Clases para la Deserialización de la Respuesta de Gemini ---
        // Estructura simplificada basada en la respuesta de generateContent
        // Puedes necesitar ajustarla según la versión de la API o si usas streaming
        private record GeminiResponse(Candidate[]? candidates, PromptFeedback? promptFeedback);
        private record Candidate(Content? content, string? finishReason, SafetyRating[]? safetyRatings);
        private record Content(Part[]? parts, string? role);
        private record Part(string? text);
        private record PromptFeedback(SafetyRating[]? safetyRatings);
        private record SafetyRating(string? category, string? probability);


        // --- Método Principal ---
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8; // Para mostrar correctamente caracteres especiales
            Console.WriteLine("--- Chatbot de PDFs con Google Gemini ---");

            // Crear el archivo api_key.secret si no existe
            string apiKeyFilePath = Path.Combine(Directory.GetCurrentDirectory(), "api_key.secret");
            if (!File.Exists(apiKeyFilePath))
            {
                Console.WriteLine("El archivo 'api_key.secret' no existe. Creándolo...");
                try
                {
                    File.WriteAllText(apiKeyFilePath, string.Empty); // Crea un archivo vacío
                    Console.WriteLine("Archivo 'api_key.secret' creado exitosamente. Por favor, agrega tu API Key en este archivo.");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al crear el archivo 'api_key.secret': {ex.Message}");
                    return;
                }
            }


            // Leer la API Key de un archivo llamado api_key.secret si existe
            if (File.Exists(apiKeyFilePath))
            {
                try
                {
                    _apiKey = File.ReadAllText(apiKeyFilePath).Trim();
                    if (string.IsNullOrWhiteSpace(_apiKey))
                    {
                        Console.WriteLine("El archivo 'api_key.secret' está vacío. Por favor, agrega tu API Key.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al leer el archivo 'api_key.secret': {ex.Message}");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Console.WriteLine("Error: API Key no proporcionada. Saliendo.");
                return;
            }

            // Inicializar HttpClient
            _httpClient = new HttpClient();

            // 2. Encontrar archivos PDF en el directorio actual
            //crea un directorio llamado docs si no existe
            string docsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "docs");
            if (!Directory.Exists(docsDirectory))
            {
                Directory.CreateDirectory(docsDirectory);
            }
            Console.WriteLine($"Buscando archivos PDF en: {docsDirectory}");
            List<string> pdfFiles = FindPdfFiles(docsDirectory);

            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("No se encontraron archivos PDF en este directorio.");
                return;
            }

            Console.WriteLine($"Se encontraron {pdfFiles.Count} archivos PDF:");
            pdfFiles.ForEach(f => Console.WriteLine($"- {Path.GetFileName(f)}"));

            // 3. Extraer texto de los PDFs
            Console.WriteLine("\nExtrayendo texto de los archivos PDF...");
            string combinedText = await ExtractTextFromPdfsAsync(pdfFiles);

            if (string.IsNullOrWhiteSpace(combinedText))
            {
                Console.WriteLine("No se pudo extraer texto de ningún PDF.");
                return;
            }

            Console.WriteLine($"Extracción completada. Total de caracteres: {combinedText.Length}");
            Console.WriteLine("\n--- ¡Listo para chatear! ---");
            Console.WriteLine("Escribe tu pregunta sobre el contenido de los PDFs o escribe 'salir' para terminar.");

            // 4. Bucle del Chatbot
            await RunChatbotLoop(combinedText);

            Console.WriteLine("\n¡Hasta luego!");
        }

        // --- Funciones Auxiliares ---

        /// <summary>
        /// Busca archivos PDF en el directorio especificado.
        /// </summary>
        static List<string> FindPdfFiles(string directory)
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

        /// <summary>
        /// Extrae texto de una lista de archivos PDF de forma asíncrona.
        /// </summary>
        static async Task<string> ExtractTextFromPdfsAsync(List<string> pdfFilePaths)
        {
            StringBuilder allText = new StringBuilder();

            foreach (var filePath in pdfFilePaths)
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"- Procesando: {fileName}");
                try
                {
                    // Usamos Task.Run para no bloquear el hilo principal con la lectura de archivos
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

        // Ejecuta el bucle principal del chatbot.
        static async Task RunChatbotLoop(string pdfContext)
        {
            string? userInput;
            while (true)
            {
                Console.Write("\nTú: ");
                userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput)) continue;
                if (userInput.Equals("salir", StringComparison.OrdinalIgnoreCase)) break;

                Console.WriteLine("Gemini pensando..."); // Feedback visual

                try
                {
                    string? geminiResponse = await CallGeminiApi(pdfContext, userInput);

                    if (geminiResponse != null)
                    {
                        Console.WriteLine($"\nGemini: {geminiResponse}");
                    }
                    else
                    {
                        Console.WriteLine("\nGemini: No he podido obtener una respuesta.");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    Console.WriteLine($"\nError de red al llamar a la API de Gemini: {httpEx.Message}");
                    if (httpEx.StatusCode.HasValue) Console.WriteLine($"Código de estado: {httpEx.StatusCode}");
                    // Podrías añadir lógica para reintentar aquí si es necesario
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"\nError al procesar la respuesta JSON de Gemini: {jsonEx.Message}");
                    // Esto puede indicar un problema con la estructura esperada vs la recibida
                }
                catch (Exception ex) // Captura cualquier otro error inesperado
                {
                    Console.WriteLine($"\nOcurrió un error inesperado: {ex.Message}");
                }
            }
        }


        /// <summary>
        /// Llama a la API de Google Gemini para obtener una respuesta basada en el contexto y la pregunta.
        /// </summary>
        static async Task<string?> CallGeminiApi(string context, string userQuery)
        {
            if (_httpClient == null || string.IsNullOrWhiteSpace(_apiKey))
            {
                Console.WriteLine("Error: HttpClient o API Key no inicializados.");
                return null;
            }

            // Construye la URL de la API (asegúrate de que sea la correcta para tu modelo y región)
            // Consulta la documentación de la API de Gemini para la URL correcta.
            string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent?key={_apiKey}";


            // Construye el prompt combinando el contexto y la pregunta del usuario
            // Es crucial darle una instrucción clara a la IA.
            string prompt = $"Basándote estrictamente en el siguiente texto extraído de varios documentos PDF, responde a la pregunta del usuario. Si la respuesta no se encuentra en el texto, indica que no tienes esa información en los documentos proporcionados.\n\n--- CONTEXTO EXTRAÍDO ---\n{context}\n--- FIN DEL CONTEXTO ---\n\nPREGUNTA DEL USUARIO:\n{userQuery}";


            // Construye el cuerpo de la solicitud JSON
            var requestBody = new
            {
                // La estructura puede variar ligeramente según el modelo y la versión de la API
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                // Opcional: Configuración de generación (ajusta según necesites)
                generationConfig = new
                {
                    temperature = 0.7, // Controla la creatividad (0=determinista, 1=más creativo)
                    topK = 40,
                    topP = 0.95,
                    // maxOutputTokens = 1024, // Límite de tokens en la respuesta
                    // stopSequences = new[] { "..." } // Secuencias para detener la generación
                },
                // Opcional: Configuración de seguridad
                safetySettings = new[] {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }

            };

            // Serializa el cuerpo de la solicitud a JSON
            string jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Realiza la solicitud POST
            //Console.WriteLine($"DEBUG: Enviando {jsonPayload.Length} caracteres a la API."); // Ayuda a depurar tamaño
            HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);

            // Lee la respuesta
            string responseBody = await response.Content.ReadAsStringAsync();

            // Verifica si la solicitud fue exitosa
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error en la API de Gemini: {response.StatusCode}");
                Console.WriteLine($"Respuesta: {responseBody}");
                // Lanza una excepción para que sea capturada en el bucle principal
                response.EnsureSuccessStatusCode();
            }

            // Deserializa la respuesta JSON
            try
            {
                //Console.WriteLine($"DEBUG: Respuesta API: {responseBody}"); // Descomenta para ver la respuesta completa
                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });


                // Extrae el texto de la respuesta
                // La estructura exacta puede variar, inspecciona la respuesta JSON si esto falla.
                string? resultText = geminiResponse?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text;

                // Verifica si hubo contenido bloqueado por seguridad
                if (string.IsNullOrEmpty(resultText))
                {
                    var firstCandidate = geminiResponse?.candidates?.FirstOrDefault();
                    if (firstCandidate?.finishReason == "SAFETY")
                    {
                        return "La respuesta fue bloqueada por motivos de seguridad según la configuración.";
                    }
                    // Verifica otros posibles finishReason si es necesario
                    if (firstCandidate?.finishReason == "MAX_TOKENS")
                    {
                        return "La respuesta generada excedió el límite máximo de tokens.";
                    }
                    if (firstCandidate?.finishReason != "STOP")
                    {
                        return $"La generación se detuvo por una razón inesperada: {firstCandidate?.finishReason}.";
                    }

                    // Si no hay texto y no es por seguridad, puede ser un error o respuesta vacía
                    var promptFeedbackReason = geminiResponse?.promptFeedback?.safetyRatings?.FirstOrDefault()?.category;
                    if (promptFeedbackReason != null)
                    {
                        return $"El prompt fue bloqueado por seguridad: {promptFeedbackReason}";
                    }

                    return "No se encontró texto en la respuesta de Gemini o la estructura es inesperada.";

                }


                return resultText?.Trim();
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"Error al deserializar la respuesta JSON: {jsonEx.Message}");
                Console.WriteLine($"Respuesta recibida: {responseBody}"); // Muestra la respuesta cruda para depurar
                return null;
            }
        }
    }
}