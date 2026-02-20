namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Wraps <see cref="ImmutableArray{T}"/> with value-based equality for use in incremental generator pipelines.
/// Roslyn's incremental pipeline uses equality to determine if downstream transforms need to re-run.
/// Since <see cref="ImmutableArray{T}"/> uses reference equality, this wrapper provides element-wise comparison.
/// </summary>
/// <typeparam name="T">The element type, which must implement <see cref="IEquatable{T}"/>.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public readonly ImmutableArray<T> Values;

    public EquatableArray(ImmutableArray<T> values)
    {
        Values = values.IsDefault ? ImmutableArray<T>.Empty : values;
    }

    public int Length => Values.Length;

    public bool IsEmpty => Values.IsEmpty;

    public bool Equals(EquatableArray<T> other)
    {
        if (Values.Length != other.Values.Length)
        {
            return false;
        }

        for (var i = 0; i < Values.Length; i++)
        {
            if (!Values[i].Equals(other.Values[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in Values)
            {
                hash = (hash * 31) + item.GetHashCode();
            }

            return hash;
        }
    }
}