using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Change;

/// <summary>
/// Maintains a canonical SHA-256 Merkle identity for an immutable Git inventory.
/// The Patricia-tree shape depends only on path digests, so equivalent inventories
/// have the same identity regardless of the order in which deltas were applied.
/// </summary>
internal sealed class GitSnapshotInventoryDigest
{
    private static readonly byte[] EmptyDigest = SHA256.HashData(
        "efforthours:git-snapshot-merkle:2:empty\0"u8);
    private static readonly byte[] EmptyPathSetDigest = SHA256.HashData(
        "efforthours:git-path-set-merkle:1:empty\0"u8);

    private readonly Lazy<string> _pathSetValue;
    private readonly Lazy<string> _value;
    private readonly Node? _root;

    private GitSnapshotInventoryDigest(Node? root)
    {
        _root = root;
        _value = new(() => $"sha256:{Convert.ToHexString(root?.Digest ?? EmptyDigest).ToLowerInvariant()}");
        _pathSetValue = new(() =>
            $"sha256:{Convert.ToHexString(root?.PathSetDigest ?? EmptyPathSetDigest).ToLowerInvariant()}");
    }

    public string PathSetValue => _pathSetValue.Value;

    public string Value => _value.Value;

    public static GitSnapshotInventoryDigest Create(IEnumerable<ChangeSnapshotFile> files)
    {
        KeyedFile[] keyedFiles = [.. files
            .Select(file => new KeyedFile(file, ComputePathKey(file.Path)))
            .OrderBy(file => file.Key, ByteArrayComparer.Instance)
            .ThenBy(file => file.File.Path, StringComparer.Ordinal)];
        if (keyedFiles.Length == 0)
        {
            return new GitSnapshotInventoryDigest(null);
        }

        List<Node> leaves = [];
        int start = 0;
        while (start < keyedFiles.Length)
        {
            int end = start + 1;
            while (end < keyedFiles.Length &&
                keyedFiles[start].Key.AsSpan().SequenceEqual(keyedFiles[end].Key))
            {
                end++;
            }

            ImmutableSortedDictionary<string, ChangeSnapshotFile>.Builder entries =
                ImmutableSortedDictionary.CreateBuilder<string, ChangeSnapshotFile>(
                    StringComparer.Ordinal);
            for (int index = start; index < end; index++)
            {
                entries.Add(keyedFiles[index].File.Path, keyedFiles[index].File);
            }

            leaves.Add(new Leaf(keyedFiles[start].Key, entries.ToImmutable()));
            start = end;
        }

        return new GitSnapshotInventoryDigest(Build(leaves, 0, leaves.Count));
    }

    public GitSnapshotInventoryDigest SetItem(ChangeSnapshotFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        byte[] key = ComputePathKey(file.Path);
        Node updated = Upsert(_root, key, file);
        return ReferenceEquals(updated, _root) ? this : new GitSnapshotInventoryDigest(updated);
    }

    public GitSnapshotInventoryDigest Remove(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Node? updated = Remove(_root, ComputePathKey(path), path, out bool removed);
        return removed ? new GitSnapshotInventoryDigest(updated) : this;
    }

    private static Node Build(IReadOnlyList<Node> leaves, int start, int count)
    {
        if (count == 1)
        {
            return leaves[start];
        }

        int end = start + count;
        int differingBit = FirstDifferingBit(leaves[start].Key, leaves[end - 1].Key);
        int low = start + 1;
        int high = end;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (GetBit(leaves[middle].Key, differingBit))
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        int split = low;
        return new Branch(
            differingBit,
            Build(leaves, start, split - start),
            Build(leaves, split, end - split));
    }

    private static Node Upsert(Node? root, byte[] key, ChangeSnapshotFile file)
    {
        if (root is null)
        {
            return Leaf.Single(key, file);
        }

        Leaf matching = FindLeaf(root, key);
        if (key.AsSpan().SequenceEqual(matching.Key))
        {
            return ReplaceLeaf(root, key, file);
        }

        int differingBit = FirstDifferingBit(key, matching.Key);
        return Insert(root, Leaf.Single(key, file), differingBit);
    }

    private static Leaf FindLeaf(Node node, byte[] key)
    {
        while (node is Branch branch)
        {
            node = GetBit(key, branch.BitIndex) ? branch.Right : branch.Left;
        }

        return (Leaf)node;
    }

    private static Node ReplaceLeaf(Node node, byte[] key, ChangeSnapshotFile file)
    {
        if (node is Leaf leaf)
        {
            ImmutableSortedDictionary<string, ChangeSnapshotFile> entries =
                leaf.Entries.SetItem(file.Path, file);
            return ReferenceEquals(entries, leaf.Entries)
                ? leaf
                : new Leaf(key, entries);
        }

        Branch branch = (Branch)node;
        return GetBit(key, branch.BitIndex)
            ? new Branch(branch.BitIndex, branch.Left, ReplaceLeaf(branch.Right, key, file))
            : new Branch(branch.BitIndex, ReplaceLeaf(branch.Left, key, file), branch.Right);
    }

