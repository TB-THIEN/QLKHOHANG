using System.Data;

namespace QuanLyKhoWeb.Services.Interfaces
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
