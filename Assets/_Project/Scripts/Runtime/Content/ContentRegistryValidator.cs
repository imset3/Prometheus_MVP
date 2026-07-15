using System;

namespace Narthex.Content
{
    public static class ContentRegistryValidator
    {
        public static void Validate(ContentRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.BuildLookup();
        }
    }
}
