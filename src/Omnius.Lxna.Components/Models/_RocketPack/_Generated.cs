
#nullable enable

namespace Omnius.Lxna.Components.Models
{
    public enum ThumbnailResizeType : byte
    {
        Pad = 0,
        Crop = 1,
    }
    public enum ThumbnailFormatType : byte
    {
        Png = 0,
    }

    public sealed partial class ThumbnailContent : global::Omnius.Core.RocketPack.IRocketPackObject<global::Omnius.Lxna.Components.Models.ThumbnailContent>, global::System.IDisposable
    {
        public static global::Omnius.Core.RocketPack.IRocketPackObjectFormatter<global::Omnius.Lxna.Components.Models.ThumbnailContent> Formatter => global::Omnius.Core.RocketPack.IRocketPackObject<global::Omnius.Lxna.Components.Models.ThumbnailContent>.Formatter;
        public static global::Omnius.Lxna.Components.Models.ThumbnailContent Empty => global::Omnius.Core.RocketPack.IRocketPackObject<global::Omnius.Lxna.Components.Models.ThumbnailContent>.Empty;

        static ThumbnailContent()
        {
            global::Omnius.Core.RocketPack.IRocketPackObject<global::Omnius.Lxna.Components.Models.ThumbnailContent>.Formatter = new ___CustomFormatter();
            global::Omnius.Core.RocketPack.IRocketPackObject<global::Omnius.Lxna.Components.Models.ThumbnailContent>.Empty = new global::Omnius.Lxna.Components.Models.ThumbnailContent(global::Omnius.Core.MemoryOwner<byte>.Empty);
        }

        private readonly global::System.Lazy<int> ___hashCode;

        public static readonly int MaxImageLength = 33554432;

        public ThumbnailContent(global::System.Buffers.IMemoryOwner<byte> image)
        {
            if (image is null) throw new global::System.ArgumentNullException("image");
            if (image.Memory.Length > 33554432) throw new global::System.ArgumentOutOfRangeException("image");

            _image = image;

            ___hashCode = new global::System.Lazy<int>(() =>
            {
                var ___h = new global::System.HashCode();
                if (!image.Memory.IsEmpty) ___h.Add(global::Omnius.Core.Helpers.ObjectHelper.GetHashCode(image.Memory.Span));
                return ___h.ToHashCode();
            });
        }

        private readonly global::System.Buffers.IMemoryOwner<byte> _image;
        public global::System.ReadOnlyMemory<byte> Image => _image.Memory;

        public static global::Omnius.Lxna.Components.Models.ThumbnailContent Import(global::System.Buffers.ReadOnlySequence<byte> sequence, global::Omnius.Core.IBytesPool bytesPool)
        {
            var reader = new global::Omnius.Core.RocketPack.RocketPackObjectReader(sequence, bytesPool);
            return Formatter.Deserialize(ref reader, 0);
        }
        public void Export(global::System.Buffers.IBufferWriter<byte> bufferWriter, global::Omnius.Core.IBytesPool bytesPool)
        {
            var writer = new global::Omnius.Core.RocketPack.RocketPackObjectWriter(bufferWriter, bytesPool);
            Formatter.Serialize(ref writer, this, 0);
        }

        public static bool operator ==(global::Omnius.Lxna.Components.Models.ThumbnailContent? left, global::Omnius.Lxna.Components.Models.ThumbnailContent? right)
        {
            return (right is null) ? (left is null) : right.Equals(left);
        }
        public static bool operator !=(global::Omnius.Lxna.Components.Models.ThumbnailContent? left, global::Omnius.Lxna.Components.Models.ThumbnailContent? right)
        {
            return !(left == right);
        }
        public override bool Equals(object? other)
        {
            if (!(other is global::Omnius.Lxna.Components.Models.ThumbnailContent)) return false;
            return this.Equals((global::Omnius.Lxna.Components.Models.ThumbnailContent)other);
        }
        public bool Equals(global::Omnius.Lxna.Components.Models.ThumbnailContent? target)
        {
            if (target is null) return false;
            if (object.ReferenceEquals(this, target)) return true;
            if (!global::Omnius.Core.BytesOperations.Equals(this.Image.Span, target.Image.Span)) return false;

            return true;
        }
        public override int GetHashCode() => ___hashCode.Value;

        public void Dispose()
        {
            _image?.Dispose();
        }

        private sealed class ___CustomFormatter : global::Omnius.Core.RocketPack.IRocketPackObjectFormatter<global::Omnius.Lxna.Components.Models.ThumbnailContent>
        {
            public void Serialize(ref global::Omnius.Core.RocketPack.RocketPackObjectWriter w, in global::Omnius.Lxna.Components.Models.ThumbnailContent value, in int rank)
            {
                if (rank > 256) throw new global::System.FormatException();

                if (!value.Image.IsEmpty)
                {
                    w.Write((uint)1);
                    w.Write(value.Image.Span);
                }
                w.Write((uint)0);
            }

            public global::Omnius.Lxna.Components.Models.ThumbnailContent Deserialize(ref global::Omnius.Core.RocketPack.RocketPackObjectReader r, in int rank)
            {
                if (rank > 256) throw new global::System.FormatException();

                global::System.Buffers.IMemoryOwner<byte> p_image = global::Omnius.Core.MemoryOwner<byte>.Empty;

                for (;;)
                {
                    uint id = r.GetUInt32();
                    if (id == 0) break;
                    switch (id)
                    {
                        case 1:
                            {
                                p_image = r.GetRecyclableMemory(33554432);
                                break;
                            }
                    }
                }

                return new global::Omnius.Lxna.Components.Models.ThumbnailContent(p_image);
            }
        }
    }


}