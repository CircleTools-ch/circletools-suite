using System.Text.Json.Serialization;
using AppSuite.SecretSanta.Core.Persistence;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>Source-generated JSON context for persisting the collection of group mementos.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<GroupState>))]
public sealed partial class GroupStateJsonContext : JsonSerializerContext;