    private static Branch Insert(Node node, Leaf leaf, int differingBit)
    {
        if (node is Branch branch && branch.BitIndex < differingBit)
        {
            return GetBit(leaf.Key, branch.BitIndex)
                ? new Branch(
                    branch.BitIndex,
                    branch.Left,
                    Insert(branch.Right, leaf, differingBit))
                : new Branch(
                    branch.BitIndex,
                    Insert(branch.Left, leaf, differingBit),
                    branch.Right);
        }

        return GetBit(leaf.Key, differingBit)
            ? new Branch(differingBit, node, leaf)
            : new Branch(differingBit, leaf, node);
    }

    private static Node? Remove(
        Node? node,
        byte[] key,
        string path,
        out bool removed)
    {
        if (node is null)
        {
            removed = false;
            return null;
        }

        if (node is Leaf leaf)
        {
            if (!key.AsSpan().SequenceEqual(leaf.Key) || !leaf.Entries.ContainsKey(path))
            {
                removed = false;
                return leaf;
            }

            ImmutableSortedDictionary<string, ChangeSnapshotFile> entries = leaf.Entries.Remove(path);
            removed = true;
            return entries.Count == 0 ? null : new Leaf(key, entries);
        }

        Branch branch = (Branch)node;
        if (GetBit(key, branch.BitIndex))
        {
            Node? right = Remove(branch.Right, key, path, out removed);
            return !removed
                ? branch
                : right is null
                    ? branch.Left
                    : new Branch(branch.BitIndex, branch.Left, right);
        }

        Node? left = Remove(branch.Left, key, path, out removed);
        return !removed
            ? branch
            : left is null
                ? branch.Right
                : new Branch(branch.BitIndex, left, branch.Right);
    }

    private static byte[] ComputePathKey(string path) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(path));

    private static int FirstDifferingBit(byte[] left, byte[] right)
    {
        for (int index = 0; index < left.Length; index++)
        {
            byte difference = (byte)(left[index] ^ right[index]);
            if (difference == 0)
            {
                continue;
            }

            for (int bit = 0; bit < 8; bit++)
            {
                if ((difference & (1 << (7 - bit))) != 0)
                {
                    return (index * 8) + bit;
                }
            }
        }

        throw new InvalidOperationException("Distinct inventory path keys were expected.");
    }

    private static bool GetBit(byte[] key, int bitIndex) =>
        (key[bitIndex / 8] & (1 << (7 - (bitIndex % 8)))) != 0;

    private static byte[] ComputeLeafDigest(
        byte[] key,
        ImmutableSortedDictionary<string, ChangeSnapshotFile> entries)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:git-snapshot-merkle:2:leaf\0"u8);
        hash.AppendData(key);
        foreach ((string path, ChangeSnapshotFile file) in entries)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.Mode));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.ObjectId));
            hash.AppendData([(byte)'\n']);
        }

        return hash.GetHashAndReset();
    }

    private static byte[] ComputeBranchDigest(int bitIndex, Node left, Node right)
    {
        Span<byte> encodedBit = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(encodedBit, bitIndex);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:git-snapshot-merkle:2:branch\0"u8);
        hash.AppendData(encodedBit);
        hash.AppendData(left.Digest);
        hash.AppendData(right.Digest);
        return hash.GetHashAndReset();
    }

    private static byte[] ComputeLeafPathSetDigest(
        byte[] key,
        ImmutableSortedDictionary<string, ChangeSnapshotFile> entries)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:git-path-set-merkle:1:leaf\0"u8);
        hash.AppendData(key);
        foreach (string path in entries.Keys)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
        }

        return hash.GetHashAndReset();
    }

    private static byte[] ComputeBranchPathSetDigest(int bitIndex, Node left, Node right)
    {
        Span<byte> encodedBit = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(encodedBit, bitIndex);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:git-path-set-merkle:1:branch\0"u8);
        hash.AppendData(encodedBit);
        hash.AppendData(left.PathSetDigest);
        hash.AppendData(right.PathSetDigest);
        return hash.GetHashAndReset();
    }

    private sealed record KeyedFile(ChangeSnapshotFile File, byte[] Key);

    private abstract class Node(
        byte[] key,
        byte[] digest,
        byte[] pathSetDigest)
    {
        public byte[] Key { get; } = key;

        public byte[] Digest { get; } = digest;

        public byte[] PathSetDigest { get; } = pathSetDigest;
    }

    private sealed class Leaf(
        byte[] key,
        ImmutableSortedDictionary<string, ChangeSnapshotFile> entries)
        : Node(
            key,
            ComputeLeafDigest(key, entries),
            ComputeLeafPathSetDigest(key, entries))
    {
        public ImmutableSortedDictionary<string, ChangeSnapshotFile> Entries { get; } = entries;

        public static Leaf Single(byte[] key, ChangeSnapshotFile file) => new(
            key,
            ImmutableSortedDictionary.Create<string, ChangeSnapshotFile>(StringComparer.Ordinal)
                .Add(file.Path, file));
    }

    private sealed class Branch(int bitIndex, Node left, Node right)
        : Node(
            left.Key,
            ComputeBranchDigest(bitIndex, left, right),
            ComputeBranchPathSetDigest(bitIndex, left, right))
    {
        public int BitIndex { get; } = bitIndex;

        public Node Left { get; } = left;

        public Node Right { get; } = right;
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return left.AsSpan().SequenceCompareTo(right);
        }
    }
}
