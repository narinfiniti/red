namespace PluginHost.Api.Middleware;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowUnencryptedAttribute : Attribute
{
}
