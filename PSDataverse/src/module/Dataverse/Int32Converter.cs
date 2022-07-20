using Newtonsoft.Json;
using System;
using System.Globalization;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace DataverseModule.Dataverse
{
    class Int32Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(int).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool existingIsNull = existingValue == null;
            if (!(existingIsNull || existingValue is int))
            {
                throw new JsonSerializationException("Int32Converter cannot read JSON with the specified existing value. System.Int32 is required.");
            }
            var value = (string)reader.Value;
            return value == null ? null : new System.ComponentModel.Int32Converter().ConvertFromInvariantString(value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is int))
            {
                throw new JsonSerializationException("Converter cannot write specified value to JSON. Int32 is required.");
            }
            writer.WriteValue("0x" + ((int)value).ToString("x", CultureInfo.InvariantCulture));
        }
    }
}
