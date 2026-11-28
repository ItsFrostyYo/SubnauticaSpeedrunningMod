using System.Text;
using LitJson;

namespace ItsFrosti.SpeedrunningMod
{
    internal static class AccountProtocol
    {
        // Build a complete object: the game's legacy LitJson writer rejects
        // JsonMapper.ToJson(string) because a primitive cannot be a root value.
        internal static string StringRequest(string key, string value)
        {
            var text = new StringBuilder();
            var writer = new JsonWriter(text);
            writer.WriteObjectStart();
            writer.WritePropertyName(key);
            writer.Write(value);
            writer.WriteObjectEnd();
            return text.ToString();
        }
    }
}
