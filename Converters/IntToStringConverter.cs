using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aniki.Converters;

public class IntToStringConverter : JsonConverter<string>
{
    //MAL API sometimes returns given field as string and sometimes as int. EVEN THO IT SAYS "INTEGER" IN DOCS. 
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32().ToString();
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? "0";
        }
        return "0";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}