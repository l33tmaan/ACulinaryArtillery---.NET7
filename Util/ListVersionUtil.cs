using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ACulinaryArtillery.Util
{
    internal static class ListVersionUtil
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo?> VersionFieldCache = new();

        public static bool TryGetListVersion(object? list, out int version)
        {
            version = -1;
            if (list == null) return false;

            Type listType = list.GetType();
            FieldInfo? field = VersionFieldCache.GetOrAdd(listType, type => type.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic));
            if (field == null || field.FieldType != typeof(int)) return false;

            object? value = field.GetValue(list);
            if (value is not int intValue) return false;

            version = intValue;
            return true;
        }
    }
}
