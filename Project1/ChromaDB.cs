using Microsoft.SemanticKernel.Connectors.Chroma;

namespace ChromaDB;

public class StoreChormaDB : IChromaClient 
{
    private readonly string url;
    private readonly IChromaClient _chromaClient; 

        
    public StoreChormaDB(string urlChroma)
    {
        this.url = urlChroma;
        _chromaClient = new ChromaClient(url); 
    }
    
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
       await _chromaClient.CreateCollectionAsync(collectionName, cancellationToken);
    }

    public Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return _chromaClient.DeleteCollectionAsync(collectionName, cancellationToken);
    }

    public Task DeleteEmbeddingsAsync(string collectionId, string[] ids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<ChromaCollectionModel?>? GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try{
            return await _chromaClient.GetCollectionAsync(collectionName);
        }
        catch{
            return null;
        }
        
    }

    public async Task<ChromaEmbeddingsModel> GetEmbeddingsAsync(string collectionId, string[] ids, string[]? include = null, CancellationToken cancellationToken = default)
    {
        return await _chromaClient.GetEmbeddingsAsync(collectionId, ids, include, cancellationToken);
    }

    public IAsyncEnumerable<string> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return _chromaClient.ListCollectionsAsync();
    }

    public async Task<ChromaQueryResultModel> QueryEmbeddingsAsync(string collectionId, ReadOnlyMemory<float>[] queryEmbeddings, int nResults, string[]? include = null, CancellationToken cancellationToken = default)
    {
        return  await _chromaClient.QueryEmbeddingsAsync(collectionId, queryEmbeddings, nResults, include, cancellationToken);
    }

    public async Task UpsertEmbeddingsAsync(string collectionId, string[] ids, ReadOnlyMemory<float>[] embeddings, object[]? metadatas = null, CancellationToken cancellationToken = default)
    {
        await _chromaClient.UpsertEmbeddingsAsync(collectionId, ids, embeddings, metadatas, cancellationToken);
    }
    
}