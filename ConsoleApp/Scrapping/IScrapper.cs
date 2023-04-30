namespace ConsoleApp.Scrapping;

public interface IScrapper<T>
{
    Task<T> GetData();
}