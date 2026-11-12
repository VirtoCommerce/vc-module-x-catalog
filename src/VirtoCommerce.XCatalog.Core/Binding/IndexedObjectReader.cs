using System;
using System.IO;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core.Serialization;

namespace VirtoCommerce.XCatalog.Core.Binding
{
    /// <summary>
    /// Reads an object the indexer stored as a JSON string, with the serializer that wrote it.
    /// </summary>
    internal static class IndexedObjectReader
    {
        /// <summary>
        /// Null when the payload's root is not a JSON object. Any failure after the object has started —
        /// malformed JSON or a value that does not fit the contract — propagates: the indexer writes only
        /// what <see cref="ProductJsonSerializer"/> produced, so such a payload is a defect to surface.
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

        private static bool StartsWithObject(JsonReader reader)
        {
            try
            {
                return reader.Read() && reader.TokenType == JsonToken.StartObject;
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }
    }
}
