using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodsBoundaryRunner.Core
{
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
