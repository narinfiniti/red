namespace PluginHost.Security;

public interface IEcdhKeyProvider
{
    EcdhKeyMaterial CreateKeyMaterial();
}
