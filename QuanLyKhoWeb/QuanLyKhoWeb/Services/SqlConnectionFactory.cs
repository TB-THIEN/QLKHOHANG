using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Services
{
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
