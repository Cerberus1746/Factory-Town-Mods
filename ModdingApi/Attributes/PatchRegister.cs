using System;

namespace ModdingAPI {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RegisterPatch : Attribute {
        public readonly string originalMethod;
        public readonly Type originalClass;
        public readonly string patch_type;
        public readonly Type[] parameters;

        public RegisterPatch(string originalMethod, Type originalClass, string patch_type, Type[] parameters = null) {
            this.originalMethod = originalMethod;
            this.originalClass = originalClass;
            this.patch_type = patch_type;
            this.parameters = parameters;
        }
    }
}