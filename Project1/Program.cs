using OllamaSharp;
using OllamaSharp.Models;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.Chroma;
using ChromaDB;
using Microsoft.SemanticKernel;
using OllamaSharp.Models.Chat;
using System.Text;
namespace project1;

public class Program
{
    
    public static string urlChromaDB = "http://localhost:8000";
    string model = "all-minilm";
    string modelChat = "llama3.2:1b";
    public static void Main(string[] args)
    {
        var pdfReader = new unstructuredPdf(); // Instancia a classe unstructuredPdf
        StoreChormaDB storeChormaDB = new StoreChormaDB(urlChromaDB);
        ReturnPoint:
        
        // Defina o caminho da pasta que você deseja listar
        string pasta = "../documents/"; 
        string[] arquivos = null;
        // Verifique se a pasta existe
        if (Directory.Exists(pasta))
        {
            // Obtenha todos os arquivos na pasta
            arquivos = Directory.GetFiles(pasta);
        }
        else
        {
            Console.WriteLine("A pasta especificada não existe.");
        }
        Console.WriteLine("Chose the document: ");
        foreach (string arquivo in arquivos)
        {
            // Exibe o nome do arquivo sem o caminho completo
            Console.WriteLine(Path.GetFileName(arquivo));
        }
        string chooseFile = Console.ReadLine();
        string normalizedCollectionName = chooseFile.Replace(" ", "_").Replace(".pdf", "").Replace(".txt", "");
        Console.WriteLine(normalizedCollectionName);

        
        var chromaCollectionModel = storeChormaDB.GetCollectionAsync(normalizedCollectionName, CancellationToken.None).Result;

        if (chromaCollectionModel == null)
        {
            Console.WriteLine("A coleção não foi encontrada.");
            storeChormaDB.CreateCollectionAsync(normalizedCollectionName, CancellationToken.None).Wait();
                    
            string filePath = $"../documents/{chooseFile}"; // Coloque o caminho do seu PDF aqui
            string extractedText = "";

            if (Path.GetExtension(chooseFile).ToLower() == ".pdf")
                extractedText = pdfReader.unstructured(filePath);
            
            if (Path.GetExtension(chooseFile).ToLower() == ".txt")
                extractedText = File.ReadAllText(filePath);

            Console.WriteLine(extractedText);
            var lines = TextChunker.SplitPlainTextLines(extractedText, 25);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 90);

            WriteParagraphsToConsole(paragraphs);
            
            List<string> extracted = new List<string>();
            extracted.AddRange(paragraphs);
            Task.Run(() => GenerateEmbeddings(extracted, normalizedCollectionName, urlChromaDB, CancellationToken.None)).Wait();
        }
        

