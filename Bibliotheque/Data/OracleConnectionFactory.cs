using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Bibliotheque.Data
{
    public class OracleConnectionFactory
    {
        private readonly IConfiguration _configuration;
        public OracleConnectionFactory(IConfiguration configuration)
        { 
        _configuration = configuration;
        }
        public OracleConnection GetConnection()
        {
            var connectionString = _configuration.GetConnectionString("OracleConnection");

            var connection = new OracleConnection(connectionString);

            return connection;
        }
    }
}
