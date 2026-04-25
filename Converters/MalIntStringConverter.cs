using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aniki.Converters;

public class MalIntStringConverter : JsonConverter<int>
{
    //MAL API sometimes returns given field as string and sometimes as int. EVEN THO IT SAYS "INTEGER" IN DOCS. 
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetInt32();
            case JsonTokenType.String:
            {
                string? str = reader.GetString();
                return str == null ? 0 : int.Parse(str);
            }
            default:
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}