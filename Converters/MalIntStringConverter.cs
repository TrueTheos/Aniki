using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aniki.Converters;

public class MalIntStringConverter : JsonConverter<int>
{
    //MAL API sometimes returns given field as string and sometimes as int. EVEN THO IT SAYS "INTEGER" IN DOCS. 
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (str == null) return 0;
            return int.Parse(str);
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}