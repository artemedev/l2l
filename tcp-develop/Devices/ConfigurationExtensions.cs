using Microsoft.Extensions.Configuration;
using System.Reflection;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddObject(this IConfigurationBuilder builder, object configObject, string sectionName)
    {
        if (configObject == null)
            throw new ArgumentNullException(nameof(configObject));
        // Преобразуем объект в словарь
        var dict = new Dictionary<string, string>();
        FlattenObject(configObject, dict, sectionName);
        return builder.AddInMemoryCollection(dict);
    }
    private static void FlattenObject(object obj, Dictionary<string, string> dict, string prefix)
    {
        if (obj == null) return;
        var type = obj.GetType();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = $"{prefix}:{property.Name}";
            var value = property.GetValue(obj)?.ToString();
            if (value != null)
            {
                dict[key] = value;
            }
        }
    }
}
