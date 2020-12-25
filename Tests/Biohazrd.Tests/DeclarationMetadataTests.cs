using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class DeclarationMetadataTests : BiohazrdTestBase
    {
        private readonly struct MyMetadata : IDeclarationMetadataItem
        {
            public readonly int Value;

            public MyMetadata(int value)
                => Value = value;
        }

        private readonly struct OtherMetadata : IDeclarationMetadataItem
        {
            public readonly int Value;

            public OtherMetadata(int value)
                => Value = value;
        }

        [Fact]
        public void DefaultMetadataIsEmpty()
        {
            DeclarationMetadata metadata = default;
            Assert.False(metadata.TryGet<MyMetadata>(out _));
            Assert.False(metadata.Has<MyMetadata>());
        }

        [Fact]
        public void CanAddToDefaultMetadata()
        {
            DeclarationMetadata metadata = default;
            Assert.False(metadata.Has<MyMetadata>());
            metadata = metadata.Add(new MyMetadata(3226));

            Assert.True(metadata.Has<MyMetadata>());
            MyMetadata value;
            Assert.True(metadata.TryGet(out value));
            Assert.Equal(3226, value.Value);
        }

        [Fact]
        public void CanSetInDefaultMetadata()
        {
            DeclarationMetadata metadata = default;
            Assert.False(metadata.Has<MyMetadata>());
            metadata = metadata.Set(new MyMetadata(3226));

            Assert.True(metadata.Has<MyMetadata>());
            MyMetadata value;
            Assert.True(metadata.TryGet(out value));
            Assert.Equal(3226, value.Value);
        }

        [Fact]
        public void AddingValueTwiceIsError()
        {
            DeclarationMetadata metadata = default;
            Assert.False(metadata.Has<MyMetadata>());
            metadata = metadata.Add(new MyMetadata(3226));
            Assert.Throws<ArgumentException>(() => metadata.Add(new MyMetadata(1337)));
        }

        [Fact]
        public void SettingValueTwiceReplacesValue()
        {
            DeclarationMetadata metadata = default;
            Assert.False(metadata.Has<MyMetadata>());
            metadata = metadata.Set(new MyMetadata(3226));
            metadata = metadata.Set(new MyMetadata(1337));

            Assert.True(metadata.Has<MyMetadata>());
            MyMetadata value;
            Assert.True(metadata.TryGet(out value));
            Assert.Equal(1337, value.Value);
        }

        [Fact]
        public void CanSetTwoTypesOfData()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Add(new MyMetadata(3226));
            metadata = metadata.Add(new OtherMetadata(1337));

            Assert.True(metadata.TryGet(out MyMetadata myValue));
            Assert.Equal(3226, myValue.Value);
            Assert.True(metadata.TryGet(out OtherMetadata otherValue));
            Assert.Equal(1337, otherValue.Value);
        }

        [Fact]
        public void RemovingFromDefaultDoesNothing()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Remove<MyMetadata>();
        }

        [Fact]
        public void CanRemoveExistingData()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Add(new MyMetadata(3226));
            Assert.True(metadata.Has<MyMetadata>());
            metadata = metadata.Remove<MyMetadata>();
            Assert.False(metadata.Has<MyMetadata>());
        }

        [Fact]
        public void DoubleRemoveDoesNothing()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Add(new MyMetadata(3226));
            Assert.True(metadata.Has<MyMetadata>());
            metadata = metadata.Remove<MyMetadata>();
            Assert.False(metadata.Has<MyMetadata>());
            metadata = metadata.Remove<MyMetadata>();
            Assert.False(metadata.Has<MyMetadata>());
        }

        [Fact]
        public void MetadataIsImmutable()
        {
            DeclarationMetadata metadata1 = default;
            DeclarationMetadata metadata2 = metadata1.Add(new MyMetadata(3226));
            DeclarationMetadata metadata3 = metadata2.Set(new MyMetadata(1337));

            Assert.False(metadata1.TryGet(out MyMetadata _));
            Assert.True(metadata2.TryGet(out MyMetadata value2));
            Assert.True(metadata3.TryGet(out MyMetadata value3));

            Assert.Equal(3226, value2.Value);
            Assert.Equal(1337, value3.Value);
        }

        [Fact]
        public void AddWithoutParameterAddsDefault()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Add<MyMetadata>();
            Assert.True(metadata.TryGet(out MyMetadata value));
            Assert.Equal(0, value.Value);
        }

        [Fact]
        public void SetWithoutParameterAddsDefault()
        {
            DeclarationMetadata metadata = default;
            metadata = metadata.Set<MyMetadata>();
            Assert.True(metadata.TryGet(out MyMetadata value));
            Assert.Equal(0, value.Value);
        }
    }
}