        while (true)
        {
            Console.WriteLine("Ask your question: ");
            var message = Console.ReadLine();
            if(message != null){
                if(message == "/")
                {
                    Console.WriteLine("Which collection you want delete?");
                    string name = Console.ReadLine();
                    storeChormaDB.DeleteCollectionAsync(name, CancellationToken.None).Wait();
                    goto ReturnPoint;
                }
                var lines = TextChunker.SplitPlainTextLines(message, 25);
                var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 90);
                List<string> extracted = new List<string>();
                extracted.AddRange(paragraphs);
                Question(paragraphs, normalizedCollectionName);
            }            
        }
    }
    
    

    public static void Question(List<string> question, string nameCollection) // Alterar a assinatura para Task<string>
    {
        GenerateResponse(question, nameCollection, CancellationToken.None).Wait();
    }
    public static async Task GenerateResponse(List<string> question, string nameCollection,  CancellationToken cancellationToken)
    {
        
        string modelChat = "llama3.2:1b";
        var uri = new Uri("http://localhost:11434/");
        string model = "all-minilm";

     
        List<string> inputquestion = new List<string>();
        
        
        var ollama = new OllamaApiClient(uri, model);
        var ollama2 = new OllamaApiClient(uri, modelChat);
        EmbedResponse embedResponse = new EmbedResponse();
        embedResponse = await ollama.Embed(new EmbedRequest()
        {
            Model = model,
            Input = question,
        }, CancellationToken.None);
        
        
        // Supondo que EmbedResponse.Embeddings é um List<double[]>
        List<double[]> embeddings = embedResponse.Embeddings;

        // Converta List<double[]> para ReadOnlyMemory<float>[]
        ReadOnlyMemory<float>[] queryEmbeddings = embeddings
        .Select(embedding => new ReadOnlyMemory<float>(embedding.Select(e => (float)e).ToArray()))
        .ToArray();

        
        
        StoreChormaDB chromaDB = new StoreChormaDB(urlChromaDB);
        ChromaCollectionModel chromaCollectionModel = await chromaDB.GetCollectionAsync(nameCollection, cancellationToken);
        string collectionId = chromaCollectionModel.Id;
        string[] includes = 
        {
                "embeddings",
                "metadatas"
        };

        ChromaQueryResultModel chromaQueryResultModel = await chromaDB.QueryEmbeddingsAsync(collectionId, queryEmbeddings, 5, includes, CancellationToken.None);

        ChatRequest ollamaRequest = new ChatRequest
        {
            Model = modelChat 
        };
        
        if (chromaQueryResultModel == null)
        {
            Console.WriteLine("Resultado da consulta é nulo.");
            return; // Retorna para evitar um NullReferenceException
        }
        
        string context = " ";
        if (chromaQueryResultModel.Metadatas != null)
        {
            // Itera sobre cada lista de metadados
            foreach (var metadataList in chromaQueryResultModel.Metadatas)
            {
                foreach (var metadata in metadataList)
                {
                    foreach (var kvp in metadata)
                    {
                        // Adiciona cada chave-valor ao contexto
                        context += $"{kvp.Key}: {kvp.Value}\n";
                    }
                    // Adiciona uma linha separadora entre os embeddings para clareza
                    context += "------------------------\n";
                }
            }
        }
        else
        {
            // Define um contexto padrão caso não haja metadados
            context = "Nenhum metadado relevante encontrado.";
        }
        
        string questionFinal = string.Join(" ", question);
        string systemPrompt = "You are a helpful reading assistant who answers questions based on snippets of text provided in context. Answer only using the context provided, being as concise as possible. If you're unsure, just say that you don't know.";
        string prompt = $"prompt para o sistema:\n{systemPrompt}\n\nContext information:\n{context}\n\nQuestion: {questionFinal}\nAnswer:";

        var chatRequest = new ChatRequest
        {
            Model = modelChat,
            Messages = new List<Message>
            {
                new Message
                
                {
                    Role = "user",
                    Content = prompt
                }
            }
        };
        var chat = new Chat(ollama2);

        var chatResponseStream = ollama2.Chat(chatRequest, CancellationToken.None); // Use a instância de `chat` criada acima
        string completeMessage = "\nResposta completa:\n";
        await foreach (var response in chatResponseStream)
        {
            if (response != null)
            {
                completeMessage += response.Message.Content; // Acumula a resposta completa
            }
        }
        
        Console.WriteLine(completeMessage + "\n");
    }
    

    public static void GenerateEmbeddings(List<string> extractedText, string nameCollection, string urlChroma, CancellationToken cancellationToken)
    {
        
        var uri = new Uri("http://localhost:11434/");
        string model = "all-minilm";
        var ollama = new OllamaApiClient(uri, model);
        //ollama.SelectedModel = "all-minilm";

        EmbedResponse embedResponse  = ollama.Embed(new EmbedRequest()
        {
            Model = model,
            Input = extractedText,
        }, CancellationToken.None).Result;
        StoreEmbeddings(embedResponse, nameCollection, extractedText, CancellationToken.None).Wait();
    }

    public static async Task StoreEmbeddings(EmbedResponse embedResponse, string nameCollection, List<string> paragraphs, CancellationToken cancellationToken)
    {
        StoreChormaDB chromaDB = new StoreChormaDB(urlChromaDB);

        if (embedResponse.Embeddings != null)
        {            
            ChromaCollectionModel chromaCollectionModel = await chromaDB.GetCollectionAsync(nameCollection, cancellationToken);
            string collectionId = chromaCollectionModel.Id;

            string[] ids = new string[embedResponse.Embeddings.Count];
            ReadOnlyMemory<float>[] embeddings = new ReadOnlyMemory<float>[embedResponse.Embeddings.Count];
            object[] metadados = new object[embedResponse.Embeddings.Count];
            
            for (int i = 0; i < embedResponse.Embeddings.Count; i++)
            {
                ids[i] = Guid.NewGuid().ToString(); // Gera um ID único para cada embedding

                float[] floatEmbedding = Array.ConvertAll(embedResponse.Embeddings[i], item => (float)item);
                
                embeddings[i] = new ReadOnlyMemory<float>(floatEmbedding); // Converte embedding para ReadOnlyMemory<float>
                metadados[i] = new { Text = paragraphs[i] };
            
            }
            
            // Upsert das embeddings no ChromaDB com seus respectivos IDs
            await chromaDB.UpsertEmbeddingsAsync(collectionId, ids, embeddings, metadados, CancellationToken.None);


            string[] includes = {
                "embeddings"
            };
            
           
            ChromaEmbeddingsModel chromaEmbeddingsModel = new ChromaEmbeddingsModel();
            chromaEmbeddingsModel = await chromaDB.GetEmbeddingsAsync(collectionId, ids, includes, CancellationToken.None);
        }
    }

    public static void WriteParagraphsToConsole(List<string> paragraphs)
    {
        for (var i = 0; i < paragraphs.Count; i++)
        {
            Console.WriteLine(paragraphs[i]);

            if (i < paragraphs.Count - 1)
            {
                Console.WriteLine("------------------------");
            }
        }
    }

}