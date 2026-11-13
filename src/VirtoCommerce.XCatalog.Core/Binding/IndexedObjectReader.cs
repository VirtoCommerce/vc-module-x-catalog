using System;
using System.IO;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core.Serialization;

namespace VirtoCommerce.XCatalog.Core.Binding;

/// <summary>
/// Reads an object a search provider returned as a JSON string, with <see cref="ProductJsonSerializer"/>
/// in a single pass — no intermediate token tree.
/// </summary>
internal static class IndexedObjectReader
{
    /// <summary>
    /// Null when the payload's root is not a JSON object, as before. Once the object has started, a
    /// failure — malformed JSON, or a value of the wrong type for the contract — propagates instead of
    /// dropping the document silently. Reading follows <see cref="ProductJsonSerializer.ObjectSerializer"/>:
    /// a JSON null leaves the member at its default, and anything after the root object is not read.
    /// </summary>
    public static object Read(string json, Type type)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);

        if (!StartsWithObject(jsonReader))
        {
            return null;
        }

        return ProductJsonSerializer.ObjectSerializer.Deserialize(jsonReader, type);
    }

    public static T Read<T>(string json) => (T)Read(json, typeof(T));

    private static bool StartsWithObject(JsonTextReader reader)
    {
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.Comment)
                {
                    return reader.TokenType == JsonToken.StartObject;
                }
            }

            return false;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }
}
