using System;
using System.Text.RegularExpressions;

namespace ModdingAPI {
    static class Expansions {
        public static T ToEnum<T>(this string value) {
            value = Regex.Replace(value, @"\s+", "");
            return (T) Enum.Parse(typeof(T), value, true);
        }
    }
}
