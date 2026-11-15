namespace UnoCockpit.Core.Storage;

public interface IKeyValueStore
{
    string? Get(string key);

    void Set(string key, string value);
}
