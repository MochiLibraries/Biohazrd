using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Biohazrd
{
    // We restrict the metadata types to structs so we don't need to worry about supporting inheritance.
    // (Otherwise we might be expected to handle returning a derived type when a the request was for a base type.)
    public readonly struct DeclarationMetadata
    {
        private readonly ImmutableDictionary<Type, object>? Metadata;

        private DeclarationMetadata(ImmutableDictionary<Type, object>? metadata)
            => Metadata = metadata;

        public bool TryGet<T>(out T value)
            where T : struct, IDeclarationMetadataItem
        {
            if (Metadata is not null && Metadata.TryGetValue(typeof(T), out object? boxedValue))
            {
                Debug.Assert(boxedValue is T, $"The value corresponding to {typeof(T).FullName} must be of that type.");
                value = Unsafe.Unbox<T>(boxedValue);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public bool Has<T>()
            where T : struct, IDeclarationMetadataItem
            => Metadata is not null && Metadata.ContainsKey(typeof(T));

        public DeclarationMetadata Set<T>(T value)
            where T : struct, IDeclarationMetadataItem
        {
            ImmutableDictionary<Type, object> metadata = Metadata ?? ImmutableDictionary<Type, object>.Empty;
            return new DeclarationMetadata(metadata.SetItem(typeof(T), value));
        }

        public DeclarationMetadata Set<T>()
            where T : struct, IDeclarationMetadataItem
            => Set<T>(default);

        public DeclarationMetadata Add<T>(T value)
            where T : struct, IDeclarationMetadataItem
        {
            ImmutableDictionary<Type, object> metadata = Metadata ?? ImmutableDictionary<Type, object>.Empty;
            return new DeclarationMetadata(metadata.Add(typeof(T), value));
        }

        public DeclarationMetadata Add<T>()
            where T : struct, IDeclarationMetadataItem
            => Add<T>(default);

        public DeclarationMetadata Remove<T>()
            where T : struct, IDeclarationMetadataItem
        {
            if (Metadata is null)
            { return this; }

            return new DeclarationMetadata(Metadata.Remove(typeof(T)));
        }
    }
}
