namespace CosmoBase.Abstractions.Interfaces;

public interface IDataWriteService<T>
    where T : class
{
    Task<T> SaveAsync(T obj);
    Task DeleteAsync(string id);
    Task DeleteAsync(string id, string partitionKey);
}