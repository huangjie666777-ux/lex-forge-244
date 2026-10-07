namespace LexForge244;

/// <summary>正则抽象语法树节点。字符区间均为闭区间 [Lo, Hi]。</summary>
internal abstract record Node
{
    public sealed record Alt(IReadOnlyList<Node> Branches) : Node;
    public sealed record Cat(IReadOnlyList<Node> Items) : Node;
    public sealed record Star(Node Inner) : Node;
    public sealed record Plus(Node Inner) : Node;
    public sealed record Opt(Node Inner) : Node;
    public sealed record Chars(IReadOnlyList<(char Lo, char Hi)> Ranges) : Node;

    /// <summary>该节点是否可匹配空串。</summary>
    public static bool Nullable(Node n) => n switch
    {
        Alt a => a.Branches.Any(Nullable),
        Cat c => c.Items.All(Nullable),
        Star => true,
        Plus p => Nullable(p.Inner),
        Opt => true,
        Chars => false,
        _ => throw new InvalidOperationException(),
    };
}
