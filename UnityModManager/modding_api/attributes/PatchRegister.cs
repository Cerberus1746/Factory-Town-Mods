using System;

namespace ModdingAPI {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RegisterPatch : Attribute {
        public readonly string originalMethod;
        public readonly Type originalClass;
        public readonly string patch_type;

        public RegisterPatch(string originalMethod, Type originalClass, string patch_type) {
            this.originalMethod = originalMethod;
            this.originalClass = originalClass;
            this.patch_type = patch_type;
        }
    }
}