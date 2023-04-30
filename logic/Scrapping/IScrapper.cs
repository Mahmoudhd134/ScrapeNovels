namespace logic.Scrapping;

public interface IScrapper<T>
{
    Task<T> GetData();
}