using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpServerLite
{
    /// <summary>
    /// Route type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RouteTypeEnum
    {
        /// <summary>
        /// Default route.
        /// </summary>
        [EnumMember(Value = "Default")]
        Default,
        /// <summary>
        /// Content route.
        /// </summary>
        [EnumMember(Value = "Content")]
        Content,
        /// <summary>
        /// Static route.
        /// </summary>
        [EnumMember(Value = "Static")]
        Static,
        /// <summary>
        /// Parameter route.
        /// </summary>
        [EnumMember(Value = "Parameter")]
        Parameter,
        /// <summary>
        /// Dynamic route.
        /// </summary>
        [EnumMember(Value = "Dynamic")]
        Dynamic
    }
}
