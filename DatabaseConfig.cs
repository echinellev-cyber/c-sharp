using System.Configuration;

namespace BiometricsFingerprint
{
    public static class DatabaseConfig
    {
        public static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["BiometricDb"].ConnectionString;
    }
}
